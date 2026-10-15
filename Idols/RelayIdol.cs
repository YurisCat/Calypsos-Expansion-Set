using CalypsoExpansionSet.Visuals;
using Entities.Player.Behaviours;
using HarmonyLib;
using UnityEngine;

namespace CalypsoExpansionSet.Idols;

internal sealed class RelayIdol : HeldIdolEffect
{
    internal const ulong ConfigId = 0x4D49000000000009UL;
    internal static IdolDefinition Definition => new(
        "relay", "接力雕像", ConfigId, 0x4D490009U,
        7331884207844971565UL, IdolEffectAuthority.OwnerNativeGameplay, () => new RelayIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        ShortDescription = "越走越慢，快找伙伴",
        CreateVisuals = CreateVisuals,
        CreateRuntime = () => new RelayIdolRuntime(),
        SharedVisual = new IdolSharedVisualDefinition(IdolVisualAuthority.Server,
            idol => new IdolEyeProgressVisual(idol, "Calypso Relay Eyes", RelayIdolRuntime.EyeColor)) { ShowWhenDropped = true },
        ContentVersion = 6
    };

    private static IDisposable CreateVisuals(GameObject prefab)
    {
        IdolEyeOverlay.Validate(prefab.GetComponent<ScoreTool>());
        return StickyNoteVisuals.Create(prefab, "handdrawn_relay");
    }

    private static RelayIdol? _active;
    private HeldIdolContext _context;
    private bool _faulted;
    private float _nextDiagnostic;
    internal readonly record struct SpeedScope(RelayIdol? Effect, float Original, float Applied, bool Air);

    public override void Start(HeldIdolContext context)
    {
        Dispose();
        _context = context;
        _faulted = false;
    }

    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        if (!_faulted && LocalHeldGuard.Ready(context, ConfigId)) _active = this;
        else if (_active == this) _active = null;
        if (CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled != true) _nextDiagnostic = 0;
    }

    internal static SpeedScope BeforeMovement(PlayerMovement movement, bool air = false)
    {
        var effect = _active;
        if (effect == null) return default;
        SpeedScope scope = default;
        try
        {
            if (effect._faulted || !LocalHeldGuard.Ready(effect._context, ConfigId)
                || movement != effect._context.Holder.PlayerMovement
                || !RelayIdolRuntime.TryGetMultiplier(effect._context, out var multiplier)) return default;
            // Air consumes MaxSpeed directly; scope it separately to avoid
            // applying fatigue twice to ground movement in FixedUpdate.
            var original = air ? movement.MaxSpeed : movement.SpeedMultiplier;
            var applied = original * multiplier;
            if (!float.IsFinite(original) || original < 0f || !float.IsFinite(applied)) return default;
            scope = new(effect, original, applied, air);
            if (air) movement.MaxSpeed = applied;
            else movement.SpeedMultiplier = applied;
            return scope;
        }
        catch (Exception error) { AfterMovement(movement, scope, error); effect.Fault(error); return default; }
    }

    internal static void AfterMovement(PlayerMovement movement, SpeedScope scope, Exception? error)
    {
        if (scope.Effect == null) return;
        try
        {
            if (movement != null)
            {
                if (scope.Air)
                {
                    if (movement.MaxSpeed == scope.Applied) movement.MaxSpeed = scope.Original;
                }
                else if (movement.SpeedMultiplier == scope.Applied)
                    movement.SpeedMultiplier = scope.Original;
            }
            if (error != null) { scope.Effect.Fault(error); return; }
            var plugin = CalypsoExpansionSetPlugin.Instance;
            if (movement != null && plugin?.DebugTools.Enabled == true
                && Time.realtimeSinceStartup >= scope.Effect._nextDiagnostic)
            {
                scope.Effect._nextDiagnostic = Time.realtimeSinceStartup + 3f;
                plugin.Log.LogInfo($"[Relay speed] ground={movement.CurrentSpeed:F3}, air={movement.CurrentAirSpeed:F3}, "
                    + $"scope={(scope.Air ? "air" : "ground")}, applied={scope.Applied:F3}.");
            }
        }
        catch (Exception cleanup) { scope.Effect.Fault(cleanup); }
    }

    private void Fault(Exception error)
    {
        if (_faulted) return;
        _faulted = true;
        if (_active == this) _active = null;
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Relay movement disabled until released: {error}");
    }

    public override void Dispose()
    {
        if (_active == this) _active = null;
        _context = default;
        _nextDiagnostic = 0;
    }
}

[HarmonyPatch(typeof(PlayerMovement), "SetAirVelocity")]
internal static class RelayAirPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance, out RelayIdol.SpeedScope __state)
        => __state = RelayIdol.BeforeMovement(__instance, true);
    [HarmonyFinalizer]
    private static Exception? Finalizer(PlayerMovement __instance, RelayIdol.SpeedScope __state, Exception? __exception)
    {
        RelayIdol.AfterMovement(__instance, __state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(PlayerMovement), "Update")]
internal static class RelayMovementPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance, out RelayIdol.SpeedScope __state)
        => __state = RelayIdol.BeforeMovement(__instance);
    [HarmonyFinalizer]
    private static Exception? Finalizer(PlayerMovement __instance, RelayIdol.SpeedScope __state, Exception? __exception)
    {
        RelayIdol.AfterMovement(__instance, __state, __exception);
        return __exception;
    }
}

[HarmonyPatch(typeof(PlayerMovement), "FixedUpdate")]
internal static class RelayPhysicsPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance, out RelayIdol.SpeedScope __state)
        => __state = RelayIdol.BeforeMovement(__instance);
    [HarmonyFinalizer]
    private static Exception? Finalizer(PlayerMovement __instance, RelayIdol.SpeedScope __state, Exception? __exception)
    {
        RelayIdol.AfterMovement(__instance, __state, __exception);
        return __exception;
    }
}
