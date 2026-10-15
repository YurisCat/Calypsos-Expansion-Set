using System.Diagnostics;
using System.Threading;
using CalypsoExpansionSet.Visuals;
using HarmonyLib;
using Mirror;
using UnityEngine;
using User;
using _200_Dev.Entities;
using _200_Dev.Sound;
using _200_Dev.UI.MicrophoneSignal;

namespace CalypsoExpansionSet.Idols;

internal sealed class VoiceIdol : HeldIdolEffect
{
    private const ulong ConfigId = 0x4D49000000000004UL;
    private const float DefaultThreshold = 0.05f;

    internal static IdolDefinition Definition => new(
        "voice", "声控雕像", ConfigId, 0x4D490004U,
        7331884207844971565UL, IdolEffectAuthority.OwnerNativeGameplay, () => new VoiceIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        ShortDescription = "发声前进，闭嘴立定",
        CreateVisuals = prefab => StickyNoteVisuals.Create(prefab, "handdrawn_voice"),
        SharedVisual = new IdolSharedVisualDefinition(IdolVisualAuthority.Holder, idol => new VoiceEyeGlow(idol)),
        ContentVersion = 6
    };

    // Audio callbacks only publish into this capture. Replacing it discards old
    // microphone/menu/holder samples without writing to the game's input state.
    private sealed class Capture
    {
        internal readonly IntPtr Microphone;
        internal float Amplitude;
        internal long Timestamp;
        internal Capture(IntPtr microphone) => Microphone = microphone;
    }

    private static VoiceIdol? _active;
    private readonly VoiceInputGate _gate = new();
    private HeldIdolContext _context;
    private Capture? _capture;
    private Exception? _audioError;
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
        CheckAudioError();
        if (!HasLocalHolder()) { Disarm(); return; }
        if (_active != this)
        {
            _active?.Disarm();
            Volatile.Write(ref _active, this);
        }
        PublishEyes(EvaluateMotion());
    }

    private bool HasLocalHolder()
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        var holder = _context.Holder;
        var idol = _context.Idol;
        return !_faulted && plugin != null && !plugin.RuntimeFailed && plugin.Registry.Ready
            && NetworkClient.isConnected && NetworkClient.ready && plugin.Sync.ClientCompatible
            && holder != null && holder == EntityManager.Me && holder.isActiveAndEnabled
            && holder.CurrentHealthSync > 0 && holder.PlayerMovement != null && holder.PlayerMovement.isOwned
            && idol != null && idol.SoItem != null && idol.SoItem.Id == ConfigId && idol.ItemOwner == holder
            && holder.InventoryNew != null && holder.InventoryNew.InHand == idol;
    }

    private PlayerMicrophone? PrepareCapture()
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        var inputs = InputsManager.Instance;
        if (!_context.GameplayReady || plugin == null || !plugin.LocalGameplayReady(_context.Holder)
            || inputs == null || inputs.onMove == null || !inputs.onMove.enabled || DissonanceAudioDevice.IsMute())
        {
            ClearCapture();
            return null;
        }
        var microphone = _context.Holder.PlayerMicrophone;
        if (microphone == null || !microphone.isActiveAndEnabled)
        {
            ClearCapture();
            return null;
        }
        var capture = Volatile.Read(ref _capture);
        if (capture == null || capture.Microphone != microphone.Pointer)
        {
            ClearCapture();
            Volatile.Write(ref _capture, new Capture(microphone.Pointer));
            if (plugin.DebugTools.Enabled)
                plugin.Log.LogInfo($"[Voice] microphone attached: idol={_context.Idol.netId}, "
                    + $"threshold={microphone.AmplitudeThreshold:F3}, multiplier={microphone.AmplitudeMultiplier:F3}.");
        }
        return microphone;
    }

    internal static void RecordAudio(PlayerMicrophone microphone)
    {
        var effect = Volatile.Read(ref _active);
        if (effect == null) return;
        var capture = Volatile.Read(ref effect._capture);
        if (capture == null || capture.Microphone != microphone.Pointer) return;
        try
        {
            // Field wrappers avoid boxing a float through a native getter here.
            Volatile.Write(ref capture.Amplitude, microphone._correctedAmplitude);
            Interlocked.Exchange(ref capture.Timestamp, Stopwatch.GetTimestamp());
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref effect._audioError, exception, null);
            Volatile.Write(ref effect._capture, null);
        }
    }

    internal static void OverrideMove(InputsManager inputs, ref Vector2 move)
    {
        var effect = Volatile.Read(ref _active);
        if (effect == null) return;
        try
        {
            if (inputs != InputsManager.Instance) return;
            effect.CheckAudioError();
            // Recheck at consumption time: pickup/drop, ownership and menus can
            // change between our Update and the native movement FixedUpdate.
            if (!effect.HasLocalHolder()) { effect.Disarm(); return; }
            move = Vector2.zero;
            var moving = effect.EvaluateMotion();
            effect.PublishEyes(moving);
            if (moving) move = Vector2.up;
        }
        catch (Exception exception) { effect.Fault(exception); }
    }

    private bool EvaluateMotion()
    {
        var microphone = PrepareCapture();
        var capture = Volatile.Read(ref _capture);
        if (microphone == null || capture == null) return false;
        var timestamp = Interlocked.Read(ref capture.Timestamp);
        if (timestamp == 0) return false;
        var threshold = microphone.amplitudeThreshold;
        if (!float.IsFinite(threshold) || threshold <= 0f) threshold = DefaultThreshold;
        return _gate.Evaluate(Volatile.Read(ref capture.Amplitude), threshold,
            timestamp / (double)Stopwatch.Frequency, Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
    }

    private void CheckAudioError()
    {
        var exception = Interlocked.Exchange(ref _audioError, null);
        if (exception != null) throw new InvalidOperationException("Native microphone sampling failed.", exception);
    }

    private void ClearCapture()
    {
        Volatile.Write(ref _capture, null);
        _gate.Reset();
        PublishEyes(false);
    }

    private void Disarm()
    {
        if (Volatile.Read(ref _active) == this) Volatile.Write(ref _active, null);
        ClearCapture();
    }

    private void PublishEyes(bool moving)
    {
        if (_context.Idol != null) CalypsoExpansionSetApi.PublishVisualState(_context.Idol, moving ? 1f : 0f);
    }

    private void Fault(Exception exception)
    {
        _faulted = true;
        Disarm();
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Voice effect disabled until released: {exception}");
    }

    public override void Dispose()
    {
        Disarm();
        Interlocked.Exchange(ref _audioError, null);
        _context = default;
    }
}

[HarmonyPatch(typeof(InputsManager), nameof(InputsManager.Move), MethodType.Getter)]
internal static class HeldVoiceMovePatch
{
    [HarmonyPostfix]
    private static void Postfix(InputsManager __instance, ref Vector2 __result)
        => VoiceIdol.OverrideMove(__instance, ref __result);
}

[HarmonyPatch(typeof(PlayerMicrophone), nameof(PlayerMicrophone.ProcessAudio))]
internal static class HeldVoiceMicrophonePatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerMicrophone __instance) => VoiceIdol.RecordAudio(__instance);
}
