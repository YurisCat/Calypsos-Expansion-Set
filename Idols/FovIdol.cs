using CalypsoExpansionSet.Visuals;
using HarmonyLib;
using Mirror;
using UnityEngine;
using _200_Dev.Entities;
using _200_Dev.Entities.Player.Behaviours.Camera;

namespace CalypsoExpansionSet.Idols;

internal sealed class FovIdol : HeldIdolEffect
{
    internal const float FieldOfView = 15f;
    internal const float ZoomInSeconds = 0.45f, ZoomOutSeconds = 0.35f;
    private const ulong ConfigId = 0x4D49000000000003UL;

    internal static IdolDefinition Definition => new(
        "near_view", "近景雕像", ConfigId, 0x4D490003U,
        7331884207844971565UL, IdolEffectAuthority.LocalPresentation,
        () => new FovIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        ShortDescription = "拿起放大，注意脚下",
        CreateVisuals = prefab => new Resources(StickyNoteVisuals.Create(prefab, "handdrawn_near_view")),
        ContentVersion = 5
    };

    private static FovIdol? _active;
    private HeldIdolContext _context;
    private Camera? _camera;
    private Camera? _boundCamera;
    private float _nativeFov;
    private float _appliedFov;
    private readonly FovBlend _blend = new();
    private bool _faulted, _returning, _reported;
    private float _nextDiagnostic;

    // A short release transition can outlive HeldIdolEffect.Dispose. Registry
    // teardown must still release it, even if no camera renders another frame.
    private sealed class Resources : IDisposable, IVisualDiagnostics
    {
        private IDisposable? _note;
        internal Resources(IDisposable note) => _note = note;
        public string SetDiagnosticMode(string mode)
            => (_note as IVisualDiagnostics)?.SetDiagnosticMode(mode) ?? "No note.";
        public void Dispose()
        {
            var note = _note; _note = null;
            try { CancelActive(); } finally { note?.Dispose(); }
        }
    }

