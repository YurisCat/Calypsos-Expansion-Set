using CalypsoExpansionSet.Audio;
using Entities.Player.Behaviours;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace CalypsoExpansionSet.Idols;

internal sealed class DuckStepIdol : HeldIdolEffect
{
    internal const ulong ConfigId = 0x4D49000000000005UL;
    // Native hearing combines this source range with each monster's own senses.
    // This is not a guaranteed detection radius or a forced aggro command.
    internal const float NoiseRange = 18f;
    internal static IdolDefinition Definition => new(
        "duck_step", "鸭步雕像", ConfigId, 0x4D490005U,
        7331884207844971565UL, IdolEffectAuthority.OwnerNativeGameplay, () => new DuckStepIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        CreateVisuals = prefab => HeldIdolAudio.CreateAttached(prefab, null, ConfigId, "duck_steps", 6, .15f, .04f,
            EmitNoiseOnServer),
        ContentVersion = 2
    };
    private static DuckStepIdol? _active;
    private HeldIdolContext _context;
    private Vector3 _previousPosition;

    public override void Start(HeldIdolContext context) { Dispose(); _context = context; _previousPosition = context.Holder.transform.position; }
    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        if (LocalHeldGuard.Ready(context, ConfigId)) _active = this;
        else { if (_active == this) _active = null; _previousPosition = context.Holder.transform.position; }
    }
    internal static void AfterFootstep(PlayerAudio audio, float before, Exception? error)
    {
        var effect = _active;
        if (effect == null || error != null || before <= 0 || audio._time != 0
            || audio.playerEntity != effect._context.Holder || !LocalHeldGuard.Ready(effect._context, ConfigId)
            || !effect._context.Holder.PlayerMovement._isGrounded) return;
        var position = effect._context.Holder.transform.position;
        var delta = position - effect._previousPosition; delta.y = 0;
        effect._previousPosition = position;
        if (delta.sqrMagnitude < .04f * .04f || delta.sqrMagnitude > 25f) return;
        HeldIdolAudio.Emit(effect._context);
    }

    internal static void EmitNoiseOnServer(HeldIdolContext context)
    {
        // The caller has verified the requesting connection, including remote
        // owners. Do not require EntityManager.Me/isOwned on the server here.
        var holder = context.Holder;
        var idol = context.Idol;
        if (!NetworkServer.active || !context.GameplayReady || holder == null || !holder.isActiveAndEnabled
            || holder.CurrentHealthSync <= 0 || idol == null || idol.SoItem == null || idol.SoItem.Id != ConfigId
            || idol.ItemOwner != holder || holder.InventoryNew == null || holder.InventoryNew.InHand != idol) return;
        var hearing = HearingSenseManager.Instance;
        if (hearing == null) throw new InvalidOperationException("Native hearing manager is unavailable for duck noise.");
        var debug = CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true;
        var before = debug ? hearing.VolumeEvents.Count : 0;
        // Two-argument native overload creates a normal event: no private
        // audience, no bypass of range/obstruction, and native persistence.
        // Source is the player, so player-specific hearing goals can resolve it.
        hearing.AddVolumeEvent(holder.transform, NoiseRange);
        if (debug)
            CalypsoExpansionSetPlugin.Instance!.Log.LogInfo($"[Duck noise] idol={idol.netId}, holder={holder.netId}, "
                + $"range={NoiseRange:F1}, nativeEvents={before}->{hearing.VolumeEvents.Count}, "
                + $"persistence={hearing.NoisePersistenceDuration:F2}s.");
    }
    public override void Dispose() { if (_active == this) _active = null; _context = default; }
}

// Native PlayFootSteps resets _time only after a footstep was actually emitted.
// Original footsteps remain; the extra sound and its hearing event share one
// validated server emission, regardless of the number of listening clients.
[HarmonyPatch(typeof(PlayerAudio), "PlayFootSteps")]
internal static class DuckFootstepPatch
{
    [HarmonyPrefix] private static void Prefix(PlayerAudio __instance, out float __state) => __state = __instance._time;
    [HarmonyFinalizer]
    private static Exception? Finalizer(PlayerAudio __instance, float __state, Exception? __exception)
    {
        DuckStepIdol.AfterFootstep(__instance, __state, __exception);
        return __exception;
    }
}
