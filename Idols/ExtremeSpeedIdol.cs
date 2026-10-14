using CalypsoExpansionSet.Visuals;
using Entities.Player;
using Entities.Player.Behaviours;
using Entities.Player.States.Data.Stand;
using HarmonyLib;
using Mirror;
using UnityEngine;
using User;
using _200_Dev.Entities;

namespace CalypsoExpansionSet.Idols;

internal sealed class ExtremeSpeedIdol : HeldIdolEffect
{
    internal const ulong ConfigId = 0x4D49000000000006UL;
    internal const float Multiplier = 5f;
    internal static IdolDefinition Definition => new(
        "extreme_speed", "极速雕像", ConfigId, 0x4D490006U,
        7331884207844971565UL, IdolEffectAuthority.OwnerNativeGameplay, () => new ExtremeSpeedIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        CreateVisuals = StickyNoteVisuals.Create,
        ContentVersion = 4
    };

    private static ExtremeSpeedIdol? _active;
    private HeldIdolContext _context;
    private SOPlayerStandWalkState? _walkData;
    private bool _faulted;
    private float _nextDiagnostic;
    private int _startFrame;
    private readonly StickyMoveLatch _latch = new();
    private bool _inPhysics, _velocityApplied, _airLocked;
    private Vector3 _groundDirection, _airDirection, _stepDirection;

