using Entities.Player;
using Entities.Player.Behaviours;
using Gameplay_Elements;
using HarmonyLib;
using Mirror;
using TMPro;
using UnityEngine;
using _200_Dev.Entities;
using _200_Dev.UI.Messages;
using _200_Dev.UI.Networking;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.UI;

// Local presentation only. Native interaction chooses the target/range/occlusion.
// Native message queues, tutorials, input labels and value formatting stay owned
// by the game; this controller owns only its extra TMP label.
internal sealed class IdolHoverInfo : IDisposable
{
    internal static string GenericIdolName =>
        IdolTextLocalization.GenericName(CalypsoExpansionSetPlugin.Instance?.HoverInfo?._useChinese == true);
    private readonly CalypsoExpansionSetPlugin _plugin;
    private UIManager? _ui;
    private TextMeshProUGUI? _reference, _label;
    private GameObject? _object;
    private Canvas? _canvas;
    private bool _disposed, _faulted, _visible;
    private bool _useChinese, _languageWarning;
    private float _nextLanguageCheck;
    private uint _shownId;
    private float _nativeMessageUntil;
    private MessageUi? _testMessage;
    private string? _testPreviousText;
    private const string TestMessage = "Native tutorial priority test (20s)";

    internal bool TestMessagePriority()
    {
        var ui = UIManager.Instance;
        if (!Testing.TestRoomLaunch.IsActive || !_plugin.DebugTools.Enabled
            || ui?.globalMessageUi == null || TextVisible(ui.globalMessageUi)) return false;
        RestoreTestMessage();
        _testMessage = ui.globalMessageUi;
        _testPreviousText = _testMessage.CurrentText;
        _testMessage.SetText(TestMessage);
        _testMessage.ShowForDuration(20f);
        return true;
    }

    private void RestoreTestMessage()
    {
        if (_testMessage != null && _testMessage.CurrentText == TestMessage)
        { _testMessage.Hide(); _testMessage.SetText(_testPreviousText ?? ""); }
        _testMessage = null; _testPreviousText = null;
    }

    internal IdolHoverInfo(CalypsoExpansionSetPlugin plugin)
    {
        _plugin = plugin;
        RefreshLanguage();
    }

    private void RefreshLanguage()
    {
        // Sample outside interaction getters: no per-frame native string
        // marshaling, reflection, source scans or localization subscriptions.
        var now = Time.realtimeSinceStartup;
        if (now < _nextLanguageCheck) return;
        _nextLanguageCheck = now + 0.5f;
        try
        {
            var code = I2.Loc.LocalizationManager.CurrentLanguageCode;
            _useChinese = IdolTextLocalization.IsChinese(code,
                string.IsNullOrEmpty(code) ? I2.Loc.LocalizationManager.CurrentLanguage : null);
        }
        catch (Exception error)
        {
            // An unready localization source must not disable hover/gameplay.
            // Keep the last selection (initially English) and retry next sample.
            if (_languageWarning) return;
            _languageWarning = true;
            _plugin.Log.LogWarning($"Idol UI language unavailable; keeping the last language and retrying: {error}");
        }
    }

