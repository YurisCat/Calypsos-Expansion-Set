using Cinemachine;
using Entities;
using Entities.Player;
using Entities.Player.Crosshair;
using HarmonyLib;
using Items;
using Mirror;
using UnityEngine;
using User;
using _200_Dev.Entities;
using _200_Dev.Entities.Interfaces;
using _200_Dev.Entities.Player.Behaviours.Camera;
using State = _200_Dev.Entities.Player.Behaviours.Camera.CameraManager.ECameraAnimatorState;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Lobby;

// Owns one local reading session; the model remains a non-networked decoration.
internal sealed class LobbyBoxInspection : IDisposable
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly LobbyBoxReadingUi _ui = new();
    private PlayerEntity? _player;
    private CameraManager? _manager;
    private Camera? _output;
    private CinemachineVirtualCamera? _virtual;
    private Transform? _oldFollow;
    private Quaternion _oldRotation;
    private State _oldState;
    private GameObject? _slot;
    private Behaviour? _movement, _look;
    private PlayerLocomotionController? _locomotion;
    private bool _crouchLocked, _jumpLocked, _frozen, _nativeWrite;
    private PlayerCrosshairUI? _crosshair;
    private bool _crosshairVisible;
    private Tool? _tool;
    private bool _usageAllowed;
    private Vector3 _playerPosition;
    private int _width, _height;
    private Il2CppSystem.Action<Entity, int, IDamageDealer.EDamgeSource>? _damaged;
    private Il2CppSystem.Action<Entity, int, IDamageDealer.EHealingSource>? _healed;
    private Il2CppSystem.Action<PlayerEntity>? _teleported;
    private Il2CppSystem.Action<PlayerEntity>? _stateChanged;
    private Il2CppSystem.Action<GameEventData>? _gameEvent;
    private PlayerLocomotionEsm? _locomotionState;
    private PlayerLifeCycleEsm? _lifeState;
    private sealed record CameraReturn(CinemachineVirtualCamera Camera, ICinemachineCamera NativeCamera, Transform? Follow,
        Quaternion Rotation, GameObject Slot, Transform Target, int Frame);
    private CameraReturn? _returning;
    private bool _settled;
    private int _entryRevision;
    private float _entryDeadline;
    private Il2CppSystem.Action<bool>? _cameraSettled;
    private PlayerEntity? _fadingPlayer;
    internal bool Active => _player is not null;
    internal bool CanClose => Active && _settled;

    internal LobbyBoxInspection(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    internal bool Open(PlayerEntity player, Camera output, LobbyBoxModel model)
    {
        FinishCameraReturn();
        var manager = CameraManager.Instance;
        var camera = manager?.InspectItemCamera;
        if (Active || manager == null || camera == null || (_returning != null && _returning.Camera != camera)
            || Testing.TestRoomAppearanceView.IsInspecting
            || player.playerCameraController?.isActiveAndEnabled != true
            || manager.LocalAnimatorState is not (State.PLAYER_POV_STANDUP or State.PLAYER_POV_CROUCH)) return false;
        try
        {
            if (!_ui.Create(player)) { _ui.Dispose(); return false; }
            _fadingPlayer = null;
            Visuals.HeldCameraOverrides.End();
            _player = player; _manager = manager; _virtual = camera; _output = output;
            _settled = false;
            _entryDeadline = Time.realtimeSinceStartup + 5;
            var revision = ++_entryRevision;
            _cameraSettled = (Action<bool>)(completed =>
            {
                if (!Active || revision != _entryRevision) return;
                if (completed) _settled = true;
                else Close();
            });
            _oldState = manager.LocalAnimatorState;
            _oldFollow = _returning != null ? _returning.Follow : camera.Follow;
            _oldRotation = _returning != null ? _returning.Rotation : camera.transform.rotation;
            _playerPosition = player.transform.position;
            _slot = _returning?.Slot ?? new GameObject("Calypso Box Inspect Camera Slot") { hideFlags = HideFlags.DontSave };
            _returning = null;
            Frame(model);
            _movement = player.PlayerMovement; _look = player.playerCameraController; _locomotion = player.LocomotionController;
            _crouchLocked = _locomotion._crouchLocked; _jumpLocked = _locomotion._jumpLocked;
            _crosshair = player.GetComponent<PlayerCrosshair>()?._crosshair;
            _crosshairVisible = _crosshair != null && _crosshair._requestedVisible;
            _tool = player.InventoryNew?.InHand; _usageAllowed = _tool != null && _tool.UsageAllowed;
            Subscribe(player);
            _nativeWrite = true;
            try
            {
                _frozen = true;
                player.EnabledMovement(false);
                if (!Active) return false;
                manager.CliInitInspectItemCamera(_slot.transform);
                if (!Active) return false;
                if (!manager.CliSetLocalCameraState(State.INSPECT_ITEM, onStateChangeComplete: _cameraSettled)) { Close(false); return false; }
                if (!Active) return false;
            }
            finally { _nativeWrite = false; }
            if (_crosshair != null) _crosshair.Enabled(false);
            if (_tool != null && _usageAllowed)
            {
                var tool = _tool;
                tool.CmdToggle(false);
                if (!Active) return false;
                tool.UsageAllowed = false;
            }
            return true;
        }
        catch { Close(false); throw; }
    }

    private void Frame(LobbyBoxModel model)
    {
        var transform = model.Root.transform;
        var center = transform.TransformPoint(model.LocalBounds.center);
        var top = transform.TransformVector(Vector3.forward).normalized;
        var normal = Vector3.Cross(top, transform.TransformVector(Vector3.right)).normalized;
        var rotation = Quaternion.LookRotation(-normal, top);
        var inverse = Quaternion.Inverse(rotation);
        var extents = Vector3.zero;
        var bounds = model.LocalBounds;
        for (var i = 0; i < 8; i++)
        {
            var corner = transform.TransformPoint(bounds.center + new Vector3(
                (i & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                (i & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                (i & 4) == 0 ? -bounds.extents.z : bounds.extents.z));
            var local = inverse * (corner - center);
            extents = Vector3.Max(extents, new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z)));
        }
        var lens = _virtual!.m_Lens;
        var frame = LobbyBoxFraming.Calculate(extents.x, extents.y, extents.z, lens.FieldOfView, _output!.aspect, lens.NearClipPlane);
        _slot!.transform.SetPositionAndRotation(center + normal * frame.Distance - rotation * Vector3.right * frame.LeftOffset, rotation);
        _width = Screen.width; _height = Screen.height;
    }

    internal void Tick(LobbyBoxModel? model)
    {
        FinishCameraReturn();
        if (!Active)
        {
            if (!NetworkClient.ready || _plugin.RuntimeFailed || model?.Root == null
                || _fadingPlayer == null || EntityManager.Me != _fadingPlayer || !_ui.Ready)
                _ui.Dispose();
            else _ui.Tick();
            if (!_ui.Ready) _fadingPlayer = null;
            return;
        }
        var player = _player!;
        if (player == null || model?.Root == null || !NetworkClient.ready || EntityManager.Me != player || !_ui.Ready)
        { Close(false); return; }
        var interaction = player.PlayerInteraction;
        var inputs = InputsManager.Instance;
        // Movement is intentionally disabled here. Do not use LocalGameplayReady.
        if (player.CurrentHealthSync <= 0
            || interaction == null || !interaction.isActiveAndEnabled || !interaction.isOwned || !interaction.CanInteract
            || _plugin.RuntimeFailed || _plugin.DebugTools.PanelOpen || Time.timeScale <= 0
            || (!_settled && Time.realtimeSinceStartup >= _entryDeadline)
            || inputs?.onInteract?.enabled != true || inputs.onJump?.enabled != true
            || _manager == null || CameraManager.Instance != _manager || _manager.LocalAnimatorState != State.INSPECT_ITEM
            || _virtual == null || _slot == null || _virtual.Follow != _slot.transform
            || _output == null || !_output.isActiveAndEnabled || SetFovShakeData.Instance?._cam != _output
            || _movement == null || _movement.enabled || _look == null || _look.enabled || _locomotion == null || _locomotion.enabled
            || player.PlayerMovement != _movement || player.playerCameraController != _look || player.LocomotionController != _locomotion
            || (player.transform.position - _playerPosition).sqrMagnitude > 0.25f
            || player.InventoryNew?.InHand != _tool || Testing.TestRoomAppearanceView.IsInspecting)
        { Close(); return; }
        if (_width != Screen.width || _height != Screen.height) Frame(model);
        _ui.Tick();
    }

    private void Subscribe(PlayerEntity player)
    {
        _damaged = (Action<Entity, int, IDamageDealer.EDamgeSource>)((_, _, _) => Close());
        _healed = (Action<Entity, int, IDamageDealer.EHealingSource>)((_, _, _) => Close());
        _teleported = (Action<PlayerEntity>)(_ => Close());
        _stateChanged = (Action<PlayerEntity>)(_ => { if (!_nativeWrite) Close(); });
        _gameEvent = (Action<GameEventData>)(_ => Close());
        _locomotionState = player.EntityStatesRunner?.GetLocomotionEsm()?.TryCast<PlayerLocomotionEsm>();
        _lifeState = player.EntityStatesRunner?.GetLifeCycleEsm()?.TryCast<PlayerLifeCycleEsm>();
        player.add_CliOnDamagedEvent(_damaged);
        player.add_CliOnHealedEvent(_healed);
        player.add_OnTeleported(_teleported);
        _locomotionState?.add_OnOwnedLocomotionStateChangedEvent(_stateChanged);
        _lifeState?.add_OnOwnedLifecycleStateChangedEvent(_stateChanged);
        GameEventData.add_CliOnGameEventBegin(_gameEvent);
    }

    internal void AfterCameraChange(CameraManager manager, bool accepted)
    { if (accepted && !_nativeWrite && _manager == manager) Close(); }

    internal void BeforeCameraTargetChange(CameraManager manager)
    {
        if (_nativeWrite) return;
        // A new native inspect target owns the reading area; do not leave our fading text over it.
        if (_manager == manager) Close(false);
        else if (CameraManager.Instance == manager) { Cleanup(_ui.Dispose); _fadingPlayer = null; }
        // The native caller assigns its new target immediately after this prefix.
        if (_returning?.Camera == manager.InspectItemCamera) FinishCameraReturn(true);
    }

    internal void BeforeMovementChange(PlayerEntity player)
    { if (!_nativeWrite && _player == player) Close(); }

    internal void RequestClose()
    {
        if (CanClose) Close();
    }

    internal void Close(bool fade = true)
    {
        if (_player is null && _slot == null)
        {
            if (!fade) { Cleanup(_ui.Dispose); _fadingPlayer = null; }
            return;
        }
        var player = _player;
        _player = null; // Clear ownership first: restoration calls the same patched native APIs.
        _settled = false; ++_entryRevision; _cameraSettled = null;
        _fadingPlayer = fade ? player : null;
        if (fade) Cleanup(_ui.FadeOut);
        else Cleanup(_ui.Dispose);
        if (player != null)
        {
            if (_damaged != null) Cleanup(() => player.remove_CliOnDamagedEvent(_damaged));
            if (_healed != null) Cleanup(() => player.remove_CliOnHealedEvent(_healed));
            if (_teleported != null) Cleanup(() => player.remove_OnTeleported(_teleported));
            if (_stateChanged != null)
            {
                Cleanup(() => _locomotionState?.remove_OnOwnedLocomotionStateChangedEvent(_stateChanged));
                Cleanup(() => _lifeState?.remove_OnOwnedLifecycleStateChangedEvent(_stateChanged));
            }
        }
        if (_gameEvent != null) Cleanup(() => GameEventData.remove_CliOnGameEventBegin(_gameEvent));
        _damaged = null; _healed = null; _teleported = null;
        _stateChanged = null; _gameEvent = null; _locomotionState = null; _lifeState = null;
        _nativeWrite = true;
        try
        {
            Cleanup(() => Visuals.HeldCameraOverrides.End());
            if (_virtual != null && _slot != null
                && _virtual.Follow == _slot.transform)
            {
                // The outgoing camera remains live during the native blend. Its old book/board
                // target must not replace the box until that blend has finished.
                _returning = new CameraReturn(_virtual, _virtual.Cast<ICinemachineCamera>(), _oldFollow, _oldRotation, _slot, _slot.transform, Time.frameCount);
                _slot = null;
                if (_manager != null && _manager == CameraManager.Instance && _manager.LocalAnimatorState == State.INSPECT_ITEM)
                    Cleanup(() => _manager.CliSetLocalCameraState(_oldState));
            }
            if (_frozen && player != null && player == EntityManager.Me && player.CurrentHealthSync > 0
                && player.PlayerMovement == _movement && player.playerCameraController == _look && player.LocomotionController == _locomotion)
            {
                if (_movement != null && !_movement.enabled && _look != null && !_look.enabled && _locomotion != null && !_locomotion.enabled)
                {
                    Cleanup(() => player.EnabledMovement(true));
                    Cleanup(() => { _locomotion._crouchLocked = _crouchLocked; _locomotion._jumpLocked = _jumpLocked; });
                }
                else
                {
                    // A component may have been enabled by another owner; release only our remaining writes.
                    if (_movement != null && !_movement.enabled) Cleanup(() => _movement.enabled = true);
                    if (_look != null && !_look.enabled) Cleanup(() => _look.enabled = true);
                    if (_locomotion != null && !_locomotion.enabled) Cleanup(() => _locomotion.enabled = true);
                    if (_locomotion != null)
                        Cleanup(() => { if (_locomotion._crouchLocked) _locomotion._crouchLocked = _crouchLocked;
                            if (_locomotion._jumpLocked) _locomotion._jumpLocked = _jumpLocked; });
                }
            }
            if (_tool != null && _usageAllowed && !_tool.UsageAllowed) Cleanup(() => _tool.UsageAllowed = true);
            if (_crosshair != null && !_crosshair._requestedVisible) Cleanup(() => _crosshair.Enabled(_crosshairVisible));
        }
        finally
        {
            if (_slot != null) { var slot = _slot; Cleanup(() => UObject.Destroy(slot)); }
            _slot = null; _manager = null; _virtual = null; _oldFollow = null; _output = null;
            _movement = _look = null; _locomotion = null; _tool = null; _crosshair = null;
            _frozen = _nativeWrite = _usageAllowed = _crosshairVisible = false;
        }
    }

    private void Cleanup(Action action)
    {
        try { action(); }
        catch (Exception error) { _plugin.Log.LogError($"[Lobby box] reading cleanup failed: {error}"); }
    }

    private void FinishCameraReturn(bool force = false)
    {
        var pending = _returning;
        if (pending == null) return;
        var ownsTarget = pending.Camera != null && pending.Slot != null && pending.Camera.Follow == pending.Target;
        if (!force && ownsTarget && (Time.frameCount == pending.Frame || CinemachineCore.Instance.IsLive(pending.NativeCamera))) return;
        _returning = null;
        if (ownsTarget)
            Cleanup(() => { pending.Camera!.Follow = pending.Follow; pending.Camera.transform.rotation = pending.Rotation; });
        if (pending.Slot != null) Cleanup(() => UObject.Destroy(pending.Slot));
    }

    public void Dispose() { Close(false); FinishCameraReturn(true); }
}

[HarmonyPatch(typeof(CameraManager), nameof(CameraManager.CliSetLocalCameraState))]
internal static class LobbyBoxCameraStatePatch
{
    private static void Postfix(CameraManager __instance, bool __result) => CalypsoExpansionSetPlugin.Instance?.LobbyBox?.AfterCameraChange(__instance, __result);
}

[HarmonyPatch(typeof(CameraManager), nameof(CameraManager.CliInitInspectItemCamera))]
internal static class LobbyBoxCameraTargetPatch
{
    private static void Prefix(CameraManager __instance) => CalypsoExpansionSetPlugin.Instance?.LobbyBox?.BeforeCameraTargetChange(__instance);
}

[HarmonyPatch(typeof(PlayerEntity), nameof(PlayerEntity.EnabledMovement))]
internal static class LobbyBoxMovementPatch
{
    private static void Prefix(PlayerEntity __instance) => CalypsoExpansionSetPlugin.Instance?.LobbyBox?.BeforeMovementChange(__instance);
}
