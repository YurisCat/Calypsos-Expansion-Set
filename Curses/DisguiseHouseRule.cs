using System.Reflection;
using EnhancedEditor;
using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Items;
using Mirror;
using UnityEngine;
using User;
using _200_Dev.BoardGameLogic;
using _200_Dev.Database.HouseRule;
using _200_Dev.EnvironmentElements.Endless;

namespace CalypsoExpansionSet.Curses;

// HouseRule is abstract in native IL2CPP despite its concrete interop wrapper.
// An empty native resume rule owns only configuration; the manager drives disguise.
internal sealed class DisguiseHouseRule : IDisposable
{
    internal const string Id = "calypso.identity_disguise";
    private const string NameTerm = "Calypso/Rules/Disguise/Name", DescriptionTerm = "Calypso/Rules/Disguise/Description";
    private readonly DisguiseCurse _curse;
    private readonly CalypsoExpansionSetPlugin _plugin;
    private HouseRule? _rule;
    private Il2CppSystem.Collections.Generic.List<HouseRule>? _registered;
    private LanguageSourceData? _languageSource;
    private float _nextTick;
    private bool _active, _pending, _ownsRound, _arrivalPlayed, _disposed;

    internal DisguiseHouseRule(DisguiseCurse curse, CalypsoExpansionSetPlugin plugin)
    { _curse = curse; _plugin = plugin; }

    internal bool Owns(HouseRule rule) => !_disposed && _rule != null && rule != null && rule.Pointer == _rule.Pointer;

    internal void EnsureRegistered(HouseRuleManager? manager)
    {
        if (_disposed || manager == null || manager.houseRules == null) return;
        EnsureLanguageSource();
        var rules = manager.houseRules;
        if (_registered?.Pointer == rules.Pointer && _rule != null && rules.Contains(_rule)) return;
        if (_rule == null)
        {
            var noSolo = HouseRuleManager.GetNoSoloTag(true);
            if (noSolo == null || !noSolo.HasValue) return;
            _rule = CreateRule(noSolo.Value);
        }
        foreach (var rule in rules)
            if (rule != null && rule.Id == Id && !Owns(rule))
                throw new InvalidOperationException($"House rule ID conflict: {Id}.");
        if (_registered != null) _registered.Remove(_rule);
        if (!rules.Contains(_rule)) rules.Add(_rule);
        _registered = rules;
        _plugin.Log.LogInfo($"[Disguise rule] registered: id={Id}, difficultyCost=2, multiplayerOnly=true.");
    }

    private static HouseRule CreateRule(Tag noSolo)
    {
        var rule = ScriptableObject.CreateInstance<HouseRuleResume>();
        if (rule == null) throw new InvalidOperationException("Could not create the native disguise house rule configuration.");
        try
        {
            // The native callback only resumes entries from this list. Keep our
            // own list empty so applying disguise cannot resume another event.
            rule._eventsToResume = new Il2CppSystem.Collections.Generic.List<GameEventData>();
            rule.name = Id;
            rule.hideFlags = HideFlags.HideAndDontSave;
            rule.debugName = Id;
            rule.ruleName = new LocalizedString { mTerm = NameTerm };
            rule.ruleDescription = new LocalizedString { mTerm = DescriptionTerm };
            rule.ruleDifficulty = HouseRule.EHouseRuleDifficulty.Difficulty2;
            rule.minEndlessRank = 0;
            rule._tags = new TagGroup();
            rule._tags.AddTag(noSolo);
            return rule;
        }
        catch
        {
            UnityEngine.Object.Destroy(rule);
            throw;
        }
    }

    internal void Refresh(HouseRuleManager manager, bool beginRound)
    {
        if (_disposed || !NetworkServer.active) return;
        EnsureRegistered(manager);
        var active = manager.IsRuleActive(Id);
        if (_active != active)
        {
            _active = active;
            _arrivalPlayed = false;
            if (!active)
            {
                _pending = false;
                if (_ownsRound) _curse.ClearRound();
                _ownsRound = false;
            }
            _plugin.Log.LogInfo($"[Disguise rule] {(active ? "applied" : "removed")}.");
        }
        if (active && beginRound) _pending = true;
    }

    internal void Tick()
    {
        if (_disposed || Time.realtimeSinceStartup < _nextTick) return;
        _nextTick = Time.realtimeSinceStartup + 0.5f;
        var manager = HouseRuleManager.Instance;
        if (manager == null)
        {
            if (_ownsRound) _curse.ClearRound();
            Reset();
            return;
        }
        EnsureRegistered(manager);
        if (!NetworkServer.active) return;
        Refresh(manager, false);
        if (!_active || !_pending) return;
        // A dice curse already running this round keeps its own lifetime. Removing
        // the rule must not erase that independently acquired curse.
        if (_curse.Active) { _arrivalPlayed = true; _pending = false; return; }
        if (_curse.Start(out _, playArrival: !_arrivalPlayed))
        { _arrivalPlayed = true; _ownsRound = true; _pending = false; }
    }

    internal void EndRound() { _pending = false; _ownsRound = false; }
    internal void Reset() { _active = _pending = _ownsRound = _arrivalPlayed = false; }

    internal string GetDescription()
    {
        EnsureLanguageSource();
        return new LocalizedString { mTerm = DescriptionTerm }.ToString()
            ?? throw new InvalidOperationException("Disguise rule description could not be localized.");
    }

