using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Entities.Player.Behaviours;
using UnityEngine;
using _200_Dev.EnvironmentElements.Player;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Visuals;

// Copies only transforms and mesh renderers. No prefab Instantiate: monster AI,
// player physics, networking, audio and animation-event scripts never run here.
internal sealed class VisualRigCopy : IDisposable
{
    private static readonly int ProximityDither = Shader.PropertyToID("_useProximityDither");
    private sealed record Pose(Transform Source, Transform Copy, bool WorldRelative = false);
    private sealed record Drawing(Renderer Source, Renderer Copy, bool PriorOff);
    private sealed record Driver(Transform Bone, Quaternion RotationOffset, Vector3 RestPosition, bool RootMotion);
    private readonly List<Pose> _poses = new();
    private readonly List<Drawing> _drawings = new();
    private readonly Dictionary<IntPtr, Transform> _map = new();
    private readonly MaterialPropertyBlock _block = new();
    private readonly Vector3 _stretch;
    private readonly Transform? _largeBone;
    private readonly float _boneScale;
    private readonly bool _hideSource;
    private readonly bool _suppressProximityDither;
    private readonly Transform _source;
    private Transform? _driverRoot;
    private Dictionary<IntPtr, Driver>? _drivers;
    private GameObject? _root;
    private bool _reportedVisible;
    internal Transform Root => _root!.transform;
    internal int RendererCount => _drawings.Count;
    internal bool Alive => _root != null && _source != null;

    internal VisualRigCopy(Transform source, Vector3 stretch, Transform? largeBone = null,
        float boneScale = 1, bool hideSource = true, bool suppressProximityDither = false)
    {
        _source = source; _stretch = stretch; _largeBone = largeBone;
        _boneScale = boneScale; _hideSource = hideSource; _suppressProximityDither = suppressProximityDither;
        try
        {
            _root = new GameObject("Calypso visual-only rig");
            _root.SetActive(false);
            _root.hideFlags = HideFlags.DontSave;
            UObject.DontDestroyOnLoad(_root);
            CopyTree(source, _root.transform, 0);
            var hierarchyCount = _poses.Count;
            for (var poseIndex = 0; poseIndex < hierarchyCount; poseIndex++)
            {
                var pose = _poses[poseIndex];
                var skin = pose.Source.GetComponent<SkinnedMeshRenderer>();
                if (skin != null && skin.sharedMesh != null)
                {
                    var copy = pose.Copy.gameObject.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh = skin.sharedMesh;
                    var bones = skin.bones;
                    var mapped = new Transform[bones.Length];
                    for (var index = 0; index < bones.Length; index++)
                        mapped[index] = bones[index] != null ? EnsureBone(bones[index], 0) : null!;
                    copy.bones = new Il2CppReferenceArray<Transform>(mapped);
                    copy.rootBone = skin.rootBone != null ? EnsureBone(skin.rootBone, 0) : null;
                    copy.quality = skin.quality;
                    copy.updateWhenOffscreen = true;
                    var bounds = skin.localBounds;
                    bounds.size *= Math.Max(1f, boneScale);
                    copy.localBounds = bounds;
                    CopyRenderer(skin, copy);
                    continue;
                }
                var renderer = pose.Source.GetComponent<MeshRenderer>();
                var filter = pose.Source.GetComponent<MeshFilter>();
                if (renderer == null || filter == null || filter.sharedMesh == null) continue;
                pose.Copy.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                CopyRenderer(renderer, pose.Copy.gameObject.AddComponent<MeshRenderer>());
            }
            if (_drawings.Count == 0) throw new InvalidOperationException("The visual hierarchy has no supported mesh renderers.");
            SyncPose();
            if (CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true)
                CalypsoExpansionSetPlugin.Instance.Log.LogInfo($"[Visual rig] renderers={_drawings.Count}, transforms={_poses.Count}; visual-only copy, no physics/network behaviours.");
        }
        catch { Dispose(); throw; }
    }

    private Transform? Map(Transform? value) => value != null && _map.TryGetValue(value.Pointer, out var copy) ? copy : null;

    // Native clothing/character rigs can bind to an animator outside their
    // visible model. Recreate only the required transform chains, never that
    // external object's components, meshes or complete sibling hierarchy.
    private Transform EnsureBone(Transform source, int depth)
    {
        var existing = Map(source);
        if (existing != null) return existing;
        if (depth > 128 || _poses.Count >= 4096) throw new InvalidOperationException("External bone hierarchy exceeds its safety bounds.");
        var parent = source.parent;
        var relative = parent == null || _source.IsChildOf(parent);
        var targetParent = relative ? Root : EnsureBone(parent!, depth + 1);
        var created = new GameObject(source.name) { hideFlags = HideFlags.DontSave };
        created.SetActive(false);
        created.transform.SetParent(targetParent, false);
        _map.Add(source.Pointer, created.transform);
        _poses.Add(new Pose(source, created.transform, relative));
        return created.transform;
    }