    internal static bool HintsHidden(ScoreTool? idol)
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        var holder = idol != null ? idol.ItemOwner : null;
        return plugin != null && !plugin.RuntimeFailed && plugin.Registry.Ready
            && NetworkClient.isConnected && NetworkClient.ready && plugin.Sync.ClientCompatible
            && idol != null && idol.SoItem != null && holder != null
            && holder.isActiveAndEnabled && holder.CurrentHealthSync > 0 && holder.InventoryNew?.InHand == idol
            && plugin.Registry.TryGet(idol.SoItem.Id, out var entry) && entry.Definition.HideHintsWhileHeld;
    }

    internal static bool TryText(ScoreTool? idol, out IdolHoverText text)
    {
        text = default;
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (idol == null || idol.SoItem == null || plugin == null || plugin.RuntimeFailed || !plugin.Registry.Ready)
            return false;
        if (plugin.Registry.TryGet(idol.SoItem.Id, out var entry))
        {
            var definition = entry.Definition;
            if (string.IsNullOrEmpty(definition.ShortDescription)) return false;
            text = new(definition.Key, definition.DisplayName, definition.ShortDescription, true);
            text = IdolTextLocalization.Resolve(definition.ConfigId, text, plugin.HoverInfo?._useChinese == true);
            return true;
        }
        if (!OriginalIdolAdvice.TryGet(idol.SoItem.Id, out text)) return false;
        text = IdolTextLocalization.Resolve(idol.SoItem.Id, text, plugin.HoverInfo?._useChinese == true);
        return true;
    }

    private bool ControlsReady(out PlayerEntity player)
    {
        player = EntityManager.Me!;
        return !_disposed && !_faulted && !_plugin.RuntimeFailed && _plugin.Registry.Ready
            && NetworkClient.isConnected && NetworkClient.ready && _plugin.Sync.ClientCompatible
            && player != null && player.isActiveAndEnabled && player.CurrentHealthSync > 0
            && player.PlayerInteraction != null && player.PlayerInteraction.isOwned
            && player.PlayerInteraction.CanInteract && _plugin.LocalGameplayReady(player)
            && !UIManager.IsGameOver;
    }

    // Also runs when the native interaction component is disabled (menus,
    // death, disconnect). This prevents a stale label from surviving that path.
    internal void Tick()
    {
        if (_disposed) return;
        RefreshLanguage();
        if (_faulted) return;
        try
        {
            var ui = UIManager.Instance;
            if (_testMessage != null && (!Testing.TestRoomLaunch.IsActive
                || !NetworkClient.isConnected || !TextVisible(_testMessage) && Time.realtimeSinceStartup >= _nativeMessageUntil))
                RestoreTestMessage();
            if (ui != _ui || !NetworkClient.isConnected)
            {
                ReleaseLabel();
                _nativeMessageUntil = 0;
                _ui = ui;
            }
            if (!_plugin.ShowIdolAdvice || !ControlsReady(out _) || NarrationVisible(ui)) Hide();
        }
        catch (Exception error) { Fault(error); }
    }

    internal void UpdateForInteraction(PlayerInteraction interaction)
    {
        if (_disposed || _faulted) return;
        try
        {
            var local = EntityManager.Me;
            // Remote players also execute LateUpdate; they must not clear or
            // replace the local player's hint.
            if (local == null || local.PlayerInteraction != interaction) return;
            if (!_plugin.ShowIdolAdvice || !ControlsReady(out _)) { Hide(); return; }
            var target = interaction.Hovered;
            // Native collider targets can be InterectableLinker wrappers.
            // Resolve through the game's interface before identifying the item.
            var real = target != null ? target.GetRealInteractable() : null;
            var idol = real != null ? real.TryCast<ScoreTool>() : null;
            if (HintsHidden(idol) || !TryText(idol, out var definition)) { Hide(); return; }
            var ui = UIManager.Instance;
            if (ui == null || ui.rootUI == null || NarrationVisible(ui)) { Hide(); return; }
            var reference = ui.globalMessageUi != null ? ui.globalMessageUi.text : null;
            if (reference == null || reference.font == null) { Hide(); return; }
            EnsureLabel(ui, reference);
            if (!Layout(reference, ui, NativeMessageVisible(ui))) { Hide(); return; }
            if (_label == null) return;
            if (_label.text != definition.ShortDescription) _label.text = definition.ShortDescription;
            if (!_visible) { _object!.SetActive(true); _visible = true; }
            if (_shownId != idol!.netId)
            {
                _shownId = idol.netId;
                if (_plugin.DebugTools.Enabled)
                    _plugin.Log.LogInfo($"[Idol hint] {definition.Key}: {definition.DisplayName} | {definition.ShortDescription}");
            }
        }
        catch (Exception error) { Fault(error); }
    }

    // Native IsDisplayed only checks its own CanvasGroup alpha. An inactive
    // object, transparent ancestor or empty retained text can still return true.
    private static bool TextVisible(MessageUi? message) =>
        message != null && message.isActiveAndEnabled && message.text != null
        && message.text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(message.text.text)
        && message.text.color.a > 0.01f && VisibleHierarchy(message.text.transform);

    private static bool VisibleHierarchy(Transform transform)
    {
        if (!transform.gameObject.activeInHierarchy) return false;
        var alpha = 1f;
        var groupsIgnored = false;
        for (var depth = 0; transform != null && depth < 64; depth++, transform = transform.parent)
        {
            var canvas = transform.GetComponent<Canvas>();
            if (canvas != null && !canvas.enabled) return false;
            if (groupsIgnored) continue;
            var group = transform.GetComponent<CanvasGroup>();
            if (group == null || !group.enabled) continue;
            alpha *= group.alpha;
            if (!float.IsFinite(alpha) || alpha <= 0.01f) return false;
            groupsIgnored = group.ignoreParentGroups;
        }
        return true;
    }

    private static bool NarrationVisible(UIManager? ui) => ui != null && ui.narrationBackground != null
        && ui.narrationBackground.enabled && ui.narrationBackground.alpha > 0.01f
        && VisibleHierarchy(ui.narrationBackground.transform);

    private bool NativeMessageVisible(UIManager? ui) =>
        Time.realtimeSinceStartup < _nativeMessageUntil
        || ui != null && (TextVisible(ui.globalMessageUi) || TextVisible(ui.messageUi));

    internal void NativeMessageStarting(MessageUi message)
    {
        if (_disposed || _faulted) return;
        try
        {
            var ui = UIManager.Instance;
            if (ui == null || message != ui.globalMessageUi && message != ui.messageUi) return;
            // Reserve a separate line before the first fade-in frame. A queue
            // alone is not visibility and must never suppress hints forever.
            var fade = float.IsFinite(message.fadeDuration) ? Math.Max(0.1f, message.fadeDuration) : 0.25f;
            _nativeMessageUntil = Time.realtimeSinceStartup + fade;
            Hide();
        }
        catch (Exception error) { Fault(error); }
    }

    private void EnsureLabel(UIManager ui, TextMeshProUGUI reference)
    {
        if (_label != null && _object != null && _ui == ui && _reference == reference) return;
        ReleaseLabel();
        _ui = ui;
        _reference = reference;
        _object = new GameObject("Calypso Idol Hover Hint");
        _object.SetActive(false);
        _object.layer = reference.gameObject.layer;
        _object.hideFlags = HideFlags.DontSave;
        _canvas = _object.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = ui.rootUI.sortingOrder + 1;
        _canvas.targetDisplay = ui.rootUI.targetDisplay;
        var textObject = new GameObject("Calypso Idol Hover Text") { layer = _object.layer, hideFlags = HideFlags.DontSave };
        textObject.transform.SetParent(_object.transform, false);
        _label = textObject.AddComponent<TextMeshProUGUI>();
        _label.raycastTarget = false;
        _label.maskable = false;
        _label.richText = false;
        _label.enableAutoSizing = true;
        _label.overflowMode = TextOverflowModes.Ellipsis;
        var rect = _label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
    }

    private bool Layout(TextMeshProUGUI reference, UIManager ui, bool occupied)
    {
        var label = _label!;
        var source = reference.rectTransform;
        var rect = label.rectTransform;
        // The independent canvas cannot inherit a disabled/transparent native
        // message container. Keep its native font and top-message placement.
        var root = ui.rootUI;
        var sourceScale = source.lossyScale;
        var factor = root.scaleFactor;
        if (!float.IsFinite(factor) || factor <= 0) factor = Math.Max(0.5f, Screen.height / 1080f);
        _canvas!.scaleFactor = factor;
        var camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        var screen = RectTransformUtility.WorldToScreenPoint(camera, source.position);
        var width = Math.Abs(source.rect.width * sourceScale.x);
        var height = Math.Abs(source.rect.height * sourceScale.y);
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 1 || height <= 1
            || !float.IsFinite(screen.x) || !float.IsFinite(screen.y)
            || screen.y < 0 || screen.y > Screen.height)
        {
            width = Screen.width * 0.85f; height = 64f * factor;
            screen = new Vector2(Screen.width * 0.5f, Screen.height - 72f * factor);
        }
        rect.pivot = source.pivot;
        rect.sizeDelta = new Vector2(Math.Min(width, Screen.width - 24f) / factor, height / factor);
        rect.position = new Vector3(screen.x, screen.y - (occupied ? height + 8f * factor : 0f), 0);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        if (label.font != reference.font) label.font = reference.font;
        if (label.fontSharedMaterial != reference.fontSharedMaterial) label.fontSharedMaterial = reference.fontSharedMaterial;
        label.fontSizeMax = reference.fontSize;
        label.fontSizeMin = Math.Min(18f, reference.fontSize);
        label.fontStyle = reference.fontStyle;
        label.alignment = reference.alignment;
        var color = reference.color;
        color.a = 1;
        label.color = color;
        label.margin = reference.margin;
        return true;
    }

    internal void Hide()
    {
        if (_object != null && _visible) _object.SetActive(false);
        _visible = false;
        _shownId = 0;
    }

    private void ReleaseLabel()
    {
        Hide();
        if (_object != null) UObject.Destroy(_object);
        _object = null;
        _canvas = null;
        _label = _reference = null;
    }

    internal void Fault(Exception error)
    {
        if (_faulted) return;
        _faulted = true;
        ReleaseLabel();
        _plugin.Log.LogError($"Idol hover hints disabled after an error; gameplay remains active: {error}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RestoreTestMessage();
        ReleaseLabel();
        _ui = null;
    }
}

