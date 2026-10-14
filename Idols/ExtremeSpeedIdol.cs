using CalypsoExpansionSet.Visuals;
using Entities.Player.Behaviours;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using User;

namespace CalypsoExpansionSet.Idols;

internal sealed class ExtremeSpeedIdol : HeldIdolEffect
{
    internal const ulong ConfigId = 0x4D49000000000006UL;
    internal const float Multiplier = 4f;
    internal static IdolDefinition Definition => new(
        "extreme_speed", "极速雕像", ConfigId, 0x4D490006U,
        7331884207844971565UL, IdolEffectAuthority.OwnerNativeGameplay, () => new ExtremeSpeedIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        CreateVisuals = StickyNoteVisuals.Create,
        ContentVersion = 2
    };

    private static ExtremeSpeedIdol? _active;
    private HeldIdolContext _context;
    private bool _faulted;
    private float _nextDiagnostic;
    private readonly StickyWasdLatch _latch = new();
    private IntPtr _keyboard;
    private int _sampleFrame = -1;
    private bool _inputSuspended;

    public override void Start(HeldIdolContext context) { Dispose(); _context = context; _faulted = false; }
    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        var inputs = InputsManager.Instance;
        var ready = CanApply() && inputs != null && inputs.onMove != null && inputs.onMove.enabled;
        SampleKeys(ready);
        if (ready) _active = this;
        else if (_active == this) _active = null;
        if (CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled != true) _nextDiagnostic = 0;
    }
    private bool CanApply() => !_faulted && LocalHeldGuard.Ready(_context, ConfigId);
    internal readonly record struct SpeedScope(ExtremeSpeedIdol? Effect, float Original, float Applied);

    private void SampleKeys(bool ready)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) { _keyboard = IntPtr.Zero; _latch.Clear(); return; }
        if (keyboard.Pointer != _keyboard)
        {
            _keyboard = keyboard.Pointer;
            _latch.Clear();
            _sampleFrame = -1;
        }
        var down = (keyboard.wKey.isPressed ? 1 : 0) | (keyboard.aKey.isPressed ? 2 : 0)
            | (keyboard.sKey.isPressed ? 4 : 0) | (keyboard.dKey.isPressed ? 8 : 0);
        if (!ready)
        {
            _latch.Suspend(down);
            _inputSuspended = true;
            _sampleFrame = Time.frameCount;
            return;
        }
        if (_inputSuspended)
        {
            // Menu typing must never become gameplay input. Resume the previous
            // latched direction, and require a new press to change it.
            _latch.Suspend(down);
            _inputSuspended = false;
            _sampleFrame = Time.frameCount;
            return;
        }
        if (_sampleFrame == Time.frameCount) return;
        _sampleFrame = Time.frameCount;
        var pressed = (keyboard.wKey.wasPressedThisFrame ? 1 : 0) | (keyboard.aKey.wasPressedThisFrame ? 2 : 0)
            | (keyboard.sKey.wasPressedThisFrame ? 4 : 0) | (keyboard.dKey.wasPressedThisFrame ? 8 : 0);
        var previous = _latch.Direction;
        _latch.Read(down, pressed);
        if (previous != _latch.Direction && CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true)
            CalypsoExpansionSetPlugin.Instance.Log.LogInfo($"[Extreme speed] sticky WASD=({_latch.Direction.x:F0},{_latch.Direction.y:F0}).");
    }

    internal static void FilterMove(InputsManager inputs, ref Vector2 value)
    {
        var effect = _active;
        if (effect == null || inputs != InputsManager.Instance) return;
        try
        {
            var ready = effect.CanApply() && inputs.onMove != null && inputs.onMove.enabled;
            effect.SampleKeys(ready);
            if (!ready) { if (_active == effect) _active = null; return; }
            var direction = effect._latch.Direction;
            if (direction == Vector2.zero) return;
            // These are native gameplay inversion flags, not saved key bindings.
            // A virtual held W/A/S/D still passes through the game's axis rules.
            if (inputs.GameplayInvertHorizontalInputs) direction.x = -direction.x;
            if (inputs.GameplayInvertVerticalInputs) direction.y = -direction.y;
            value = direction;
        }
        catch (Exception error) { effect.Fault(error); }
    }

    internal static SpeedScope BeforeMovementUpdate(PlayerMovement movement)
    {
        var effect = _active;
        if (effect == null) return default;
        try
        {
            if (!effect.CanApply() || movement != effect._context.Holder.PlayerMovement) return default;
            var original = movement.SpeedMultiplier;
            var applied = original * Multiplier;
            if (!float.IsFinite(original) || original < 0f || !float.IsFinite(applied)) return default;
            movement.SpeedMultiplier = applied;
            return new(effect, original, applied);
        }
        catch (Exception error) { effect.Fault(error); return default; }
    }

    internal static void AfterMovementUpdate(PlayerMovement movement, SpeedScope scope, Exception? error)
    {
        if (scope.Effect == null) return;
        try
        {
            if (movement != null && movement.SpeedMultiplier == scope.Applied)
                movement.SpeedMultiplier = scope.Original;
            if (error != null) { scope.Effect.Fault(error); return; }
            var plugin = CalypsoExpansionSetPlugin.Instance;
            if (movement != null && plugin?.DebugTools.Enabled == true && Time.realtimeSinceStartup >= scope.Effect._nextDiagnostic)
            {
                scope.Effect._nextDiagnostic = Time.realtimeSinceStartup + 3f;
                plugin.Log.LogInfo($"[Extreme speed] current={movement.CurrentSpeed:F3}, factor={Multiplier:F1}, "
                    + $"nativeMultiplier={scope.Original:F3}, restored={movement.SpeedMultiplier:F3}.");
            }
        }
        catch (Exception cleanup) { scope.Effect.Fault(cleanup); }
    }
    private void Fault(Exception error)
    {
        if (_faulted) return;
        _faulted = true;
        if (_active == this) _active = null;
        _latch.Clear();
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Extreme speed disabled until released: {error}");
    }

    public override void Dispose()
    {
        if (_active == this) _active = null;
        _context = default; _nextDiagnostic = 0;
        _latch.Clear(); _keyboard = IntPtr.Zero; _sampleFrame = -1; _inputSuspended = false;
    }
}

