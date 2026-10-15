using BepInEx;
using Entities.Player;
using Entities.Player.Behaviours;
using EnvironmentElements;
using HarmonyLib;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using User;
using _200_Dev.Entities;
using _200_Dev.UI.Networking;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Lobby;

// Each client owns its decoration and reading state. No spawned network identity or gameplay state.
internal sealed class LobbyBoxRuntime : IDisposable
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    internal LobbyFurniturePreview Furniture { get; }
    internal LobbyBoxLighting? Lighting { get; private set; }
    private readonly LobbyBoxInteractionUi _interactionUi = new();
    private LobbyBoxModel? _model;
    private Transform? _anchor;
    private PlayerEntity? _reader;
    private GameObject? _canvasObject;
    private TextMeshProUGUI? _text;
    private Image? _background;
    private bool _open, _focused, _consumeRelease, _faulted;
    private float _nextCheck, _nextText;
    private string _cachedText = "";
    private readonly string _placementPath = Path.Combine(Paths.ConfigPath, "CalypsoExpansionSet", "lobby-box-placement.json");
    private bool _usingDebugPlacement;
    internal Transform? ModelTransform => _model?.Root != null ? _model.Root.transform : null;
    internal string PlacementPath => _placementPath;
    internal bool Ready => ModelTransform != null;
    internal LobbyBoxRuntime(CalypsoExpansionSetPlugin plugin)
    { _plugin = plugin; Furniture = new LobbyFurniturePreview(plugin); }

    internal void Tick()
    {
        if (_faulted) return;
        try
        {
            Furniture.Tick();
            Lighting?.Maintenance();
            Lighting?.Sync();
            if (Time.realtimeSinceStartup < _nextCheck) return;
            _nextCheck = Time.realtimeSinceStartup + 0.5f;
            var start = NetworkClient.ready ? Room.GetStartRoom() : null;
            var anchor = start != null ? start.transform : null;
            if (_anchor != anchor || (_model != null && _model.Root == null)) ClearScene();
            if (anchor == null || !_plugin.Registry.Ready) return;
            if (_model == null)
            {
                var prefab = _plugin.Registry.Entries.First().Data?.prefab;
                var renderer = prefab != null ? prefab.transform.Find("Dice/SM_IdoleBody/SM_IdolHead")?.GetComponent<Renderer>() : null;
                if (renderer == null || renderer.sharedMaterial == null) return;
                _anchor = anchor;
                _model = new LobbyBoxModel(anchor, renderer.sharedMaterial);
                Lighting = new LobbyBoxLighting(_plugin, _model.Root.transform, start!, _model.LocalBounds);
                Apply(new LobbyBoxPlacement());
                if (_plugin.DebugTools.Enabled && File.Exists(_placementPath))
                {
                    try { LoadPlacement(); }
                    catch (Exception error) { _plugin.Log.LogWarning("[Lobby placement] keeping default: " + error.Message); }
                }
                _plugin.Log.LogInfo("[Lobby box] local decoration ready; final placement is pending authoring.");
            }
            if (_usingDebugPlacement && !_plugin.DebugTools.Enabled)
            { Apply(new LobbyBoxPlacement()); _usingDebugPlacement = false; }
        }
        catch (Exception error) { Fault(error); }
    }

    private bool Controls(out PlayerEntity player, out Camera camera)
    {
        player = EntityManager.Me!;
        camera = SetFovShakeData.Instance?._cam!;
        return !_faulted && Ready && NetworkClient.ready && player != null && camera != null
            && camera.isActiveAndEnabled && player.CurrentHealthSync > 0 && player.PlayerInteraction != null
            && player.PlayerInteraction.isOwned && player.PlayerInteraction.CanInteract
            && _plugin.LocalGameplayReady(player) && !_plugin.RuntimeFailed;
    }

    private Vector3 InteractionPoint => _model!.Root.transform.TransformPoint(new Vector3(
        _model.LocalBounds.center.x, _model.LocalBounds.max.y + 0.035f, _model.LocalBounds.center.z));

    private bool PointVisible(Camera camera, PlayerInteraction interaction, float range)
    {
        if (_model == null) return false;
        var delta = InteractionPoint - camera.transform.position;
        var distance = delta.magnitude;
        if (distance < 0.001f || distance > range) return false;
        var mask = (int)interaction._occlusionLayerMask | (int)interaction._interactableLayerMask;
        return !Physics.Raycast(new Ray(camera.transform.position, delta / distance),
            Mathf.Max(0f, distance - 0.015f), mask, QueryTriggerInteraction.Ignore);
    }

    private bool AimedAtBox(Camera camera, PlayerInteraction interaction)
    {
        if (_model == null) return false;
        var range = Mathf.Clamp(interaction._maxDistanceByDefault, 0.5f, 4f);
        var delta = InteractionPoint - camera.transform.position;
        var forward = Vector3.Dot(delta, camera.transform.forward);
        var perpendicular = Mathf.Max(0, delta.sqrMagnitude - forward * forward);
        return LobbyBoxTuning.PointInAim(forward, perpendicular, range, 0.12f)
            && PointVisible(camera, interaction, range);
    }

    internal void LateUpdate(PlayerInteraction interaction)
    {
        if (_faulted || EntityManager.Me?.PlayerInteraction != interaction) return;
        try
        {
            if (!Controls(out var player, out var camera)) { Close(); return; }
            if (player.InteractionUI == null) { Close(); return; }
            if (_reader != null && _reader != player) Close();
            if (!PointVisible(camera, interaction, _open ? 6f : Mathf.Clamp(interaction._maxDistanceByDefault, 0.5f, 4f)))
            { Close(); return; }
            _focused = AimedAtBox(camera, interaction);
            if (_focused && interaction.Hovered != null) interaction.ResetHover();
            _interactionUi.Show(player.InteractionUI, camera, InteractionPoint, _focused, _open);
            if (_open) Show(camera); else HideIntroduction();
        }
        catch (Exception error) { Fault(error); }
    }

    internal bool Interact(PlayerInteraction interaction)
    {
        if (_faulted || EntityManager.Me?.PlayerInteraction != interaction) return false;
        try
        {
            if (!Controls(out var player, out var camera)) return false;
            if (!PointVisible(camera, interaction, _open ? 6f : Mathf.Clamp(interaction._maxDistanceByDefault, 0.5f, 4f)))
            { Close(); return false; }
            if (!_open && !AimedAtBox(camera, interaction)) return false;
            _open = !_open;
            _reader = _open ? player : null;
            _consumeRelease = true;
            _nextText = 0;
            _interactionUi.RefreshText();
            if (_plugin.DebugTools.Enabled) _plugin.Log.LogInfo($"[Lobby box] introduction {(_open ? "opened" : "closed")} locally.");
            return true;
        }
        catch (Exception error) { Fault(error); return false; }
    }

    internal bool ConsumeRelease(PlayerInteraction interaction)
    {
        if (EntityManager.Me?.PlayerInteraction != interaction || !_consumeRelease) return false;
        _consumeRelease = false;
        return true;
    }

    private void Show(Camera camera)
    {
        var reference = UIManager.Instance?.globalMessageUi?.text;
        if (reference == null || reference.font == null) { Hide(); return; }
        if (_canvasObject == null)
        {
            _canvasObject = new GameObject("Calypso Box Introduction") { hideFlags = HideFlags.DontSave, layer = 0 };
            _canvasObject.SetActive(false);
            _canvasObject.transform.SetParent(_anchor, false);
            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = _canvasObject.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(700, 620);
            _background = _canvasObject.AddComponent<Image>();
            _background.color = new Color(0.025f, 0.025f, 0.035f, 0.88f);
            _background.raycastTarget = false;
            var labelObject = new GameObject("Text") { hideFlags = HideFlags.DontSave };
            labelObject.transform.SetParent(_canvasObject.transform, false);
            _text = labelObject.AddComponent<TextMeshProUGUI>();
            _text.raycastTarget = false; _text.richText = false; _text.maskable = false;
            _text.enableAutoSizing = true; _text.fontSizeMax = 32; _text.fontSizeMin = 23;
            _text.overflowMode = TextOverflowModes.Overflow;
            var textRect = _text.rectTransform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(22, 18); textRect.offsetMax = new Vector2(-22, -18);
        }
        if (_text!.font != reference.font) _text.font = reference.font;
        _text.color = Color.white;
        _text.alignment = TextAlignmentOptions.TopLeft;
        var layout = _canvasObject.GetComponent<RectTransform>();
        layout.sizeDelta = new Vector2(700, 620);
        var modelTransform = _model!.Root.transform;
        var localBounds = _model.LocalBounds;
        var worldBounds = new Bounds(modelTransform.TransformPoint(localBounds.center), Vector3.zero);
        for (var corner = 0; corner < 8; corner++)
            worldBounds.Encapsulate(modelTransform.TransformPoint(localBounds.center + new Vector3(
                (corner & 1) == 0 ? -localBounds.extents.x : localBounds.extents.x,
                (corner & 2) == 0 ? -localBounds.extents.y : localBounds.extents.y,
                (corner & 4) == 0 ? -localBounds.extents.z : localBounds.extents.z)));
        var coverNormal = modelTransform.up.normalized;
        var coverFace = modelTransform.TransformPoint(new Vector3(localBounds.center.x, localBounds.max.y, localBounds.center.z));
        var aboveBox = new Vector3(worldBounds.center.x, worldBounds.max.y + 0.08f, worldBounds.center.z);
        const float textScale = 0.0022f;
        var outward = LobbyBoxTuning.PanelOutwardOffset(Vector3.Dot(aboveBox - coverFace, coverNormal),
            Vector3.Dot(camera.transform.right, coverNormal), Vector3.Dot(camera.transform.up, coverNormal),
            layout.sizeDelta.x * textScale, layout.sizeDelta.y * textScale);
        _canvasObject.transform.position = aboveBox + coverNormal * outward;
        _canvasObject.transform.rotation = camera.transform.rotation;
        // Keep panel size independent from room-root scaling.
        var parentScale = _anchor!.lossyScale;
        _canvasObject.transform.localScale = new Vector3(textScale / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
            textScale / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), textScale / Mathf.Max(0.001f, Mathf.Abs(parentScale.z)));
        if (Time.realtimeSinceStartup >= _nextText)
        {
            _nextText = Time.realtimeSinceStartup + 0.25f;
            var text = LobbyBoxText.Introduction;
            if (_cachedText != text) { _text.text = text; _cachedText = text; }
        }
        if (!_canvasObject.activeSelf) _canvasObject.SetActive(true);
    }

    internal void Apply(LobbyBoxPlacement data)
    {
        data.Validate();
        var transform = ModelTransform ?? throw new InvalidOperationException("The lobby box is not ready.");
        transform.localPosition = Vec(data.Position);
        transform.localRotation = Quaternion.Euler(Vec(data.Rotation));
        transform.localScale = Vec(data.Scale);
        Lighting?.SetBrightness(data.LightBrightness);
        Lighting?.SetPreset(data.LightPreset);
        _nextText = 0;
    }

    internal void Edit(LobbyBoxPlacement data)
    {
        if (!_plugin.DebugTools.Enabled) throw new InvalidOperationException("DebugMode is required.");
        Apply(data); _usingDebugPlacement = true;
    }

    private static Vector3 Vec(float[] value) => new(value[0], value[1], value[2]);
    private static float[] Values(Vector3 value) => new[] { value.x, value.y, value.z };
    internal LobbyBoxPlacement Capture()
    {
        var transform = ModelTransform ?? throw new InvalidOperationException("The lobby box is not ready.");
        return new LobbyBoxPlacement
        {
            Scene = _anchor!.gameObject.scene.name, Anchor = _anchor.name,
            SavedAt = DateTimeOffset.UtcNow.ToString("O"), Position = Values(transform.localPosition),
            Rotation = Values(transform.localEulerAngles), Scale = Values(transform.localScale),
            WorldPosition = Values(transform.position), LightPreset = Lighting?.Preset ?? 1,
            LightBrightness = Lighting?.Brightness ?? 1f
        };
    }

    internal void SavePlacement()
    {
        if (!_plugin.DebugTools.Enabled) throw new InvalidOperationException("DebugMode is required.");
        var data = Capture(); var json = data.Serialize();
        Directory.CreateDirectory(Path.GetDirectoryName(_placementPath)!);
        // Keep each saved revision as evidence; atomically replace the current authoring preset.
        var archive = Path.Combine(Path.GetDirectoryName(_placementPath)!, $"lobby-box-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}.json");
        File.WriteAllText(archive, json);
        var temporary = _placementPath + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, _placementPath, true);
        _usingDebugPlacement = true;
        _plugin.DebugTools.Report(LobbyBoxText.Pick("摆放已保存：", "Placement saved: ") + _placementPath);
        _plugin.Log.LogInfo("[Lobby placement] " + json.Replace("\r", "").Replace("\n", " "));
    }

    internal void LoadPlacement()
    {
        if (!_plugin.DebugTools.Enabled) return;
        var file = new FileInfo(_placementPath);
        if (!file.Exists) throw new FileNotFoundException("No saved lobby placement.", _placementPath);
        if (file.Length > 16384) throw new InvalidDataException("Placement file is too large.");
        var data = LobbyBoxPlacement.Parse(File.ReadAllText(_placementPath));
        if (data.Anchor != _anchor!.name || data.Scene != _anchor.gameObject.scene.name)
            throw new InvalidDataException("Placement scene/anchor differs from this lobby; move the box and save a new placement.");
        Apply(data); _usingDebugPlacement = true;
    }

    internal void BringToPlayer()
    {
        if (!_plugin.DebugTools.Enabled || _anchor == null || EntityManager.Me == null) return;
        var player = EntityManager.Me;
        var forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        var world = player.transform.position + forward * 1.3f;
        // Authoring command intentionally allows a new provisional point; final placement remains manual.
        if (Physics.Raycast(world + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~4, QueryTriggerInteraction.Ignore))
            world = hit.point + Vector3.up * 0.02f;
        var data = Capture();
        data.Position = Values(_anchor.InverseTransformPoint(world));
        Apply(data); _usingDebugPlacement = true;
    }

    private void HideIntroduction() { if (_canvasObject != null) _canvasObject.SetActive(false); }
    private void Hide() { HideIntroduction(); _interactionUi.Hide(); }
    private void Close() { _open = _focused = false; _reader = null; Hide(); }
    private void ClearScene()
    {
        Close(); _consumeRelease = false;
        _interactionUi.Dispose();
        Lighting?.Dispose(); Lighting = null;
        if (_canvasObject != null) UObject.Destroy(_canvasObject);
        _canvasObject = null; _text = null; _background = null; _cachedText = "";
        _model?.Dispose(); _model = null; _anchor = null; _usingDebugPlacement = false;
    }
    private void Fault(Exception error)
    {
        if (_faulted) return;
        _faulted = true;
        ClearScene();
        Furniture.Dispose();
        _plugin.Log.LogError($"[Lobby box] disabled after an error; restart after fixing it.\n{error}");
    }
    public void Dispose() { _faulted = true; ClearScene(); Furniture.Dispose(); }
}

[HarmonyPatch(typeof(PlayerInteraction), "LateUpdate")]
internal static class LobbyBoxHoverPatch
{
    private static void Postfix(PlayerInteraction __instance) => CalypsoExpansionSetPlugin.Instance?.LobbyBox?.LateUpdate(__instance);
}

[HarmonyPatch(typeof(PlayerInteraction), "OnInteract_TryInteractWith")]
internal static class LobbyBoxInteractPatch
{
    // Native server-approved retries call this method with an empty context.
    // Only a real performed input may toggle this local decoration.
    private static bool Prefix(PlayerInteraction __instance, InputAction.CallbackContext obj) =>
        !obj.performed || CalypsoExpansionSetPlugin.Instance?.LobbyBox?.Interact(__instance) != true;
}

[HarmonyPatch(typeof(PlayerInteraction), "OnInteract_TryReleaseInteractWith")]
internal static class LobbyBoxReleasePatch
{
    private static bool Prefix(PlayerInteraction __instance) => CalypsoExpansionSetPlugin.Instance?.LobbyBox?.ConsumeRelease(__instance) != true;
}