    public override void Start(HeldIdolContext context)
    {
        Cancel();
        _context = context;
        _faulted = _returning = false;
    }

    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        if (!CanApply()) { Cancel(); return; }
        if (_active == this) return;
        var previous = _active;
        var amount = previous != null && previous._context.Holder == context.Holder
            && previous.OwnsCamera() ? previous._blend.Read(Time.realtimeSinceStartup) : 0f;
        previous?.Cancel();
        _active = this;
        _returning = false;
        BeginBlend(amount, 1f, ZoomInSeconds);
    }

    private bool LocalViewReady()
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        var holder = _context.Holder;
        return !_faulted && plugin != null && !plugin.RuntimeFailed && plugin.Registry.Ready
            && NetworkClient.isConnected && NetworkClient.ready && plugin.Sync.ClientCompatible
            && holder != null && holder == EntityManager.Me && holder.isActiveAndEnabled
            && holder.CurrentHealthSync > 0 && holder.PlayerMovement != null && holder.PlayerMovement.isOwned
            && plugin.LocalGameplayReady(holder);
    }

    private bool CanApply() => !_returning && LocalViewReady() && LocalHeldGuard.Ready(_context, ConfigId);
    private bool OwnsCamera() => _camera != null && _camera.fieldOfView == _appliedFov;
    private void BeginBlend(float from, float target, float duration)
    {
        _blend.Begin(from, target, Time.realtimeSinceStartup, duration);
        _reported = false;
        _nextDiagnostic = 0;
    }

    internal static FovIdol? BeforeNativeFov()
    {
        var effect = _active;
        if (effect == null) return null;
        try
        {
            // A drop can reach the camera before the effect runtime's next
            // Update. Start the same release transition while we still own the
            // last camera write, instead of snapping back for that one frame.
            if (!effect._returning && !effect.CanApply()) effect.Dispose();
            if (_active != effect) return null;
            // The native method adds offsets to Camera.fieldOfView. Remove only
            // our last write before it runs, so it never adds to the forced FOV.
            effect.ReleaseCamera();
            if (effect._returning ? effect.LocalViewReady() : effect.CanApply()) return effect;
            effect.Cancel();
            return null;
        }
        catch (Exception exception) { effect.Fault(exception); return null; }
    }

    internal static void AfterNativeFov(SetFovShakeData source, FovIdol? effect, Exception? exception)
    {
        if (effect == null) return;
        try
        {
            if (exception != null) { effect.Fault(exception); return; }
            var manager = CameraManager.Instance;
            var camera = source._cam;
            if (_active != effect) return;
            if (!(effect._returning ? effect.LocalViewReady() : effect.CanApply()) || !source._ready || camera == null
                || !camera.isActiveAndEnabled || camera.orthographic || source._playerPerlinShake == null
                || source._playerPerlinShake != effect._context.Holder.playerPerlinShake || manager == null
                || (manager.LocalAnimatorState != CameraManager.ECameraAnimatorState.PLAYER_POV_STANDUP
                    && manager.LocalAnimatorState != CameraManager.ECameraAnimatorState.PLAYER_POV_CROUCH))
            {
                effect.Cancel();
                return;
            }
            if (effect._boundCamera == null && !effect._returning)
                effect.BeginBlend(effect._blend.StartValue, 1f, ZoomInSeconds);
            else if (effect._boundCamera != null && effect._boundCamera != camera)
            {
                if (effect._returning) { effect.Cancel(); return; }
                effect.BeginBlend(0f, 1f, ZoomInSeconds);
            }
            effect._boundCamera = camera;
            effect._nativeFov = camera.fieldOfView;
            if (!float.IsFinite(effect._nativeFov) || effect._nativeFov <= 0f) { effect.Cancel(); return; }
            var now = Time.realtimeSinceStartup;
            var amount = effect._blend.Read(now);
            var complete = effect._blend.Complete(now);
            if (effect._returning && complete)
            {
                effect.Report(now, effect._nativeFov, 0f, true);
                effect.Cancel();
                return;
            }
            effect._camera = camera;
            effect._appliedFov = effect._nativeFov + (FieldOfView - effect._nativeFov) * amount;
            camera.fieldOfView = effect._appliedFov;
            effect._appliedFov = camera.fieldOfView;
            effect.Report(now, effect._appliedFov, amount, complete);
        }
        catch (Exception error) { effect.Fault(error); }
    }

    private void Report(float now, float fov, float amount, bool complete)
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (plugin?.DebugTools.Enabled != true) { _reported = false; _nextDiagnostic = 0; return; }
        if (_reported || !complete && now < _nextDiagnostic) return;
        _nextDiagnostic = now + 0.1f;
        _reported = complete;
        plugin.Log.LogInfo($"[Near view] {(_returning ? "release" : "pickup")}: weight={amount:F3}, "
            + $"fov={fov:F2}, native={_nativeFov:F2}, complete={complete}.");
    }

    private void ReleaseCamera()
    {
        var camera = _camera;
        _camera = null;
        // A different value belongs to the game or another MOD; leave it alone.
        if (camera != null && camera.fieldOfView == _appliedFov)
            camera.fieldOfView = _nativeFov;
    }

    private void Fault(Exception exception)
    {
        if (_faulted) return;
        _faulted = true;
        try { Cancel(); }
        catch (Exception cleanup) { exception = new AggregateException(exception, cleanup); }
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"FOV effect disabled until released: {exception}");
    }

    public override void Dispose()
    {
        if (_returning) return;
        // Drop/switch/transfer may ease out for the previous local holder, while
        // death, menus, disconnect, camera replacement and unload restore now.
        var source = SetFovShakeData.Instance;
        var manager = CameraManager.Instance;
        if (_active == this && LocalViewReady() && OwnsCamera() && source != null && source._ready
            && source._cam == _camera && source._playerPerlinShake == _context.Holder.playerPerlinShake
            && manager != null && (manager.LocalAnimatorState == CameraManager.ECameraAnimatorState.PLAYER_POV_STANDUP
                || manager.LocalAnimatorState == CameraManager.ECameraAnimatorState.PLAYER_POV_CROUCH))
        {
            var amount = _blend.Read(Time.realtimeSinceStartup);
            if (amount > 0f)
            {
                _returning = true;
                BeginBlend(amount, 0f, ZoomOutSeconds);
                return;
            }
        }
        Cancel();
    }

    private void Cancel()
    {
        if (_active == this) _active = null;
        ReleaseCamera();
        _boundCamera = null;
        _blend.Clear();
        _returning = false;
    }

    internal static void CancelActive() => _active?.Cancel();
}

internal sealed class FovBlend
{
    private float _from, _target, _started, _duration;
    internal float StartValue => _from;
    internal void Begin(float from, float target, float now, float duration)
    {
        _from = Mathf.Clamp(from, 0f, 1f); _target = Mathf.Clamp(target, 0f, 1f);
        _started = now; _duration = duration;
    }
    internal float Read(float now)
    {
        if (!float.IsFinite(now) || now < _started) return 0f;
        var t = _duration > 0f ? Mathf.Clamp((now - _started) / _duration, 0f, 1f) : 1f;
        t = t * t * (3f - 2f * t);
        return _from + (_target - _from) * t;
    }
    internal bool Complete(float now) => !float.IsFinite(now) || now < _started || now - _started >= _duration;
    internal void Clear() => _from = _target = _started = _duration = 0f;
}

[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.Disconnect))]
internal static class NearViewDisconnectPatch
{
    [HarmonyPrefix] private static void Prefix() => FovIdol.CancelActive();
}

[HarmonyPatch(typeof(SetFovShakeData), "LateUpdate")]
internal static class HeldFovPatch
{
    [HarmonyPrefix]
    private static void Prefix(out FovIdol? __state) => __state = FovIdol.BeforeNativeFov();

    [HarmonyFinalizer]
    private static Exception? Finalizer(SetFovShakeData __instance, FovIdol? __state, Exception? __exception)
    {
        FovIdol.AfterNativeFov(__instance, __state, __exception);
        return __exception;
    }
}
