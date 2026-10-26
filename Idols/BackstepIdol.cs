using CalypsoExpansionSet.Visuals;
using HarmonyLib;
using UnityEngine;
using User;

namespace CalypsoExpansionSet.Idols;

internal sealed class BackstepIdol : HeldIdolEffect
{
    internal const ulong ConfigId = 0x4D49000000000010UL;
    internal static IdolDefinition Definition => new(
        "backstep", "退行雕像", ConfigId, 0x4D490010U,
        7331884207844971565UL, IdolEffectAuthority.OwnerNativeGameplay, () => new BackstepIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        ShortDescription = "进亦是退，退亦是退",
        CreateVisuals = prefab => StickyNoteVisuals.Create(prefab, "handdrawn_backstep"),
        ContentVersion = 3
    };

    private static BackstepIdol? _active;
    private HeldIdolContext _context;
    private bool _faulted;

    public override void Start(HeldIdolContext context)
    {
        Dispose();
        _context = context;
        _faulted = false;
    }

    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        if (CanApply(InputsManager.Instance)) _active = this;
        else Disarm();
    }

    private bool CanApply(InputsManager? inputs) => !_faulted
        && inputs != null && inputs == InputsManager.Instance && inputs.onMove != null && inputs.onMove.enabled
        && CalypsoExpansionSetPlugin.Instance?.Registry.Ready == true
        && LocalHeldGuard.Ready(_context, ConfigId);

    internal static void FilterMove(InputsManager inputs, ref Vector2 value)
    {
        var effect = _active;
        if (effect == null || inputs != InputsManager.Instance) return;
        try
        {
            if (!effect.CanApply(inputs)) { effect.Disarm(); return; }
            value.y = -Mathf.Abs(value.y);
        }
        catch (Exception error) { effect.Fault(error); }
    }

    internal static void AllowBackwardRun(PlayerLocomotionController controller, ref bool allowed)
    {
        var effect = _active;
        if (effect == null || allowed) return;
        try
        {
            var inputs = InputsManager.Instance;
            if (!effect.CanApply(inputs)) { effect.Disarm(); return; }
            if (controller != effect._context.Holder.LocomotionController) return;
            // Keep CanRunCheck's stamina/action checks and the native run lifecycle.
            // Only its forward-direction predicate accepts our backward input too.
            allowed = inputs!.Move.y < 0f;
        }
        catch (Exception error) { effect.Fault(error); }
    }

    private void Disarm() { if (_active == this) _active = null; }

    private void Fault(Exception error)
    {
        _faulted = true;
        Disarm();
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Backstep effect disabled until released: {error}");
    }

    public override void Dispose() { Disarm(); _context = default; }
}

[HarmonyPatch(typeof(InputsManager), nameof(InputsManager.Move), MethodType.Getter)]
internal static class BackstepMovePatch
{
    [HarmonyPostfix]
    private static void Postfix(InputsManager __instance, ref Vector2 __result)
        => BackstepIdol.FilterMove(__instance, ref __result);
}

[HarmonyPatch(typeof(PlayerLocomotionController), "IsUsingForwardInput")]
internal static class BackstepRunDirectionPatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerLocomotionController __instance, ref bool __result)
        => BackstepIdol.AllowBackwardRun(__instance, ref __result);
}
