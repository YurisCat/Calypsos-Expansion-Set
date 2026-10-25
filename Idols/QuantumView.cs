using CalypsoExpansionSet.Visuals;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Items;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering;
using User;

namespace CalypsoExpansionSet.Idols;

// Only the normal scene camera observes. Scan/ping masks, icons and arrows never
// enter this geometric test, and world colliders still block the actual idol.
internal sealed class QuantumView
{
    private readonly Il2CppStructArray<RaycastHit> _hits = new(32);
    private QuantumFrustum _frustum;
    private Camera? _camera;
    private IntPtr _viewer;
    private bool _inCamera;
    private int _mask;
    internal Vector3 Origin { get; private set; }
    internal QuantumCamera Projection { get; private set; }
    internal double Sampled { get; private set; } = double.NegativeInfinity;
    internal bool Ready => _camera != null && _camera == SetFovShakeData.Instance?._cam
        && _camera.isActiveAndEnabled && _camera.targetTexture == null
        && UserInstance.Me?.playerEntity?.Pointer == _viewer
        && !Testing.TestRoomAppearanceView.IsInspecting
        && CalypsoExpansionSetPlugin.Instance?.DebugTools.PanelOpen == false;

    internal bool Begin(Camera camera)
    {
        if (camera == null || camera != SetFovShakeData.Instance?._cam) return false;
        _inCamera = false; Sampled = double.NegativeInfinity;
        var plugin = CalypsoExpansionSetPlugin.Instance;
        var player = UserInstance.Me?.playerEntity;
        if (!camera.isActiveAndEnabled
            || camera.targetTexture != null || player == null || player.PlayerInteraction == null
            || Testing.TestRoomAppearanceView.IsInspecting || plugin?.DebugTools.PanelOpen != false) return false;
        _camera = camera; _viewer = player.Pointer; Origin = camera.transform.position;
        var m = camera.projectionMatrix * camera.worldToCameraMatrix;
        for (var i = 0; i < 16; i++) if (!float.IsFinite(m[i])) return false;
        Projection = new(new(m.m00, m.m01, m.m02, m.m03), new(m.m10, m.m11, m.m12, m.m13),
            new(m.m20, m.m21, m.m22, m.m23), new(m.m30, m.m31, m.m32, m.m33));
        if (!Projection.Valid) return false;
        _frustum = Projection.Frustum;
        _mask = player.PlayerInteraction._occlusionLayerMask;
        _inCamera = true;
        return true;
    }

    internal bool Finish(Camera camera)
    {
        if (!_inCamera || camera != _camera) return false;
        _inCamera = false; Sampled = NetworkTime.time; return true;
    }
    internal void Reset() { _inCamera = false; _camera = null; _viewer = IntPtr.Zero; Sampled = double.NegativeInfinity; }

    // Panini and lens distortion resample this camera's rendered color image.
    // They cannot draw real idol geometry clipped out of its source frustum.
    // The source frustum, with its small edge margin, is a finite conservative
    // bound even when post-processing crops or warps pixels inside that image.
    internal bool MaySee(Bounds bounds) => _frustum.MayContain(Point(bounds.center), Point(bounds.extents));

    internal bool Sees(ScoreTool idol, Bounds bounds)
    {
        if (!MaySee(bounds)) return false;
        // Center and a 3x3 grid on each horizontal side retain partial visibility.
        if (Unobstructed(idol, bounds.center)) return true;
        var e = bounds.extents * 0.95f;
        for (var y = -1; y <= 1; y++)
            for (var side = 0; side < 8; side++)
            {
                var angle = side * Mathf.PI * 0.25f;
                var point = bounds.center + new Vector3(Mathf.Cos(angle) * e.x, y * e.y, Mathf.Sin(angle) * e.z);
                if (Unobstructed(idol, point)) return true;
            }
        return false;
    }

    private bool Unobstructed(ScoreTool idol, Vector3 target) =>
        ClearRay(_hits, Origin, target, _mask, idol);

    internal static bool ClearRay(Il2CppStructArray<RaycastHit> buffer, Vector3 origin, Vector3 target, int mask, ScoreTool idol)
    {
        var delta = target - origin; var distance = delta.magnitude;
        if (distance < 0.02f) return true;
        var count = Physics.RaycastNonAlloc(new Ray(origin, delta / distance), buffer, distance, mask, QueryTriggerInteraction.Ignore);
        if (count >= buffer.Length) return false;
        for (var i = 0; i < count; i++)
        {
            var hit = buffer[i]; var collider = hit.collider;
            if (collider == null || hit.distance >= distance - 0.03f
                || collider.transform.IsChildOf(idol.transform)
                || collider.GetComponentInParent<Entities.Entity>() != null) continue;
            return false;
        }
        return true;
    }

    internal static QuantumPoint Point(Vector3 p) => new(p.x, p.y, p.z);
    internal static Vector3 Vector(QuantumPoint p) => new(p.X, p.Y, p.Z);
    internal static bool BoundsFor(ScoreTool idol, out Bounds bounds)
    {
        bounds = default;
        var body = idol.SM_Body; var head = idol.SM_Head;
        if (body == null || head == null || !body.enabled || !head.enabled || body.forceRenderingOff || head.forceRenderingOff) return false;
        bounds = body.bounds; bounds.Encapsulate(head.bounds); bounds.Expand(0.08f);
        return Point(bounds.center).Valid && Point(bounds.extents).Valid && bounds.size.sqrMagnitude > 0.001f;
    }
}

[HarmonyPatch(typeof(HeldCameraOverrides), nameof(HeldCameraOverrides.BeginRender))]
internal static class QuantumCameraBeginPatch
{ private static void Postfix(Camera camera) => QuantumIdolRuntime.Capture(camera); }
[HarmonyPatch(typeof(RenderPipelineManager), "EndCameraRendering")]
internal static class QuantumCameraEndPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First + 1)]
    private static void Prefix(Camera camera) => QuantumIdolRuntime.Finish(camera);
}