// No OS key injection or action subscriptions. The last newly pressed WASD key
// wins even when opposite keys cancel the game's combined Move vector. Releasing
// a key never cancels the latch or selects an older, still-held key. If multiple
// presses arrive in the same input frame, use deterministic W/A/S/D order.
internal sealed class StickyWasdLatch
{
    private int _down;
    internal Vector2 Direction { get; private set; }
    internal void Read(int down, int pressed)
    {
        var fresh = ((down & ~_down) | pressed) & 15;
        if ((fresh & 1) != 0) Direction = new Vector2(0, 1);
        if ((fresh & 2) != 0) Direction = new Vector2(-1, 0);
        if ((fresh & 4) != 0) Direction = new Vector2(0, -1);
        if ((fresh & 8) != 0) Direction = new Vector2(1, 0);
        _down = down & 15;
    }
    internal void Suspend(int down) => _down = down & 15;
    internal void Clear() { _down = 0; Direction = Vector2.zero; }
}

[HarmonyPatch(typeof(InputsManager), nameof(InputsManager.Move), MethodType.Getter)]
internal static class ExtremeSpeedInputPatch
{
    [HarmonyPostfix]
    private static void Postfix(InputsManager __instance, ref Vector2 __result)
        => ExtremeSpeedIdol.FilterMove(__instance, ref __result);
}

// FixedUpdate calls SetGroundVelocity/SetQuicksandVelocity -> CalculateSpeed.
// Update separately reads the multiplier when normalizing animation. Scope both
// native callers, without retaining a setting or directly writing body velocity.
[HarmonyPatch(typeof(PlayerMovement), "Update")]
internal static class ExtremeSpeedMovementPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance, out ExtremeSpeedIdol.SpeedScope __state)
        => __state = ExtremeSpeedIdol.BeforeMovementUpdate(__instance);
    [HarmonyFinalizer]
    private static Exception? Finalizer(PlayerMovement __instance, ExtremeSpeedIdol.SpeedScope __state, Exception? __exception)
    {
        ExtremeSpeedIdol.AfterMovementUpdate(__instance, __state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(PlayerMovement), "FixedUpdate")]
internal static class ExtremeSpeedPhysicsPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance, out ExtremeSpeedIdol.SpeedScope __state)
        => __state = ExtremeSpeedIdol.BeforeMovementUpdate(__instance);
    [HarmonyFinalizer]
    private static Exception? Finalizer(PlayerMovement __instance, ExtremeSpeedIdol.SpeedScope __state, Exception? __exception)
    {
        ExtremeSpeedIdol.AfterMovementUpdate(__instance, __state, __exception);
        return __exception;
    }
}