[HarmonyPatch(typeof(ScoreTool), nameof(ScoreTool.InteractableName), MethodType.Getter)]
internal static class IdolNamePatch
{
    [HarmonyPostfix]
    private static void Postfix(ScoreTool __instance, ref string __result)
    {
        try
        {
            if (IdolHoverInfo.HintsHidden(__instance)) { __result = ""; return; }
            if (!IdolHoverInfo.TryText(__instance, out var text)) return;
            if (CalypsoExpansionSetPlugin.Instance!.ShowIndividualIdolNames)
            {
                if (!string.IsNullOrEmpty(text.DisplayName)) __result = text.DisplayName;
            }
            else if (text.IsMod) __result = IdolHoverInfo.GenericIdolName;
        }
        catch (Exception error) { CalypsoExpansionSetPlugin.Instance?.HoverInfo?.Fault(error); }
    }
}

[HarmonyPatch(typeof(GameplayElement), nameof(GameplayElement.DisplayInteractibleName), MethodType.Getter)]
internal static class IdolNameVisibilityPatch
{
    [HarmonyPostfix]
    private static void Postfix(GameplayElement __instance, ref bool __result)
    {
        try
        {
            var idol = __instance.TryCast<ScoreTool>();
            if (IdolHoverInfo.HintsHidden(idol)) { __result = false; return; }
            if (CalypsoExpansionSetPlugin.Instance?.ShowIndividualIdolNames != true) return;
            if (IdolHoverInfo.TryText(idol, out var text) && !text.IsMod
                && !string.IsNullOrEmpty(text.DisplayName)) __result = true;
        }
        catch (Exception error) { CalypsoExpansionSetPlugin.Instance?.HoverInfo?.Fault(error); }
    }
}

[HarmonyPatch(typeof(PlayerInteraction), "LateUpdate")]
internal static class IdolHoverUpdatePatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerInteraction __instance)
        => CalypsoExpansionSetPlugin.Instance?.HoverInfo?.UpdateForInteraction(__instance);
}

[HarmonyPatch(typeof(MessageUi), nameof(MessageUi.Show))]
internal static class IdolHintYieldMessagePatch
{
    [HarmonyPrefix]
    private static void Prefix(MessageUi __instance) => CalypsoExpansionSetPlugin.Instance?.HoverInfo?.NativeMessageStarting(__instance);
}

[HarmonyPatch(typeof(MessageUi), nameof(MessageUi.ShowForDuration))]
internal static class IdolHintYieldTimedMessagePatch
{
    [HarmonyPrefix]
    private static void Prefix(MessageUi __instance) => CalypsoExpansionSetPlugin.Instance?.HoverInfo?.NativeMessageStarting(__instance);
}
