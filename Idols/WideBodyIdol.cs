using CalypsoExpansionSet.Visuals;
using Entities.Player.Behaviours;
using HarmonyLib;
using UnityEngine;
using User;
using _200_Dev.Entities.Player.Behaviours.Camera;

namespace CalypsoExpansionSet.Idols;

internal sealed class WideBodyIdol : HeldIdolEffect
{
    internal const ulong ConfigId = 0x4D4900000000000BUL;
    internal const float CameraDrop = 0.25f, HorizontalStretch = 1.18f;
    internal static IdolDefinition Definition => new(
        "wide_body", "宽体雕像", ConfigId, 0x4D49000BU, 7331884207844971565UL,
        IdolEffectAuthority.LocalPresentation, () => new WideBodyIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        ShortDescription = "压缩拉长，宽体横梁",
        ContentVersion = 5,
        CreateVisuals = prefab => StickyNoteVisuals.Create(prefab, "handdrawn_wide_body"),
        CreateRuntime = () => new HeldAppearanceRuntime(ConfigId,
            player => StretchedPlayerAppearance.Create(player, new Vector3(1.8f, 0.65f, 1f), 1f))
    };
    private static WideBodyIdol? _active;
    private HeldIdolContext _context;
    private Camera? _camera;
    private Vector3 _beforePosition, _writtenPosition;
    private Matrix4x4 _beforeProjection, _writtenProjection;
    private Camera.ProjectionMatrixMode _beforeProjectionMode;
    private bool _faulted;

    public override void Start(HeldIdolContext context) { Dispose(); _context = context; _faulted = false; }
    public override void Tick(HeldIdolContext context)
    {
        HeldCameraOverrides.End();
        Release();
        _context = context;
        if (!_faulted && LocalHeldGuard.Ready(context, ConfigId)) _active = this;
        else if (_active == this) _active = null;
    }

    private bool IsPov(Camera camera)
    {
        var source = SetFovShakeData.Instance;
        var manager = CameraManager.Instance;
        return !_faulted && !Testing.TestRoomAppearanceView.IsInspecting
            && LocalHeldGuard.Ready(_context, ConfigId) && source != null && source._ready
            && source._cam == camera && source._playerPerlinShake == _context.Holder.playerPerlinShake
            && manager != null && (manager.LocalAnimatorState == CameraManager.ECameraAnimatorState.PLAYER_POV_STANDUP
                || manager.LocalAnimatorState == CameraManager.ECameraAnimatorState.PLAYER_POV_CROUCH);
    }

    internal static void Begin(Camera camera)
    {
        var effect = _active;
        if (effect == null) return;
        try
        {
            if (camera == null || !effect.IsPov(camera)) return;
            effect.Release();
            effect._beforePosition = camera.transform.position;
            effect._beforeProjectionMode = camera.projectionMatrixMode;
            effect._beforeProjection = camera.projectionMatrix;
            effect._writtenPosition = effect._beforePosition;
            effect._writtenProjection = effect._beforeProjection;
            effect._camera = camera;
            camera.transform.position = effect._writtenPosition = effect._beforePosition + Vector3.down * CameraDrop;
            effect._writtenProjection.m00 *= HorizontalStretch;
            camera.projectionMatrix = effect._writtenProjection;
        }
        catch (Exception error) { effect.Fault(error); }
    }

    internal static void End(Camera? camera = null)
    {
        var effect = _active;
        if (effect == null || camera != null && effect._camera != camera) return;
        try { effect.Release(); }
        catch (Exception error) { effect.Fault(error); }
    }

    private void Release()
    {
        var camera = _camera; _camera = null;
        if (camera == null) return;
        if (camera.transform.position == _writtenPosition) camera.transform.position = _beforePosition;
        if (camera.projectionMatrixMode == Camera.ProjectionMatrixMode.Explicit
            && camera.projectionMatrix == _writtenProjection)
        {
            // Restoring a matrix value still leaves Unity in explicit projection
            // mode. Automatic cameras must resume using their current parameters.
            if (_beforeProjectionMode == Camera.ProjectionMatrixMode.Explicit) camera.projectionMatrix = _beforeProjection;
            else if (_beforeProjectionMode == Camera.ProjectionMatrixMode.PhysicalPropertiesBased) camera.usePhysicalProperties = true;
            else camera.ResetProjectionMatrix();
        }
    }

    private void Fault(Exception error)
    {
        if (_faulted) return;
        _faulted = true;
        try { Release(); } catch (Exception cleanup) { error = new AggregateException(error, cleanup); }
        if (_active == this) _active = null;
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Wide-body camera disabled until released: {error}");
    }

    public override void Dispose()
    {
        HeldCameraOverrides.End();
        Release();
        if (_active == this) _active = null;
        _context = default;
    }
}

// Native hover uses the output camera as well. Give it the same temporary
// position/projection as rendering so the lowered view keeps its aim aligned.
[HarmonyPatch(typeof(PlayerInteraction), "LateUpdate")]
internal static class WideBodyInteractionCameraPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerInteraction __instance, out Camera? __state)
    {
        __state = null;
        var player = _200_Dev.Entities.EntityManager.Me;
        if (player == null || player.PlayerInteraction != __instance || !__instance.isOwned) return;
        var camera = SetFovShakeData.Instance?._cam;
        if (camera == null) return;
        __state = camera;
        HeldCameraOverrides.BeginInteraction(camera);
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Camera? __state, Exception? __exception)
    {
        if (__state != null) HeldCameraOverrides.End(__state);
        return __exception;
    }
}
