using CalypsoExpansionSet.Visuals;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using _200_Dev.Entities.Player.Behaviours.Camera;

namespace CalypsoExpansionSet.Idols;

internal sealed class UpsideDownIdol : HeldIdolEffect
{
    internal const ulong ConfigId = 0x4D49000000000008UL;
    internal static IdolDefinition Definition => new(
        "upside_down", "倒立雕像", ConfigId, 0x4D490008U,
        7331884207844971565UL, IdolEffectAuthority.LocalPresentation, () => new UpsideDownIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        CreateVisuals = StickyNoteVisuals.Create,
        ContentVersion = 2
    };
    private static UpsideDownIdol? _active;
    private HeldIdolContext _context;
    private Camera? _renderCamera;
    private Quaternion _original, _applied;
    private bool _faulted, _logged;

    public override void Start(HeldIdolContext context) { Dispose(); _context = context; _faulted = _logged = false; }
    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        Release();
        if (CanApply()) _active = this;
        else if (_active == this) _active = null;
    }
    private bool CanApply() => !_faulted && LocalHeldGuard.Ready(_context, ConfigId);

    internal static void Begin(Camera camera)
    {
        var effect = _active;
        if (effect == null) return;
        try
        {
            var source = SetFovShakeData.Instance;
            var manager = CameraManager.Instance;
            if (!effect.CanApply() || camera == null || !camera.isActiveAndEnabled || camera.orthographic
                || source == null || !source._ready || source._cam != camera
                || source._playerPerlinShake != effect._context.Holder.playerPerlinShake || manager == null
                || (manager.LocalAnimatorState != CameraManager.ECameraAnimatorState.PLAYER_POV_STANDUP
                    && manager.LocalAnimatorState != CameraManager.ECameraAnimatorState.PLAYER_POV_CROUCH)) return;
            effect.Release();
            effect._original = camera.transform.localRotation;
            effect._applied = effect._original * Quaternion.AngleAxis(180f, Vector3.forward);
            effect._renderCamera = camera;
            camera.transform.localRotation = effect._applied;
            effect._applied = camera.transform.localRotation;
            if (!effect._logged && CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true)
            {
                effect._logged = true;
                CalypsoExpansionSetPlugin.Instance.Log.LogInfo($"[Upside down] scoped 180-degree render roll on {camera.name}; restored before next gameplay update.");
            }
        }
        catch (Exception error) { effect.Fault(error); }
    }

    internal static void End(Camera? camera = null)
    {
        var effect = _active;
        if (effect == null || camera != null && effect._renderCamera != camera) return;
        try { effect.Release(); }
        catch (Exception error) { effect.Fault(error); }
    }
    private void Release()
    {
        var camera = _renderCamera; _renderCamera = null;
        if (camera != null && camera.transform.localRotation == _applied)
            camera.transform.localRotation = _original;
    }
    private void Fault(Exception error)
    {
        _faulted = true;
        try { Release(); } catch (Exception cleanup) { error = new AggregateException(error, cleanup); }
        if (_active == this) _active = null;
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Upside-down rendering disabled until released: {error}");
    }
    public override void Dispose() { Release(); if (_active == this) _active = null; _context = default; }
}

// Apply only while the world camera is rendering, not while input/physics run.
[HarmonyPatch(typeof(RenderPipelineManager), "BeginCameraRendering")]
internal static class UpsideDownRenderBeginPatch
{
    [HarmonyPostfix] private static void Postfix(Camera camera) => UpsideDownIdol.Begin(camera);
}
[HarmonyPatch(typeof(RenderPipelineManager), "EndCameraRendering")]
internal static class UpsideDownRenderEndPatch
{
    [HarmonyPrefix] private static void Prefix(Camera camera) => UpsideDownIdol.End(camera);
}
[HarmonyPatch(typeof(RenderPipelineManager), "DoRenderLoop_Internal")]
internal static class UpsideDownRenderFinallyPatch
{
    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception) { UpsideDownIdol.End(); return __exception; }
}
