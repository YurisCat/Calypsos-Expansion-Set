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
        ShortDescription = "越拿越慢，换人还原",
        CreateVisuals = CreateVisuals,
        CreateRuntime = () => new RelayIdolRuntime(),
        ContentVersion = 2
    };

    private static IDisposable CreateVisuals(GameObject prefab)
    {
        IdolEyeOverlay.Validate(prefab.GetComponent<ScoreTool>());
        return StickyNoteVisuals.Create(prefab, "todo_relay");
    }

    private static RelayIdol? _active;
    private HeldIdolContext _context;
    private bool _faulted;
    private float _nextDiagnostic;
    internal readonly record struct SpeedScope(RelayIdol? Effect, float Original, float Applied);

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

    internal static SpeedScope BeforeMovement(PlayerMovement movement)
    {
        var effect = _active;
        if (effect == null) return default;
        try
        {
            if (effect._faulted || !LocalHeldGuard.Ready(effect._context, ConfigId)
                || movement != effect._context.Holder.PlayerMovement
                || !RelayIdolRuntime.TryGetFatigue(effect._context, out var fatigue)) return default;
            var original = movement.SpeedMultiplier;
            var applied = original * RelayFatigue.Multiplier(fatigue);
            if (!float.IsFinite(original) || original < 0f || !float.IsFinite(applied)) return default;
            movement.SpeedMultiplier = applied;
            return new(effect, original, applied);
        }
        catch (Exception error) { effect.Fault(error); return default; }
    }

    internal static void AfterMovement(PlayerMovement movement, SpeedScope scope, Exception? error)
    {
        if (scope.Effect == null) return;
        try
        {
            if (movement != null && movement.SpeedMultiplier == scope.Applied)
                movement.SpeedMultiplier = scope.Original;
            if (error != null) { scope.Effect.Fault(error); return; }
            var plugin = CalypsoExpansionSetPlugin.Instance;
            if (movement != null && plugin?.DebugTools.Enabled == true
                && Time.realtimeSinceStartup >= scope.Effect._nextDiagnostic)
            {
                scope.Effect._nextDiagnostic = Time.realtimeSinceStartup + 3f;
                plugin.Log.LogInfo($"[Relay speed] current={movement.CurrentSpeed:F3}, "
                    + $"applied={scope.Applied:F3}, restored={movement.SpeedMultiplier:F3}.");
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