    private void CopyTree(Transform source, Transform copy, int depth)
    {
        if (depth > 128 || _poses.Count >= 4096) throw new InvalidOperationException("Visual hierarchy exceeds its safety bounds.");
        copy.gameObject.layer = source.gameObject.layer;
        if (depth != 0) copy.gameObject.name = source.name;
        copy.localPosition = source.localPosition;
        copy.localRotation = source.localRotation;
        copy.localScale = source.localScale;
        _map.Add(source.Pointer, copy);
        _poses.Add(new Pose(source, copy));
        for (var index = 0; index < source.childCount; index++)
        {
            var child = source.GetChild(index);
            // Held items remain native objects with their own pose/lifetime.
            if (IsSeparateItem(child, _source)) continue;
            var created = new GameObject(child.name) { hideFlags = HideFlags.DontSave };
            created.SetActive(false);
            created.transform.SetParent(copy, false);
            CopyTree(child, created.transform, depth + 1);
        }
    }

    private void CopyRenderer(Renderer source, Renderer copy)
    {
        copy.sharedMaterials = source.sharedMaterials;
        copy.shadowCastingMode = source.shadowCastingMode;
        copy.receiveShadows = source.receiveShadows;
        copy.lightProbeUsage = source.lightProbeUsage;
        copy.reflectionProbeUsage = source.reflectionProbeUsage;
        copy.enabled = source.enabled;
        _drawings.Add(new Drawing(source, copy, source.forceRenderingOff));
    }

    // The live skeleton owns a corpse Item on root.x. Only this rig's own
    // ragdoll belongs to it; a carried corpse must remain a separate visual.
    internal static bool IsSeparateItem(Transform value, Transform hierarchy)
    {
        var rig = hierarchy.GetComponent<PlayerRigConfigWorld>();
        if (rig != null)
        {
            var owner = value.GetComponentInParent<PlayerRigConfigWorld>(true);
            if (owner != null && owner != rig) return true;
        }
        var item = value.GetComponentInParent<Items.Item>(true);
        if (item == null || item.transform == hierarchy || !item.transform.IsChildOf(hierarchy)) return false;
        var corpse = item.TryCast<DeadPlayerTool>();
        var ragdoll = rig != null ? rig.deadRagDollControllerUpgraded : null;
        return corpse == null || ragdoll == null || ragdoll.deadPlayerTool != corpse;
    }

    internal Transform? CopiedTransform(Transform source) => Map(source);

    // A disguise uses another character's meshes, but this player's animation.
    // Bind once when the rig is ready; no hierarchy scans occur while rendering.
    internal void BindPoseTo(Transform root, Transform reference)
    {
        static Dictionary<string, Transform?> Names(Transform hierarchy)
        {
            var result = new Dictionary<string, Transform?>(StringComparer.Ordinal);
            foreach (var transform in hierarchy.GetComponentsInChildren<Transform>(true))
            {
                if (IsSeparateItem(transform, hierarchy)) continue;
                if (!result.TryAdd(transform.name, transform)) result[transform.name] = null;
            }
            return result;
        }
        var names = Names(root);
        var rest = Names(reference);
        var drivers = new Dictionary<IntPtr, Driver>();
        var sourceRotation = Quaternion.Inverse(_source.rotation);
        var referenceRotation = Quaternion.Inverse(reference.rotation);
        foreach (var pose in _poses)
        {
            if (pose.Source == _source || !names.TryGetValue(pose.Source.name, out var driver) || driver == null
                || !rest.TryGetValue(pose.Source.name, out var bind) || bind == null) continue;
            var offset = Quaternion.Inverse(referenceRotation * bind.rotation) * sourceRotation * pose.Source.rotation;
            drivers[pose.Source.Pointer] = new Driver(driver, offset, reference.InverseTransformPoint(bind.position),
                pose.Source.name == "root.x");
        }
        // Face rigs differ across characters. Unmatched facial bones keep the
        // donor's neutral pose under its animated head; locomotion must bind.
        foreach (var name in new[] { "root.x", "head.x", "hand.l", "hand.r", "foot.l", "foot.r" })
            if (!names.TryGetValue(name, out var bone) || bone == null || !rest.ContainsKey(name)
                || !_poses.Any(pose => pose.Source.name == name && drivers.ContainsKey(pose.Source.Pointer)))
                throw new InvalidOperationException($"Cannot bind disguise locomotion bone '{name}'.");
        _driverRoot = root;
        _drivers = drivers;
    }

    internal Renderer? CopiedRenderer(Renderer source) =>
        _drawings.FirstOrDefault(drawing => drawing.Source == source)?.Copy;

