using BepInEx.Configuration;
using Entities.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using _200_Dev.Entities;
using _200_Dev.Entities.Player.Behaviours.Camera;

namespace CalypsoExpansionSet.QualityOfLife.Pings;

internal sealed class PingInput : IDisposable
{
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly ConfigEntry<string> _mouse, _gamepad;
    private InputAction? _action;
    private readonly PingPress _press = new();
    private PlayerEntity? _player;
    private double _nextRequest;

    internal PingInput(CalypsoExpansionSetPlugin plugin)
    {
        _plugin = plugin;
        _mouse = plugin.Config.Bind("QualityOfLife", "PingMouseKeyboard", "<Mouse>/middleButton",
            "队友标记的键鼠 Input System 路径，例如 <Mouse>/middleButton 或 <Keyboard>/g；空值禁用。修改后重启。\n"
            + "/ Mouse/keyboard binding path for shared pings. Empty disables this binding. Restart after changes.");
        _gamepad = plugin.Config.Bind("QualityOfLife", "PingGamepad", "<Gamepad>/dpad/up",
            "队友标记的手柄 Input System 路径，默认十字键上；空值禁用。修改后重启。\n"
            + "/ Gamepad binding path for shared pings (default: D-pad up). Empty disables this binding. Restart after changes.");
    }

    internal void Tick(SharedPings pings)
    {
        var player = EntityManager.Me;
        if (_player != player) { Reset(); _player = player; }
        var camera = SetFovShakeData.Instance?._cam;
        var ready = pings.ClientReady && player != null && player.CurrentHealthSync > 0
            && player.PlayerInteraction != null && player.PlayerInteraction.CanInteract
            && _plugin.LocalGameplayReady(player) && camera != null && camera.isActiveAndEnabled
            && !Testing.TestRoomAppearanceView.IsInspecting
            && CameraManager.Instance?.LocalAnimatorState is CameraManager.ECameraAnimatorState.PLAYER_POV_STANDUP
                or CameraManager.ECameraAnimatorState.PLAYER_POV_CROUCH;
        if (!ready)
        {
            Reset();
            return;
        }
        if (_action == null)
        {
            _action = new InputAction("Calypso shared ping", InputActionType.Button);
            if (!string.IsNullOrWhiteSpace(_mouse.Value)) _action.AddBinding(_mouse.Value);
            if (!string.IsNullOrWhiteSpace(_gamepad.Value)) _action.AddBinding(_gamepad.Value);
        }
        if (!_action.enabled) { _press.Reset(); _action.Enable(); return; }
        var now = Time.realtimeSinceStartupAsDouble;
        var intent = _press.Update(_action.IsPressed(), _action.WasPressedThisFrame(), now);
        if (intent == PingPressAction.None || now < _nextRequest) return;
        _nextRequest = now + 0.2;
        if (intent == PingPressAction.Clear) { pings.CancelOwn(); return; }
        var interaction = player!.PlayerInteraction!;
        var mask = (int)interaction._occlusionLayerMask | (int)interaction._interactableLayerMask;
        // Keep native aim aligned with the wide-body interaction camera. The
        // render-only upside-down layer does not change the center ray.
        try
        {
            Visuals.HeldCameraOverrides.BeginInteraction(camera!);
            var ray = camera!.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (!SharedPings.Raycast(ray, mask, out var hit)) { pings.Trace("Aim: no eligible surface."); return; }
            var item = SharedPings.HitItem(hit);
            pings.Trace($"Aim: {hit.collider.name}, layer={hit.collider.gameObject.layer}, trigger={hit.collider.isTrigger}, "
                + $"item={(item != null ? item.name : "none")}, netId={(item != null ? item.netId : 0)}.");
            if (item != null && (item.netId == 0 || SharedPings.ItemFor(item.netId) != item)) return;
            pings.Request(ray, item != null ? item.netId : 0);
        }
        finally { Visuals.HeldCameraOverrides.End(camera); }
    }

    internal void Reset()
    { _action?.Disable(); _press.Reset(); _nextRequest = 0; }

    public void Dispose()
    { Reset(); _action?.Dispose(); _action = null; _player = null; }
}