    public override void Start(HeldIdolContext context)
    {
        Dispose();
        _context = context;
        _faulted = false;
        _startFrame = Time.frameCount;
        // Read the standing-walk configuration once, never change the shared SO.
        var states = EntityManager.Instance?.PlayerLocomotionEntityStates;
        if (states != null)
            for (var index = 0; index < states.Count; index++)
            {
                var state = states[index];
                if (state != null && state.stateType == EntityManager.LocomotionEntityStateType.Walk)
                {
                    _walkData = state.entityState?.TryCast<PlayerWalkState>()?.Data;
                    break;
                }
            }
        if (_walkData == null) throw new InvalidOperationException("Standing-walk speed configuration is unavailable.");
    }

    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        if (CanHold())
        {
            _active = this;
            try
            {
                StopRunning();
                UpdateAirLock(_context.Holder.PlayerMovement);
                SampleMove(InputsManager.Instance);
            }
            catch (Exception error) { Fault(error); }
        }
        else
        {
            if (_active == this) _active = null;
            _latch.Reset();
            ResetMotion();
        }
        if (CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled != true) _nextDiagnostic = 0;
    }

    // UI action-map availability is deliberately separate from ownership/life.
    // Do not weaken LocalHeldGuard: other idols still need its gameplay gate.
    private bool CanHold()
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        var holder = _context.Holder;
        var idol = _context.Idol;
        return !_faulted && plugin != null && !plugin.RuntimeFailed
            && NetworkClient.isConnected && NetworkClient.ready && plugin.Sync.ClientCompatible
            && holder != null && holder == EntityManager.Me && holder.isActiveAndEnabled
            && holder.CurrentHealthSync > 0 && holder.PlayerMovement != null && holder.PlayerMovement.isOwned
            && holder.PlayerMovement.isActiveAndEnabled
            && holder.LocomotionController != null && holder.LocomotionController.isActiveAndEnabled
            && idol != null && idol.SoItem != null && idol.SoItem.Id == ConfigId && idol.ItemOwner == holder
            && holder.InventoryNew != null && holder.InventoryNew.InHand == idol;
    }

    private bool CanMove() => CanHold() && Time.timeScale > 0f;

    private void SampleMove(InputsManager? inputs)
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        var sample = CanMove() && inputs != null && inputs.onMove != null && inputs.onMove.enabled
            && plugin != null && plugin.LocalGameplayReady(_context.Holder);
        var previous = _latch.Direction;
        // 'move' is the native unified action callback's value, before gameplay
        // inversion and our getter patch. It includes bindings and gamepads.
        if (!sample) _latch.Read(Vector2.zero, false);
        else if (Time.frameCount != _startFrame && IsGrounded(_context.Holder.PlayerMovement))
            _latch.Read(inputs!.move, true);
        if (previous != _latch.Direction && plugin?.DebugTools.Enabled == true)
            plugin.Log.LogInfo($"[Extreme speed] direction=({_latch.Direction.x:F2},{_latch.Direction.y:F2}).");
    }

    private static bool IsGrounded(PlayerMovement movement) => movement._isGrounded && !movement._jumpInitiated;

    private float TargetSpeed()
    {
        var data = _walkData;
        if (data == null) throw new InvalidOperationException("Standing-walk speed configuration was lost.");
        var baseline = _context.Holder.transformationStatus == PlayerEntity.TransformationStatus.Aged
            ? data.SpeedAged : data.Speed;
        var target = baseline * Multiplier;
        if (!float.IsFinite(target) || target <= 0f)
            throw new InvalidOperationException("Native walking speed is invalid.");
        return target;
    }

    private Vector3 WorldDirection(PlayerMovement movement)
    {
        var inputs = InputsManager.Instance;
        var direction = _latch.Direction;
        if (inputs != null)
        {
            if (inputs.GameplayInvertHorizontalInputs) direction.x = -direction.x;
            if (inputs.GameplayInvertVerticalInputs) direction.y = -direction.y;
        }
        var root = movement.root;
        if (root == null) throw new InvalidOperationException("Player movement root is unavailable.");
        var world = root.right * direction.x + root.forward * direction.y;
        world.y = 0f;
        var magnitude = world.magnitude;
        if (!float.IsFinite(magnitude) || magnitude <= 0f)
            throw new InvalidOperationException("Player horizontal direction is invalid.");
        return world / magnitude;
    }

    private void UpdateAirLock(PlayerMovement movement)
    {
        if (IsGrounded(movement)) { _airLocked = false; return; }
        if (_airLocked) return;
        _airDirection = _groundDirection.sqrMagnitude > 0f ? _groundDirection : WorldDirection(movement);
        _airLocked = true;
    }

    private void StopRunning()
    {
        var controller = _context.Holder.LocomotionController;
        if (controller.LocomotionEsm?.CurrentStateType == EntityManager.LocomotionEntityStateType.Run)
            controller.CliTryToWalk(); // Exit through the native state machine (stamina, FOV and animation).
        else if (controller.IsRunning) controller.IsRunning = false;
    }

    internal static bool BlockRun(PlayerLocomotionController controller)
    {
        var effect = _active;
        if (effect == null) return false;
        try { return effect.CanHold() && controller == effect._context.Holder.LocomotionController; }
        catch (Exception error) { effect.Fault(error); return false; }
    }

    internal static ExtremeSpeedIdol? BeforePhysics(PlayerMovement movement)
    {
        var effect = _active;
        if (effect == null) return null;
        try
        {
            if (!effect.CanMove() || movement != effect._context.Holder.PlayerMovement) return null;
            effect.StopRunning();
            effect.UpdateAirLock(movement);
            effect._inPhysics = true;
            effect._velocityApplied = false;
            return effect;
        }
        catch (Exception error) { effect.Fault(error); return null; }
    }

    internal static void NoteVelocity(PlayerMovement movement)
    {
        var effect = _active;
        if (effect == null || !effect._inPhysics) return;
        try
        {
            if (!effect.CanMove() || movement != effect._context.Holder.PlayerMovement) return;
            effect.UpdateAirLock(movement);
            effect.SampleMove(InputsManager.Instance);
            effect._stepDirection = effect._airLocked ? effect._airDirection : effect.WorldDirection(movement);
            if (!effect._airLocked) effect._groundDirection = effect._stepDirection;
            effect._velocityApplied = true;
        }
        catch (Exception error) { effect.Fault(error); }
    }

    internal static void AfterPhysics(PlayerMovement movement, ExtremeSpeedIdol? effect, Exception? error)
    {
        if (effect == null) return;
        effect._inPhysics = false;
        try
        {
            if (error != null) { effect.Fault(error); return; }
            // Only correct a step that used a native ground/air velocity path.
            if (!effect._velocityApplied || !effect.CanMove() || movement != effect._context.Holder.PlayerMovement) return;
            var body = movement.rb;
            if (body == null || body.isKinematic) return;
            effect.UpdateAirLock(movement); // HandleJumpRequest may have accepted a jump during this step.
            var target = effect.TargetSpeed();
            // PhysX damping runs after FixedUpdate. Precompensate X/Z only;
            // leaving damping and Y intact preserves native jump/fall integration.
            var retention = 1f - body.linearDamping * Time.fixedDeltaTime;
            if (!float.IsFinite(retention) || retention <= 0f || retention > 1f)
                throw new InvalidOperationException("Player linear damping cannot preserve horizontal speed.");
            var horizontal = effect._stepDirection * (target / retention);
            if (!float.IsFinite(horizontal.x) || !float.IsFinite(horizontal.z))
                throw new InvalidOperationException("Player horizontal velocity is invalid.");
            var velocity = body.linearVelocity;
            velocity.x = horizontal.x;
            velocity.z = horizontal.z;
            body.linearVelocity = velocity;
        }
        catch (Exception failure) { effect.Fault(failure); }
        finally { effect._velocityApplied = false; }
    }

    private void ResetMotion()
    {
        _inPhysics = _velocityApplied = _airLocked = false;
        _groundDirection = _airDirection = _stepDirection = Vector3.zero;
    }

    internal static void FilterMove(InputsManager inputs, ref Vector2 value)
    {
        var effect = _active;
        if (effect == null || inputs != InputsManager.Instance) return;
        try
        {
            effect.SampleMove(inputs);
            if (!effect.CanMove()) return;
            var direction = effect._latch.Direction;
            if (inputs.GameplayInvertHorizontalInputs) direction.x = -direction.x;
            if (inputs.GameplayInvertVerticalInputs) direction.y = -direction.y;
            value = direction;
        }
        catch (Exception error) { effect.Fault(error); }
    }

    internal readonly record struct SpeedScope(ExtremeSpeedIdol? Effect, float MaxSpeed, float SpeedMultiplier,
        float AppliedSpeed, float AppliedMultiplier);

    internal static SpeedScope BeforeSpeed(PlayerMovement movement)
    {
        var effect = _active;
        if (effect == null) return default;
        SpeedScope scope = default;
        try
        {
            if (!effect.CanMove() || movement != effect._context.Holder.PlayerMovement) return default;
            var target = effect.TargetSpeed();
            // CalculateSpeed and animation divide by ToolSlowPower; air uses
            // MaxSpeed directly. Cancel only that divisor for one native call.
            var toolSlow = movement.ToolSlowPower;
            if (!float.IsFinite(target) || target <= 0f || !float.IsFinite(toolSlow) || toolSlow <= 0f)
                throw new InvalidOperationException("Native walking speed or held-tool slowdown is invalid.");
            scope = new(effect, movement.MaxSpeed, movement.SpeedMultiplier, target, toolSlow);
            movement.MaxSpeed = target;
            movement.SpeedMultiplier = toolSlow;
            return scope;
        }
        catch (Exception error)
        {
            AfterSpeed(movement, scope, error);
            effect.Fault(error);
            return default;
        }
    }

    internal static void AfterSpeed(PlayerMovement movement, SpeedScope scope, Exception? error)
    {
        if (scope.Effect == null) return;
        try
        {
            if (movement != null)
            {
                if (movement.MaxSpeed == scope.AppliedSpeed) movement.MaxSpeed = scope.MaxSpeed;
                if (movement.SpeedMultiplier == scope.AppliedMultiplier) movement.SpeedMultiplier = scope.SpeedMultiplier;
            }
            if (error != null) { scope.Effect.Fault(error); return; }
            var plugin = CalypsoExpansionSetPlugin.Instance;
            if (movement != null && plugin?.DebugTools.Enabled == true && Time.realtimeSinceStartup >= scope.Effect._nextDiagnostic)
            {
                scope.Effect._nextDiagnostic = Time.realtimeSinceStartup + 3f;
                plugin.Log.LogInfo($"[Extreme speed] ground={movement.CurrentSpeed:F3}, air={movement.CurrentAirSpeed:F3}, "
                    + $"target={scope.AppliedSpeed:F3}, factor={Multiplier:F1}.");
            }
        }
        catch (Exception cleanup) { scope.Effect.Fault(cleanup); }
    }

    private void Fault(Exception error)
    {
        if (_faulted) return;
        _faulted = true;
        if (_active == this) _active = null;
        _latch.Reset();
        ResetMotion();
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Extreme speed disabled until released: {error}");
    }

    public override void Dispose()
    {
        if (_active == this) _active = null;
        _context = default;
        _walkData = null;
        _nextDiagnostic = 0;
        _latch.Reset();
        ResetMotion();
    }
}

