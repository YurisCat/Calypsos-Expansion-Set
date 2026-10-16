using BepInEx;
using Entities.Player;
using Entities.Player.Behaviours;
using EnvironmentElements;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using User;
using _200_Dev.Entities;
using _200_Dev.Entities.Player.Behaviours.Inventory;
using _200_Dev.Entities.Player.Behaviours.Camera;

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
    private readonly LobbyBoxInspection _inspection;
    private PlayerInteraction? _releaseOwner;
    private bool _faulted;
    private float _nextCheck;
    private readonly string _placementPath = Path.Combine(Paths.ConfigPath, "CalypsoExpansionSet", "lobby-box-placement.json");
    private bool _usingDebugPlacement;
    internal Transform? ModelTransform => _model?.Root != null ? _model.Root.transform : null;
    internal string PlacementPath => _placementPath;
    internal bool Ready => ModelTransform != null;
    internal LobbyBoxRuntime(CalypsoExpansionSetPlugin plugin)
    { _plugin = plugin; Furniture = new LobbyFurniturePreview(plugin); _inspection = new LobbyBoxInspection(plugin); }

    internal void Tick()
    {
        if (_faulted) return;
        try
        {
            if (InputsManager.Instance?.onInteract?.enabled != true || EntityManager.Me?.PlayerInteraction != _releaseOwner)
                _releaseOwner = null;
            _inspection.Tick(_model);
            if (!_inspection.CanClose) _interactionUi.HideReadingPrompt();
            if (!_inspection.Active && !Controls(out _, out _)) _interactionUi.Hide();
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
            && player.PlayerInteraction.isActiveAndEnabled && player.PlayerInteraction.isOwned && player.PlayerInteraction.CanInteract
            && _plugin.LocalGameplayReady(player) && !_plugin.RuntimeFailed
            && !Testing.TestRoomAppearanceView.IsInspecting
            && CameraManager.Instance?.LocalAnimatorState is CameraManager.ECameraAnimatorState.PLAYER_POV_STANDUP
                or CameraManager.ECameraAnimatorState.PLAYER_POV_CROUCH;
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
            if (_inspection.Active)
            {
                _inspection.Tick(_model);
                if (_inspection.Active)
                {
                    if (interaction.Hovered != null) interaction.ResetHover();
                    var reader = EntityManager.Me;
                    var output = SetFovShakeData.Instance?._cam;
                    if (_inspection.CanClose && reader?.InteractionUI != null && output != null)
                        _interactionUi.Show(reader.InteractionUI, output, InteractionPoint, true, true);
                    else _interactionUi.Hide();
                    return;
                }
            }
            if (!Controls(out var player, out var camera) || player.InteractionUI == null)
            { _interactionUi.Hide(); return; }
            if (!PointVisible(camera, interaction, Mathf.Clamp(interaction._maxDistanceByDefault, 0.5f, 4f)))
            { _interactionUi.Hide(); return; }
            var focused = AimedAtBox(camera, interaction);
            if (focused && interaction.Hovered != null) interaction.ResetHover();
            _interactionUi.Show(player.InteractionUI, camera, InteractionPoint, focused, false);
        }
        catch (Exception error) { Fault(error); }
    }

    internal bool Interact(PlayerInteraction interaction)
    {
        if (_faulted || EntityManager.Me?.PlayerInteraction != interaction) return false;
        try
        {
            if (_inspection.Active)
            {
                _inspection.Tick(_model);
                if (!_inspection.Active) return false;
                // Consume entry-time presses without closing or forwarding them to the world.
                _inspection.RequestClose(); _interactionUi.Hide(); _releaseOwner = interaction;
                return true;
            }
            if (!Controls(out var player, out var camera)) return false;
            bool focused;
            try
            {
                // Match the temporary camera used by the native hover pass, then release it before reading.
                Visuals.HeldCameraOverrides.BeginInteraction(camera);
                focused = AimedAtBox(camera, interaction);
            }
            finally { Visuals.HeldCameraOverrides.End(camera); }
            if (!focused) return false;
            _inspection.Open(player, camera, _model!);
            _releaseOwner = interaction;
            _interactionUi.Hide();
            _interactionUi.RefreshText();
            if (_plugin.DebugTools.Enabled) _plugin.Log.LogInfo($"[Lobby box] inspection opened={_inspection.Active} locally.");
            return true;
        }
        catch (Exception error) { Fault(error); return true; }
    }

    internal void AfterCameraChange(CameraManager manager, bool accepted)
    {
        try { _inspection.AfterCameraChange(manager, accepted); }
        catch (Exception error) { Fault(error); }
    }

    internal void BeforeCameraTargetChange(CameraManager manager)
    {
        try { _inspection.BeforeCameraTargetChange(manager); }
        catch (Exception error) { Fault(error); }
    }

    internal void BeforeMovementChange(PlayerEntity player)
    {
        try { _inspection.BeforeMovementChange(player); }
        catch (Exception error) { Fault(error); }
    }

    internal bool BlocksInventoryScroll(InventoryNew inventory) => _inspection.Active && EntityManager.Me?.InventoryNew == inventory;

    internal bool ConsumeRelease(PlayerInteraction interaction)
    {
        if (_releaseOwner != interaction) return false;
        _releaseOwner = null;
        return true;
    }

    internal void Apply(LobbyBoxPlacement data)
    {
        data.Validate();
        Close();
        var transform = ModelTransform ?? throw new InvalidOperationException("The lobby box is not ready.");
        transform.localPosition = Vec(data.Position);
        transform.localRotation = Quaternion.Euler(Vec(data.Rotation));
        transform.localScale = Vec(data.Scale);
        Lighting?.SetBrightness(data.LightBrightness);
        Lighting?.SetPreset(data.LightPreset);
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

    private void Close() { _inspection.Close(); _interactionUi.Hide(); }
    private void ClearScene()
    {
        Close(); _releaseOwner = null;
        _inspection.Dispose();
        _interactionUi.Dispose();
        Lighting?.Dispose(); Lighting = null;
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

// The wheel belongs to the local reading session while open; leave other inventories and sessions untouched.
[HarmonyPatch(typeof(InventoryNew), nameof(InventoryNew.CliOnFocusPreviousInput))]
internal static class LobbyBoxPreviousItemPatch
{
    private static bool Prefix(InventoryNew __instance) => CalypsoExpansionSetPlugin.Instance?.LobbyBox?.BlocksInventoryScroll(__instance) != true;
}

[HarmonyPatch(typeof(InventoryNew), nameof(InventoryNew.CliOnFocusNextInput))]
internal static class LobbyBoxNextItemPatch
{
    private static bool Prefix(InventoryNew __instance) => CalypsoExpansionSetPlugin.Instance?.LobbyBox?.BlocksInventoryScroll(__instance) != true;
}
