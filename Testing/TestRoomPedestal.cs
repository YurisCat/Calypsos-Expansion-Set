using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CalypsoExpansionSet.Testing;

internal sealed class TestRoomPedestal : IDisposable
{
    // Original Addressables GUIDs verified in the game catalog and asset containers.
    private const string ModelKey = "63890c5622abc2b4a8138c2dcfbebf52";
    private const string MaterialKey = "73e95878aab21ce4e8a1c420ccf81f58";
    private AsyncOperationHandle<GameObject>? _modelHandle;
    private AsyncOperationHandle<Material>? _materialHandle;
    private Mesh _mesh = null!;
    private Material _material = null!;
    private Bounds _bounds;
    private bool _ready;
    internal float Height => _bounds.size.y;
    internal float Depth => _bounds.size.z;

    internal TestRoomPedestal()
    {
        // The start room does not load this prop. Let the game's Addressables
        // resolve dependencies, and retain both handles until our room is removed.
        try
        {
            _modelHandle = Addressables.LoadAssetAsync<GameObject>((Il2CppSystem.String)ModelKey);
            _materialHandle = Addressables.LoadAssetAsync<Material>((Il2CppSystem.String)MaterialKey);
        }
        catch { Dispose(); throw; }
    }

    internal bool Ready()
    {
        if (_ready) return true;
        if (_modelHandle == null || _materialHandle == null)
            throw new ObjectDisposedException(nameof(TestRoomPedestal));
        if ((_modelHandle.IsDone && _modelHandle.Status != AsyncOperationStatus.Succeeded)
            || (_materialHandle.IsDone && _materialHandle.Status != AsyncOperationStatus.Succeeded))
            throw new InvalidOperationException("Could not load the game's SM_SocleStatue / M_SocleStatue Addressables assets.");
        if (!_modelHandle.IsDone || !_materialHandle.IsDone) return false;
        var source = _modelHandle.Result?.GetComponentInChildren<MeshFilter>(true);
        if (source == null || source.sharedMesh == null || source.sharedMesh.name != "SM_SocleStatue"
            || _materialHandle.Result == null || _materialHandle.Result.name != "M_SocleStatue")
            throw new InvalidOperationException("The original pedestal asset names no longer match this game version.");
        _mesh = source.sharedMesh;
        _material = _materialHandle.Result;
        _bounds = _mesh.bounds;
        if (!float.IsFinite(Height) || Height <= 0 || Height > 1.5f
            || !float.IsFinite(_bounds.size.x) || !float.IsFinite(Depth)
            || _bounds.size.x <= 0 || Depth <= 0
            || _bounds.size.x + 0.7f > TestRoomLayout.Spacing
            || Depth + 0.7f > TestRoomLayout.RowSpacing)
            throw new InvalidOperationException($"The native pedestal bounds {_bounds} do not fit the gallery's walking aisles.");
        _ready = true;
        return true;
    }

    internal void Create(Transform parent, int layer, string name, float x, float z)
    {
        if (!_ready) throw new InvalidOperationException("Wait for pedestal resources before building the gallery.");
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(x, 0, z);
        root.layer = layer;
        var visual = new GameObject("SM_SocleStatue");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(-_bounds.center.x, -_bounds.min.y, -_bounds.center.z);
        visual.layer = layer;
        visual.AddComponent<MeshFilter>().sharedMesh = _mesh;
        visual.AddComponent<MeshRenderer>().sharedMaterial = _material;
        // The original decorative prefab has no collider. Give each own display
        // a flat, static support; its mesh/material remain the game's resources.
        var support = root.AddComponent<BoxCollider>();
        support.center = new Vector3(0, Height / 2, 0);
        support.size = _bounds.size;
    }

    public void Dispose()
    {
        _ready = false;
        _mesh = null!;
        _material = null!;
        if (_modelHandle != null && _modelHandle.IsValid()) Addressables.Release(_modelHandle);
        if (_materialHandle != null && _materialHandle.IsValid()) Addressables.Release(_materialHandle);
        _modelHandle = null;
        _materialHandle = null;
    }
}
