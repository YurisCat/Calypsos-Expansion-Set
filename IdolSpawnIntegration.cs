using HarmonyLib;
using Items;
using Mirror;
using UnityEngine;

namespace CalypsoExpansionSet;

internal sealed class IdolSpawnIntegration
{
    internal sealed class Batch : IDisposable
    {
        internal readonly int Number;
        internal ItemSet? Copy;
        internal readonly Dictionary<string, int> Spawned = new(StringComparer.Ordinal);
        internal Batch(int number) => Number = number;
        public void Dispose() { if (Copy != null) UnityEngine.Object.Destroy(Copy); Copy = null; }
    }

    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly Stack<Batch> _batches = new();
    private int _batchNumber;
    internal IdolSpawnIntegration(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    internal Batch? Begin(ItemDropManager manager, int seed, ref ItemSet set, GameEventData gameEvent, bool ignoreCap)
    {
        if (!NetworkServer.active || set == null) return null;
        _plugin.PrepareRegistry();
        var batch = new Batch(++_batchNumber);
        try
        {
            var allowed = _plugin.CanAddNaturalContent;
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            batch.Copy = UnityEngine.Object.Instantiate(set);
            batch.Copy.name = $"CalypsoExpansionSet Batch {batch.Number}";
            batch.Copy.hideFlags = HideFlags.DontSave;
            var entries = new Il2CppSystem.Collections.Generic.List<ItemSetEntry>();
            if (set.spawnItems != null)
                foreach (var original in set.spawnItems)
                {
                    if (original == null) { entries.Add(original!); continue; }
                    var entry = new ItemSetEntry
                    {
                        spawnCountAtMinDifficulty = original.spawnCountAtMinDifficulty,
                        spawnCountAtMaxDifficulty = original.spawnCountAtMaxDifficulty,
                        legacyCount = original.legacyCount,
                        _loggedLegacyFallback = original._loggedLegacyFallback,
                        items = original.items == null ? null : new Il2CppSystem.Collections.Generic.List<SOTool>()
                    };
                    if (original.items != null)
                    {
                        foreach (var item in original.items)
                            if (item == null || !_plugin.Registry.TryGet(item.Id, out _) || allowed)
                                entry.items!.Add(item!);
                        if (allowed)
                            foreach (var registered in _plugin.Registry.Entries)
                            {
                                var hasTemplate = false;
                                var hasType = false;
                                foreach (var item in entry.items!)
                                {
                                    if (item == null) continue;
                                    hasTemplate |= item.Id == registered.Definition.TemplateId;
                                    hasType |= item.Id == registered.Definition.ConfigId;
                                }
                                if (hasTemplate && !hasType) entry.items.Add(registered.Data!);
                                if (hasTemplate || hasType) candidates.Add(registered.Definition.Key);
                            }
                    }
                    entries.Add(entry);
                }
            batch.Copy.spawnItems = set.spawnItems == null ? null : entries;
            // Bonus items may be guaranteed drops: never append a type and increase native totals.
            if (!allowed && set.bonusItems != null)
            {
                var bonus = new Il2CppSystem.Collections.Generic.List<SOTool>();
                foreach (var item in set.bonusItems)
                    if (item == null || !_plugin.Registry.TryGet(item.Id, out _)) bonus.Add(item!);
                batch.Copy.bonusItems = bonus;
            }
            var weights = string.Join(", ", candidates.OrderBy(key => key, StringComparer.Ordinal).Select(key =>
            {
                _plugin.Registry.TryGet(key, out var entry);
                return $"{key}(weight={manager.GetIdolWeight(entry.Data!):G})";
            }));
            _plugin.Log.LogInfo($"[Pool] batch={batch.Number}, eventId={(gameEvent != null ? gameEvent.gameEventId : -1)}, "
                + $"seed={seed}, set={set.name}, enabled={allowed}, MOD candidates before native filters=[{weights}], "
                + $"maxPerType={manager.maxSpawnsPerIdolType}, ignoreCap={ignoreCap || gameEvent != null && gameEvent.IgnoreIdolSpawnTypeCap}, "
                + $"defaultWeight={manager.defaultIdolWeight}, divisor={manager.idolWeightDivisor}.");
            set = batch.Copy;
            _batches.Push(batch);
            return batch;
        }
        catch
        {
            batch.Dispose();
            throw;
        }
    }

    internal void Observe(Item? item)
    {
        var idol = item != null ? item.TryCast<ScoreTool>() : null;
        if (idol == null || idol.SoItem == null || !_plugin.Registry.TryGet(idol.SoItem.Id, out var entry)) return;
        _plugin.Sync.Track(idol, false);
        if (_batches.Count != 0)
        {
            var batch = _batches.Peek();
            batch.Spawned.TryGetValue(entry.Definition.Key, out var count);
            batch.Spawned[entry.Definition.Key] = count + 1;
            _plugin.Log.LogInfo($"[Natural spawn] batch={batch.Number}, type={entry.Definition.Key}, netId={idol.netId}, value={idol.CurrentValue}.");
        }
    }

    internal void End(Batch? batch, Exception? exception)
    {
        if (batch == null) return;
        try
        {
            if (_batches.Count != 0 && ReferenceEquals(_batches.Peek(), batch)) _batches.Pop();
            var results = string.Join(", ", batch.Spawned.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key} x{pair.Value}"));
            _plugin.Log.LogInfo($"[Batch result] batch={batch.Number}, completed={exception == null}, actually spawned MOD idols=[{results}].");
        }
        finally { batch.Dispose(); }
    }
}

[HarmonyPatch(typeof(ItemDropManager), nameof(ItemDropManager.SrvSpawnItemsForData))]
internal static class NaturalIdolPoolPatch
{
    private static void Prefix(ItemDropManager __instance, int seed, ref ItemSet set, GameEventData gameEventData,
        bool ignoreIdolSpawnTypeCap, out IdolSpawnIntegration.Batch? __state)
    {
        __state = null;
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (plugin == null) return;
        try { __state = plugin.Spawns.Begin(__instance, seed, ref set, gameEventData, ignoreIdolSpawnTypeCap); }
        catch (Exception exception) { plugin.Fail(exception); }
    }
    private static Exception? Finalizer(Exception? __exception, IdolSpawnIntegration.Batch? __state)
    {
        CalypsoExpansionSetPlugin.Instance?.Spawns.End(__state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(ScoreTool), nameof(ScoreTool.OnStartServer))]
internal static class NaturalIdolSpawnPatch
{
    // Hook the parameterless lifecycle method: native nullable score arguments in
    // SrvSpawnItem can be null and fail in an IL2CPP Harmony trampoline before our code runs.
    private static void Postfix(ScoreTool __instance) => CalypsoExpansionSetPlugin.Instance?.Spawns.Observe(__instance);
}

[HarmonyPatch(typeof(ItemDropManager), nameof(ItemDropManager.ResetIdolWeights))]
internal static class IdolWeightResetPatch
{
    private static void Postfix() => CalypsoExpansionSetPlugin.Instance?.Registry.ApplyInitialWeights();
}

[HarmonyPatch(typeof(NetworkClient), "PrepareToSpawnSceneObjects")]
internal static class IdolNetworkPrefabPatch
{
    private static void Postfix() => CalypsoExpansionSetPlugin.Instance?.PrepareRegistry();
}

[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.Ready))]
internal static class IdolBeforeClientReadyPatch
{
    private static void Prefix() => CalypsoExpansionSetPlugin.Instance?.PrepareRegistry();
}

[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.SetClientReady))]
internal static class IdolServerReadyPatch
{
    private static bool Prefix(NetworkConnectionToClient conn) => CalypsoExpansionSetPlugin.Instance?.Sync.AllowReady(conn) ?? true;
}