    internal bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        var found = false;
        foreach (var drawing in _drawings)
        {
            var renderer = drawing.Copy;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            found = true;
        }
        return found;
    }

    internal void SyncPose()
    {
        if (_root == null || _source == null) return;
        var root = _driverRoot != null ? _driverRoot : _source;
        foreach (var pose in _poses)
        {
            if (pose.Source == null || pose.Copy == null) continue;
            var source = pose.Source;
            if (pose.Source == _source)
            {
                pose.Copy.SetPositionAndRotation(root.position, root.rotation);
                pose.Copy.localScale = Vector3.Scale(root.lossyScale, _stretch);
            }
            else
            {
                if (_drivers != null && _driverRoot != null && _drivers.TryGetValue(source.Pointer, out var driver))
                {
                    // Retarget rotation deltas in rig space. Copying local bone
                    // positions would deform a donor onto the wearer's anatomy.
                    pose.Copy.localPosition = source.localPosition;
                    pose.Copy.localScale = source.localScale;
                    pose.Copy.rotation = Root.rotation * Quaternion.Inverse(_driverRoot.rotation) * driver.Bone.rotation * driver.RotationOffset;
                    if (driver.RootMotion)
                    {
                        var movement = _driverRoot.InverseTransformPoint(driver.Bone.position) - driver.RestPosition;
                        pose.Copy.position = Root.TransformPoint(_source.InverseTransformPoint(source.position) + movement);
                    }
                    if (source == _largeBone) pose.Copy.localScale *= _boneScale;
                    pose.Copy.gameObject.SetActive(source.gameObject.activeSelf);
                    continue;
                }
                if (pose.WorldRelative)
                {
                    pose.Copy.localPosition = root.InverseTransformPoint(source.position);
                    pose.Copy.localRotation = Quaternion.Inverse(root.rotation) * source.rotation;
                    var size = source.lossyScale;
                    var origin = root.lossyScale;
                    if (Math.Abs(origin.x) < 0.00001f || Math.Abs(origin.y) < 0.00001f || Math.Abs(origin.z) < 0.00001f)
                        throw new InvalidOperationException("Visible rig has a degenerate scale.");
                    pose.Copy.localScale = new Vector3(size.x / origin.x, size.y / origin.y, size.z / origin.z);
                }
                else
                {
                    pose.Copy.localPosition = source.localPosition;
                    pose.Copy.localRotation = source.localRotation;
                    pose.Copy.localScale = source.localScale;
                }
                if (pose.Source == _largeBone) pose.Copy.localScale *= _boneScale;
                pose.Copy.gameObject.SetActive(pose.Source.gameObject.activeSelf);
            }
        }
        SyncDrawing();
        _root.SetActive(root.gameObject.activeInHierarchy);
        if (!_reportedVisible && _root.activeInHierarchy && CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true)
        {
            _reportedVisible = true;
            var drawing = _drawings[0];
            CalypsoExpansionSetPlugin.Instance.Log.LogInfo($"[Visual rig visible] source={_source.name}, scale={Root.lossyScale}, sourceBounds={drawing.Source.bounds.size}, copyBounds={drawing.Copy.bounds.size}, sourceEnabled={drawing.Source.enabled}, copyEnabled={drawing.Copy.enabled}, priorOff={drawing.PriorOff}.");
        }
    }

    internal void ShowAt(Vector3 position, Quaternion rotation, Vector3 scale, bool visible, int layer)
    {
        if (_root == null) return;
        Root.SetPositionAndRotation(position, rotation);
        Root.localScale = scale;
        foreach (var pose in _poses)
        {
            pose.Copy.gameObject.layer = layer;
            if (pose.Source != _source) pose.Copy.gameObject.SetActive(pose.Source.gameObject.activeSelf);
        }
        SyncDrawing();
        _root.SetActive(visible);
    }

    private void SyncDrawing()
    {
        foreach (var drawing in _drawings)
        {
            if (drawing.Source == null || drawing.Copy == null) continue;
            drawing.Copy.enabled = drawing.Source.enabled && !drawing.PriorOff;
            drawing.Source.GetPropertyBlock(_block);
            // Template hair enables camera-proximity dithering. A third-person
            // disguise keeps its cutout/opacity, without that proximity fade.
            if (_suppressProximityDither) _block.SetFloat(ProximityDither, 0f);
            drawing.Copy.SetPropertyBlock(_block);
            if (_hideSource) drawing.Source.forceRenderingOff = true;
            var skin = drawing.Source.TryCast<SkinnedMeshRenderer>();
            var target = drawing.Copy.TryCast<SkinnedMeshRenderer>();
            if (skin == null || target == null || skin.sharedMesh == null) continue;
            for (var index = 0; index < skin.sharedMesh.blendShapeCount; index++)
                target.SetBlendShapeWeight(index, skin.GetBlendShapeWeight(index));
        }
    }

    public void Dispose()
    {
        if (_hideSource)
            foreach (var drawing in _drawings)
                if (drawing.Source != null && drawing.Source.forceRenderingOff)
                    drawing.Source.forceRenderingOff = drawing.PriorOff;
        if (_root != null)
        {
            _root.SetActive(false);
            UObject.Destroy(_root);
        }
        _root = null;
        _poses.Clear(); _drawings.Clear(); _map.Clear();
        _drivers?.Clear(); _drivers = null; _driverRoot = null;
    }
}
