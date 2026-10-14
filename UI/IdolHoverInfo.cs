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
    private readonly CalypsoExpansionSetPlugin _plugin;
    private UIManager? _ui;
    private TextMeshProUGUI? _reference, _label;
    private GameObject? _object;
    private bool _disposed, _faulted, _visible;
    private uint _shownId;
    private float _nativeMessageUntil;

    internal IdolHoverInfo(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    internal static bool TryDefinition(ScoreTool? idol, out IdolDefinition definition)
    {
        definition = null!;
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (idol == null || idol.SoItem == null || plugin == null || plugin.RuntimeFailed || !plugin.Registry.Ready
            || !plugin.Registry.TryGet(idol.SoItem.Id, out var entry)
            || string.IsNullOrEmpty(entry.Definition.ShortDescription)) return false;
        definition = entry.Definition;
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
        if (_disposed || _faulted) return;
        try
        {
            var ui = UIManager.Instance;
            if (ui != _ui || !NetworkClient.isConnected)
            {
                ReleaseLabel();
                _nativeMessageUntil = 0;
                _ui = ui;
            }
            if (!ControlsReady(out _) || NativeMessageVisible(ui)) Hide();
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
            if (!ControlsReady(out _)) { Hide(); return; }
            var target = interaction.Hovered;
            // Native collider targets can be InterectableLinker wrappers.
            // Resolve through the game's interface before identifying the item.
            var real = target != null ? target.GetRealInteractable() : null;
            var idol = real != null ? real.TryCast<ScoreTool>() : null;
            if (!TryDefinition(idol, out var definition)) { Hide(); return; }
            var ui = UIManager.Instance;
            if (ui == null || ui.rootUI == null || NativeMessageVisible(ui)) { Hide(); return; }
            var reference = ui.globalMessageUi != null ? ui.globalMessageUi.text : null;
            if (reference == null || reference.font == null) { Hide(); return; }
            EnsureLabel(ui, reference);
            if (!Layout(reference, ui.rootUI.transform)) { Hide(); return; }
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

    private bool NativeMessageVisible(UIManager? ui) =>
        Time.realtimeSinceStartup < _nativeMessageUntil
        || ui != null && (ui.globalMessageUi != null && ui.globalMessageUi.IsDisplayed
            || ui.messageUi != null && ui.messageUi.IsDisplayed
            || ui._messageQueue != null && ui._messageQueue.Count != 0
            || ui.narrationBackground != null && ui.narrationBackground.alpha > 0.001f);

    internal void NativeMessageStarting(MessageUi message)
    {
        if (_disposed || _faulted) return;
        try
        {
            var ui = UIManager.Instance;
            if (ui == null || message != ui.globalMessageUi && message != ui.messageUi) return;
            // A show tween can still have alpha=0 in its first frame. Yield
            // before it starts; thereafter the actual native visibility wins.
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
        _object.transform.SetParent(ui.rootUI.transform, false);
        _label = _object.AddComponent<TextMeshProUGUI>();
        _label.raycastTarget = false;
        _label.richText = false;
        _label.enableAutoSizing = true;
        _label.overflowMode = TextOverflowModes.Ellipsis;
        var rect = _label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
    }

    private bool Layout(TextMeshProUGUI reference, Transform root)
    {
        var label = _label!;
        var source = reference.rectTransform;
        var rect = label.rectTransform;
        // Copy the native top-message text's world layout into our independent
        // root child. It must not inherit the native message's hidden CanvasGroup.
        var sourceScale = source.lossyScale;
        var parentScale = root.lossyScale;
        if (parentScale.x == 0 || parentScale.y == 0 || parentScale.z == 0
            || source.rect.width <= 0 || source.rect.height <= 0) return false;
        rect.pivot = source.pivot;
        rect.sizeDelta = source.rect.size;
        rect.position = source.position;
        rect.rotation = source.rotation;
        rect.localScale = new Vector3(sourceScale.x / parentScale.x, sourceScale.y / parentScale.y,
            sourceScale.z / parentScale.z);
        if (label.font != reference.font) label.font = reference.font;
        if (label.fontSharedMaterial != reference.fontSharedMaterial) label.fontSharedMaterial = reference.fontSharedMaterial;
        label.fontSizeMax = reference.fontSize;
        label.fontSizeMin = Math.Min(18f, reference.fontSize);
        label.fontStyle = reference.fontStyle;
        label.alignment = reference.alignment;
        label.color = reference.color;
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
            if (IdolHoverInfo.TryDefinition(__instance, out var definition)) __result = definition.DisplayName;
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
