using CalypsoExpansionSet.Visuals;
using Entities.Player.Behaviours;
using HarmonyLib;
using UnityEngine;

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
        ContentVersion = 1
    };

    private static ExtremeSpeedIdol? _active;
    private HeldIdolContext _context;
    private bool _faulted;
    private float _nextDiagnostic;

    public override void Start(HeldIdolContext context) { Dispose(); _context = context; _faulted = false; }
    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        if (CanApply()) _active = this;
        else if (_active == this) _active = null;
    }
    private bool CanApply() => !_faulted && LocalHeldGuard.Ready(_context, ConfigId);
    internal readonly record struct SpeedScope(ExtremeSpeedIdol? Effect, float Original, float Applied);

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
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Extreme speed disabled until released: {error}");
    }

    public override void Dispose() { if (_active == this) _active = null; _context = default; _nextDiagnostic = 0; }
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
