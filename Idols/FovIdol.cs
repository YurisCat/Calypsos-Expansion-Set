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
    private const ulong ConfigId = 0x4D49000000000003UL;

    internal static IdolDefinition Definition => new(
        "near_view", "近景雕像", ConfigId, 0x4D490003U,
        7331884207844971565UL, IdolEffectAuthority.LocalPresentation,
        () => new FovIdol())
    {
        PoolTemplateId = 13601636613369722279UL,
        CreateVisuals = prefab => StickyNoteVisuals.Create(prefab),
        ContentVersion = 1
    };

    private static FovIdol? _active;
    private HeldIdolContext _context;
    private Camera? _camera;
    private float _nativeFov;
    private float _appliedFov;
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
        if (!CanApply()) { ReleaseCamera(); return; }
        if (_active == this) return;
        _active?.ReleaseCamera();
        _active = this;
    }

    private bool CanApply()
    {
        var plugin = CalypsoExpansionSetPlugin.Instance;
        var holder = _context.Holder;
        var idol = _context.Idol;
        return !_faulted && plugin != null && !plugin.RuntimeFailed && _context.GameplayReady
            && NetworkClient.isConnected && NetworkClient.ready && plugin.Sync.ClientCompatible
            && holder != null && holder == EntityManager.Me && holder.isActiveAndEnabled
            && holder.CurrentHealthSync > 0 && holder.PlayerMovement != null && holder.PlayerMovement.isOwned
            && idol != null && idol.SoItem != null && idol.SoItem.Id == ConfigId && idol.ItemOwner == holder
            && holder.InventoryNew != null && holder.InventoryNew.InHand == idol
            && plugin.LocalGameplayReady(holder);
    }

    internal static FovIdol? BeforeNativeFov()
    {
        var effect = _active;
        if (effect == null) return null;
        try
        {
            // The native method adds offsets to Camera.fieldOfView. Remove only
            // our last write before it runs, so it never adds to the forced FOV.
            effect.ReleaseCamera();
            return effect.CanApply() ? effect : null;
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
            if (_active != effect || !effect.CanApply() || !source._ready || camera == null
                || !camera.isActiveAndEnabled || camera.orthographic || source._playerPerlinShake == null
                || source._playerPerlinShake != effect._context.Holder.playerPerlinShake || manager == null
                || (manager.LocalAnimatorState != CameraManager.ECameraAnimatorState.PLAYER_POV_STANDUP
                    && manager.LocalAnimatorState != CameraManager.ECameraAnimatorState.PLAYER_POV_CROUCH))
                return;
            effect._nativeFov = camera.fieldOfView;
            if (!float.IsFinite(effect._nativeFov)) return;
            effect._camera = camera;
            effect._appliedFov = FieldOfView;
            camera.fieldOfView = effect._appliedFov;
            effect._appliedFov = camera.fieldOfView;
        }
        catch (Exception error) { effect.Fault(error); }
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
        _faulted = true;
        if (_active == this) _active = null;
        try { ReleaseCamera(); }
        catch (Exception cleanup) { exception = new AggregateException(exception, cleanup); }
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"FOV effect disabled until released: {exception}");
    }

    public override void Dispose()
    {
        if (_active == this) _active = null;
        ReleaseCamera();
        _context = default;
    }
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