internal sealed class StickyMoveLatch
{
    internal const float DeadZone = 0.1f;
    internal Vector2 Direction { get; private set; } = Vector2.up;
    private bool _waitForNeutral;

    internal void Read(Vector2 raw, bool sampling)
    {
        if (!sampling) { _waitForNeutral = true; return; }
        if (!float.IsFinite(raw.x) || !float.IsFinite(raw.y)) return;
        var magnitude = raw.magnitude;
        if (!float.IsFinite(magnitude)) return;
        if (magnitude <= DeadZone) { _waitForNeutral = false; return; }
        // A key/stick held while leaving UI must first be released/centered.
        if (_waitForNeutral) return;
        Direction = raw / magnitude;
    }

    internal void Reset() { Direction = Vector2.up; _waitForNeutral = false; }
}

[HarmonyPatch(typeof(InputsManager), nameof(InputsManager.Move), MethodType.Getter)]
internal static class ExtremeSpeedInputPatch
{
    [HarmonyPostfix]
    private static void Postfix(InputsManager __instance, ref Vector2 __result)
        => ExtremeSpeedIdol.FilterMove(__instance, ref __result);
}

// Scope each speed consumer, so state changes and jump processing never see a
// retained override. Horizontal correction is separate from native vertical motion.
[HarmonyPatch(typeof(PlayerMovement), "CalculateSpeed")]
internal static class ExtremeSpeedGroundPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance, out ExtremeSpeedIdol.SpeedScope __state)
        => __state = ExtremeSpeedIdol.BeforeSpeed(__instance);
    [HarmonyFinalizer]
    private static Exception? Finalizer(PlayerMovement __instance, ExtremeSpeedIdol.SpeedScope __state, Exception? __exception)
    {
        ExtremeSpeedIdol.AfterSpeed(__instance, __state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(PlayerMovement), "SetAirVelocity")]
