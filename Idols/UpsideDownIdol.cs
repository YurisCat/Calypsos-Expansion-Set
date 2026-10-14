using CalypsoExpansionSet.Visuals;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using User;
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
        ShortDescription = "画面倒立，视角反向，左右对调",
        CreateVisuals = prefab => StickyNoteVisuals.Create(prefab, "todo_upside_down"),
        ContentVersion = 5
    };
    private static UpsideDownIdol? _active;
    private HeldIdolContext _context;
    private Camera? _renderCamera;
    private Quaternion _original, _applied;
    private bool _faulted, _logged;
    private float _nextLookDiagnostic;
    private float _nextMoveDiagnostic;

    public override void Start(HeldIdolContext context) { Dispose(); _context = context; _faulted = _logged = false; }
    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        Release();
        if (CanApply()) _active = this;
        else if (_active == this) _active = null;
        if (CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled != true)
        {
            _logged = false;
            _nextLookDiagnostic = 0;
            _nextMoveDiagnostic = 0;
        }
    }
    private bool CanApply() => !_faulted && LocalHeldGuard.Ready(_context, ConfigId);

    private Camera? PovCamera()
    {
        var source = SetFovShakeData.Instance;
        var manager = CameraManager.Instance;
        var camera = source?._cam;
        return CanApply() && source != null && source._ready && camera != null
            && camera.isActiveAndEnabled && !camera.orthographic
            && source._playerPerlinShake == _context.Holder.playerPerlinShake && manager != null
            && (manager.LocalAnimatorState == CameraManager.ECameraAnimatorState.PLAYER_POV_STANDUP
                || manager.LocalAnimatorState == CameraManager.ECameraAnimatorState.PLAYER_POV_CROUCH)
            ? camera : null;
    }

    internal static void FilterLook(InputsManager inputs, ref Vector2 value)
    {
        var effect = _active;
        if (effect == null || inputs != InputsManager.Instance) return;
        try
        {
            if (effect.PovCamera() == null) return;
            // Negate the already processed axes; preserve sensitivity and saved
            // invert settings. Never modify UI cursor coordinates.
            var raw = value;
            value = new Vector2(-raw.x, -raw.y);
            var plugin = CalypsoExpansionSetPlugin.Instance;
            if (plugin?.DebugTools.Enabled == true && raw != Vector2.zero
                && Time.realtimeSinceStartup >= effect._nextLookDiagnostic)
            {
                effect._nextLookDiagnostic = Time.realtimeSinceStartup + 1f;
                plugin.Log.LogInfo($"[Upside down look] raw=({raw.x:F3},{raw.y:F3}), output=({value.x:F3},{value.y:F3}).");
            }
        }
        catch (Exception error) { effect.Fault(error); }
    }

    internal static void FilterMove(InputsManager inputs, ref Vector2 value)
    {
        var effect = _active;
        if (effect == null || inputs != InputsManager.Instance) return;
        try
        {
            if (effect.PovCamera() == null || inputs.onMove == null || !inputs.onMove.enabled) return;
            var raw = value;
            value = new Vector2(-raw.x, raw.y);
            var plugin = CalypsoExpansionSetPlugin.Instance;
            if (plugin?.DebugTools.Enabled == true && raw.x != 0f
                && Time.realtimeSinceStartup >= effect._nextMoveDiagnostic)
            {
                effect._nextMoveDiagnostic = Time.realtimeSinceStartup + 1f;
                plugin.Log.LogInfo($"[Upside down move] raw=({raw.x:F3},{raw.y:F3}), output=({value.x:F3},{value.y:F3}).");
            }
        }
        catch (Exception error) { effect.Fault(error); }
    }

    internal static void Begin(Camera camera)
    {
        var effect = _active;
        if (effect == null) return;
        try
        {
            if (camera == null || effect.PovCamera() != camera) return;
            effect.Release();
            effect._original = camera.transform.localRotation;
            effect._applied = effect._original * Quaternion.AngleAxis(180f, Vector3.forward);
            effect._renderCamera = camera;
            camera.transform.localRotation = effect._applied;
            effect._applied = camera.transform.localRotation;
            if (!effect._logged && CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true)
            {
                effect._logged = true;
                CalypsoExpansionSetPlugin.Instance.Log.LogInfo($"[Upside down] 180-degree render roll on {camera.name}, both look axes inverted; HUD/cursor unchanged.");
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
        if (_faulted) return;
        _faulted = true;
        try { Release(); } catch (Exception cleanup) { error = new AggregateException(error, cleanup); }
        if (_active == this) _active = null;
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Upside-down rendering disabled until released: {error}");
    }
    public override void Dispose()
    {
        Release(); if (_active == this) _active = null; _context = default;
        _nextLookDiagnostic = _nextMoveDiagnostic = 0;
    }
}

[HarmonyPatch(typeof(InputsManager), nameof(InputsManager.Move), MethodType.Getter)]
internal static class UpsideDownMovePatch
{
    [HarmonyPostfix]
    private static void Postfix(InputsManager __instance, ref Vector2 __result)
        => UpsideDownIdol.FilterMove(__instance, ref __result);
}

[HarmonyPatch(typeof(InputsManager), nameof(InputsManager.Look), MethodType.Getter)]
internal static class UpsideDownLookPatch
{
    [HarmonyPostfix]
    private static void Postfix(InputsManager __instance, ref Vector2 __result)
        => UpsideDownIdol.FilterLook(__instance, ref __result);
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
