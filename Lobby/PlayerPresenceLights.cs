using CalypsoExpansionSet.QualityOfLife;
using EnvironmentElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using _200_Dev.Portrait;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Lobby;

// Each client owns only its extra meshes/material. Native portrait materials,
// skulls, nickname layout and voice animation remain owned by the game.
internal sealed class PlayerPresenceLights : IDisposable
{
    internal static readonly Color InsideColor = new(0.05f, 0.9f, 1f, 1f);
    internal static readonly Color OutsideColor = new(0.16f, 0.18f, 0.2f, 1f);
    private sealed record Lamp(PlayerPortrait Portrait, Renderer Source, GameObject Root, MeshRenderer Renderer)
    { internal bool? Inside; }
    private readonly CalypsoExpansionSetPlugin _plugin;
    private readonly Dictionary<int, Lamp> _lamps = new();
    private readonly HashSet<int> _seen = new();
    private readonly List<int> _removed = new();
    private readonly MaterialPropertyBlock _block = new();
    private Material? _material;
    private Mesh? _mesh;
    private Room? _room;
    private float _nextUpdate;
    private bool _disposed, _failed;

    internal PlayerPresenceLights(CalypsoExpansionSetPlugin plugin) => _plugin = plugin;

    internal void Tick()
    {
        if (_disposed || _failed) return;
        try
        {
            var room = Mirror.NetworkClient.ready ? Room.GetStartRoom() : null;
            if (_room != room) { Clear(); _room = room; }
            var portraits = room != null ? PortraitManager.Instance?.Portrait : null;
            if (portraits == null || _plugin.RuntimeFailed || !Mirror.NetworkClient.isConnected)
            { Clear(); return; }
            if (Time.realtimeSinceStartup < _nextUpdate) return;
            _nextUpdate = Time.realtimeSinceStartup + 0.1f;
            _seen.Clear();
            for (var index = 0; index < portraits.Count; index++)
            {
                var portrait = portraits[index];
                var owner = portrait != null ? portrait.CurrentPortraitOwner : null;
                var source = portrait != null ? portrait.portraitRenderer : null;
                if (portrait == null || !portrait.isActiveAndEnabled || source == null || owner == null) continue;
                var key = portrait.GetInstanceID();
                _seen.Add(key);
                if (_lamps.TryGetValue(key, out var lamp)
                    && (lamp.Portrait != portrait || lamp.Source != source || lamp.Root == null))
                { Release(lamp); _lamps.Remove(key); lamp = null; }
                if (lamp == null) _lamps[key] = lamp = Create(portrait, source);
                bool inside = false;
                var known = _plugin.RoomStatus?.InCurrentStartRoom(owner, room!, out inside) == true;
                var visible = known && source.enabled && source.gameObject.activeInHierarchy;
                lamp.Root.SetActive(visible);
                if (!visible) continue;
                Position(lamp);
                if (lamp.Inside == inside) continue;
                _block.Clear();
                _block.SetColor("_Color", inside ? InsideColor : OutsideColor);
                lamp.Renderer.SetPropertyBlock(_block);
                lamp.Inside = inside;
            }
            _removed.Clear();
            foreach (var key in _lamps.Keys) if (!_seen.Contains(key)) _removed.Add(key);
            foreach (var key in _removed) { Release(_lamps[key]); _lamps.Remove(key); }
        }
        catch (Exception error)
        {
            _failed = true;
            Clear();
            _plugin.Log.LogError($"Player presence lights disabled after an error; native portraits remain active: {error}");
        }
    }

    private Lamp Create(PlayerPortrait portrait, Renderer source)
    {
        EnsureResources();
        var root = new GameObject("Calypso Player Presence")
        { layer = source.gameObject.layer, hideFlags = HideFlags.DontSave };
        try
        {
            root.SetActive(false);
            root.transform.SetParent(source.transform, false);
            root.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return new Lamp(portrait, source, root, renderer);
        }
        catch { UObject.Destroy(root); throw; }
    }

    private static void Position(Lamp lamp)
    {
        var bounds = lamp.Source.localBounds;
        var extent = bounds.extents;
        // Choose the portrait plane by its thinnest local axis. A sphere is
        // visible on either face, without guessing which way the photo faces.
        var normal = extent.x <= extent.y && extent.x <= extent.z ? 0 : extent.y <= extent.z ? 1 : 2;
        var horizontal = normal == 0 ? 2 : 0;
        var vertical = normal == 1 ? 2 : 1;
        var position = bounds.center;
        // Center above the photo with clearance for its native frame.
        position[vertical] += extent[vertical] * 1.5f;
        lamp.Root.transform.localPosition = position;
        var scale = lamp.Source.transform.lossyScale;
        var radius = Math.Min(extent[horizontal] * Mathf.Abs(scale[horizontal]),
            extent[vertical] * Mathf.Abs(scale[vertical])) * 0.13f;
        if (!float.IsFinite(radius) || radius <= 0) throw new InvalidOperationException("Portrait has invalid bounds.");
        lamp.Root.transform.localScale = new Vector3(radius / Math.Max(0.0001f, Mathf.Abs(scale.x)),
            radius / Math.Max(0.0001f, Mathf.Abs(scale.y)), radius / Math.Max(0.0001f, Mathf.Abs(scale.z)));
    }

    private void EnsureResources()
    {
        if (_material == null)
        {
            // UI/Default is already used by the native UI. White vertex colors
            // and texture make its _Color a light-independent per-instance tint.
            _material = new Material(Graphic.defaultGraphicMaterial)
            { name = "Calypso Presence Light", hideFlags = HideFlags.DontSave };
            _material.SetTexture("_MainTex", Texture2D.whiteTexture);
        }
        if (_mesh != null) return;
        const int segments = 16, rings = 8;
        var vertices = new Vector3[(segments + 1) * (rings + 1)];
        var colors = new Color[vertices.Length];
        var triangles = new int[segments * rings * 6];
        for (var ring = 0; ring <= rings; ring++)
        {
            var latitude = Mathf.PI * ring / rings;
            for (var segment = 0; segment <= segments; segment++)
            {
                var longitude = Mathf.PI * 2 * segment / segments;
                var index = ring * (segments + 1) + segment;
                vertices[index] = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude),
                    Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                colors[index] = Color.white;
            }
        }
        var triangle = 0;
        for (var ring = 0; ring < rings; ring++)
            for (var segment = 0; segment < segments; segment++)
            {
                var index = ring * (segments + 1) + segment;
                triangles[triangle++] = index;
                triangles[triangle++] = index + segments + 1;
                triangles[triangle++] = index + 1;
                triangles[triangle++] = index + 1;
                triangles[triangle++] = index + segments + 1;
                triangles[triangle++] = index + segments + 2;
            }
        _mesh = new Mesh { name = "Calypso Presence Sphere", hideFlags = HideFlags.DontSave };
        _mesh.vertices = vertices;
        _mesh.colors = colors;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
        _mesh.UploadMeshData(true);
    }

    private static void Release(Lamp lamp)
    {
        if (lamp.Root == null) return;
        lamp.Root.SetActive(false);
        UObject.Destroy(lamp.Root);
    }

    private void Clear()
    {
        foreach (var lamp in _lamps.Values) Release(lamp);
        _lamps.Clear();
        _room = null;
        if (_mesh != null) UObject.Destroy(_mesh);
        if (_material != null) UObject.Destroy(_material);
        _mesh = null;
        _material = null;
        _nextUpdate = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }
}