    private void EnsureLanguageSource()
    {
        if (_languageSource == null)
        {
            _languageSource = new LanguageSourceData();
            // I2 resolves translations by CurrentLanguage (name), not by Code.
            // These names must match the native I2Languages resource exactly.
            var names = new[] { "English", "Simplified_chinese", "Traditional_chinese", "Japanese", "Russian", "Portuguese_brazil", "Spanish" };
            var codes = new[] { "en", "zh-CN", "zh-TW", "ja", "ru", "pt-BR", "es" };
            for (var i = 0; i < names.Length; i++)
                _languageSource.mLanguages.Add(new LanguageData { Name = names[i], Code = codes[i] });
            AddTerm(NameTerm, new[] { "Disguise", "伪装", "偽裝", "変装", "Маскировка", "Disfarce", "Disfraz" });
            AddTerm(DescriptionTerm, new[] {
                "Players swap appearances, with new assignments each round.",
                "玩家互换外观，小局重新分配",
                "玩家互換外觀，小局重新分配",
                "プレイヤーの見た目が入れ替わり、ラウンドごとに再割り当てされます。",
                "Игроки меняются внешностью. Распределение меняется каждый раунд.",
                "Os jogadores trocam de aparência, com nova distribuição a cada rodada.",
                "Los jugadores intercambian apariencias, reasignadas en cada ronda." });
            _languageSource.UpdateDictionary(true);
        }
        if (!LocalizationManager.Sources.Contains(_languageSource)) LocalizationManager.Sources.Add(_languageSource);
    }

    private void AddTerm(string key, string[] translations) => _languageSource!.mTerms.Add(new TermData
    {
        Term = key,
        Languages = new Il2CppStringArray(translations),
        Flags = new Il2CppStructArray<byte>(translations.Length)
    });

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Reset();
        if (_rule != null)
        {
            _registered?.Remove(_rule);
            UnityEngine.Object.Destroy(_rule);
        }
        if (_languageSource != null) LocalizationManager.Sources.Remove(_languageSource);
        _registered = null; _rule = null; _languageSource = null;
    }
}

[HarmonyPatch(typeof(HouseRule), "get_Id")]
internal static class DisguiseRuleIdPatch
{
    private static bool Prefix(HouseRule __instance, ref string __result)
    {
        if (CalypsoExpansionSetPlugin.Instance?.Disguises?.HouseRule.Owns(__instance) != true) return true;
        __result = DisguiseHouseRule.Id;
        return false;
    }
}

// Cover selection, save restoration and client SyncSet callbacks before they
// resolve a custom ID. Registration is also maintained when managers change.
[HarmonyPatch(typeof(HouseRuleManager))]
internal static class DisguiseRuleRegistrationPatch
{
    [HarmonyPatch(nameof(HouseRuleManager.OnStartClient)), HarmonyPrefix]
    private static void StartClient(HouseRuleManager __instance) => Register(__instance);
    [HarmonyPatch(nameof(HouseRuleManager.GetHouseRuleById)), HarmonyPrefix]
    private static void Lookup(HouseRuleManager __instance) => Register(__instance);
    internal static void Register(HouseRuleManager? manager)
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        try { plugin?.Disguises?.HouseRule.EnsureRegistered(manager); }
        catch (Exception error) { plugin?.Fail(error); }
    }

    private static void Refresh(HouseRuleManager manager, bool beginRound)
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        try { plugin?.Disguises?.HouseRule.Refresh(manager, beginRound); }
        catch (Exception error) { plugin?.Fail(error); }
    }

    [HarmonyPatch(nameof(HouseRuleManager.SrvApplyCachedHouseRules)), HarmonyPostfix]
    private static void Apply(HouseRuleManager __instance) => Refresh(__instance, true);
    [HarmonyPatch(nameof(HouseRuleManager.SrvRemoveHouseRules)), HarmonyPostfix]
    private static void Remove(HouseRuleManager __instance) => Refresh(__instance, false);
    [HarmonyPatch(nameof(HouseRuleManager.SrvNotifyHouseRulesOfEventStart)), HarmonyPostfix]
    private static void Event(HouseRuleManager __instance) => Refresh(__instance, true);
}

// The native generator takes Nullable<bool> by value. Its generated Harmony
// trampoline treats that value as a pointer; register at its three callers instead.
[HarmonyPatch]
internal static class DisguiseRuleGenerationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(BoardGameManager), nameof(BoardGameManager.SrvInitializeBoardGameRules));
        yield return AccessTools.DeclaredMethod(typeof(BoardGameToolShop), nameof(BoardGameToolShop.OnStartServer));
        yield return AccessTools.DeclaredMethod(typeof(EndlessManager), nameof(EndlessManager.SrvSpawnEndlessBoards));
    }

    private static void Prefix() => DisguiseRuleRegistrationPatch.Register(HouseRuleManager.Instance);
}

// Filter before random selection, even when a caller overrides the native solo
// flag. Room membership includes dead players and players awaiting their bodies.
[HarmonyPatch(typeof(HouseRuleManager.__c__DisplayClass51_0), "_SrvGenerateHouseRulesByBoardConfig_b__0")]
internal static class DisguiseRuleCandidatePatch
{
    private static void Postfix(HouseRule __0, ref bool __result)
    {
        if (__result && NetworkServer.active
            && CalypsoExpansionSetPlugin.Instance?.Disguises?.HouseRule.Owns(__0) == true
            && (UserInstancesManager.Instance?.Count ?? 0) < 2)
            __result = false;
    }
}