internal static class ExtremeSpeedAirPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance, out ExtremeSpeedIdol.SpeedScope __state)
        => __state = ExtremeSpeedIdol.BeforeSpeed(__instance);
    [HarmonyFinalizer]
    private static Exception? Finalizer(PlayerMovement __instance, ExtremeSpeedIdol.SpeedScope __state, Exception? __exception)
    {
        ExtremeSpeedIdol.AfterSpeed(__instance, __state, __exception);
        if (__exception == null) ExtremeSpeedIdol.NoteVelocity(__instance);
        return __exception;
    }
}

[HarmonyPatch(typeof(PlayerMovement), "SetGroundVelocity")]
internal static class ExtremeSpeedGroundVelocityPatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerMovement __instance) => ExtremeSpeedIdol.NoteVelocity(__instance);
}

[HarmonyPatch(typeof(PlayerMovement), "FixedUpdate")]
internal static class ExtremeSpeedPhysicsPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance, out ExtremeSpeedIdol? __state)
        => __state = ExtremeSpeedIdol.BeforePhysics(__instance);
    [HarmonyFinalizer]
    private static Exception? Finalizer(PlayerMovement __instance, ExtremeSpeedIdol? __state, Exception? __exception)
    {
        ExtremeSpeedIdol.AfterPhysics(__instance, __state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(PlayerLocomotionController), "CanRunCheck")]
internal static class ExtremeSpeedRunGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerLocomotionController __instance, ref bool __result)
    {
        if (!ExtremeSpeedIdol.BlockRun(__instance)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerLocomotionController), "OnRunInput_ContinuousState")]
internal static class ExtremeSpeedRunInputPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerLocomotionController __instance) => !ExtremeSpeedIdol.BlockRun(__instance);
}

[HarmonyPatch(typeof(PlayerLocomotionController), "CliTryToRun")]
internal static class ExtremeSpeedRunStatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerLocomotionController __instance) => !ExtremeSpeedIdol.BlockRun(__instance);
}

[HarmonyPatch(typeof(PlayerMovement), "Update")]
internal static class ExtremeSpeedAnimationPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance, out ExtremeSpeedIdol.SpeedScope __state)
        => __state = ExtremeSpeedIdol.BeforeSpeed(__instance);
    [HarmonyFinalizer]
    private static Exception? Finalizer(PlayerMovement __instance, ExtremeSpeedIdol.SpeedScope __state, Exception? __exception)
    {
        ExtremeSpeedIdol.AfterSpeed(__instance, __state, __exception);
        return __exception;
    }
}
