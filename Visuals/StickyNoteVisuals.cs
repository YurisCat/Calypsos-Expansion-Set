using System.Runtime.InteropServices;
using System.Text.Json;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Visuals;

internal interface IVisualDiagnostics
{
    string SetDiagnosticMode(string mode);
}

/// <summary>Owns one type's pure visual attachment. No collider or network component.</summary>
internal sealed class StickyNoteVisuals : IDisposable, IVisualDiagnostics
{
    private sealed class MeshData
    {
        public int version { get; set; }
        public float[][] vertices { get; set; } = Array.Empty<float[]>();
        public float[][] normals { get; set; } = Array.Empty<float[]>();
        public float[][] uv { get; set; } = Array.Empty<float[]>();
        public int[] triangles { get; set; } = Array.Empty<int>();
    }

    private Mesh? _mesh;
    private Material? _material;
    private Texture2D? _paper;
    private GameObject? _attachment;
    private Vector3[] _suppliedNormals = Array.Empty<Vector3>();
    private Vector3[] _flatNormals = Array.Empty<Vector3>();
    private static byte[]? _sharedPaper;
    private static int _paperUsers;
    private bool _ownsPaperBase;
    private bool _disposed;

    internal static IDisposable Create(GameObject prefab)
        => Create(prefab, null);

    internal static IDisposable Create(GameObject prefab, string? artworkKey)
    {
        if (artworkKey != null && (artworkKey.Length is < 1 or > 64 || artworkKey[0] is < 'a' or > 'z'
            || artworkKey.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))))
            throw new ArgumentException("Artwork key must be a stable lowercase identifier.", nameof(artworkKey));
        var owned = new StickyNoteVisuals();
        try { owned.Build(prefab, artworkKey); return owned; }
        catch { owned.Dispose(); throw; }
    }

    private static byte[] Resource(string name)
    {
        using var stream = typeof(StickyNoteVisuals).Assembly.GetManifestResourceStream("CalypsoExpansionSet.StickyNote." + name)
            ?? throw new InvalidOperationException($"Missing embedded note resource: {name}.");
        if (stream.Length <= 0 || stream.Length > 4 * 1024 * 1024)
            throw new InvalidOperationException($"Invalid note resource size: {name}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private void Build(GameObject prefab, string? artworkKey)
    {
        var head = prefab.transform.Find("Dice/SM_IdoleBody/SM_IdolHead");
        var source = head != null ? head.GetComponent<Renderer>() : null;
        if (source == null || source.sharedMaterial == null)
            throw new InvalidOperationException("Score's head/material is unavailable for the note attachment.");
        var data = JsonSerializer.Deserialize<MeshData>(Resource("note.mesh.json"))
            ?? throw new InvalidOperationException("Missing note mesh data.");
        var count = data.vertices.Length;
        if (data.version != 1 || count is < 3 or > 4096 || data.normals.Length != count || data.uv.Length != count
            || data.triangles.Length is < 3 or > 12288 || data.triangles.Length % 3 != 0
            || data.triangles.Any(index => index < 0 || index >= count))
            throw new InvalidOperationException("Invalid note mesh counts/indices.");
        static bool Valid(float[] values, int size) => values.Length == size && values.All(float.IsFinite);
        var vertices = new Vector3[count];
        var uvs = new Vector2[count];
        _suppliedNormals = new Vector3[count];
        _flatNormals = new Vector3[count];
        var colors = new Color[count];
        for (var i = 0; i < count; i++)
        {
            if (!Valid(data.vertices[i], 3) || !Valid(data.normals[i], 3) || !Valid(data.uv[i], 2))
                throw new InvalidOperationException("Non-finite or malformed note mesh attributes.");
            vertices[i] = new Vector3(data.vertices[i][0], data.vertices[i][1], data.vertices[i][2]);
            _suppliedNormals[i] = new Vector3(data.normals[i][0], data.normals[i][1], data.normals[i][2]);
            if (vertices[i].sqrMagnitude > 1f || _suppliedNormals[i].sqrMagnitude is < 0.9f or > 1.1f)
                throw new InvalidOperationException("Note mesh exceeds head-local bounds or has invalid normals.");
            uvs[i] = new Vector2(data.uv[i][0], data.uv[i][1]);
            colors[i] = Color.white;
        }
        // Export keeps separate corners, so UV seams and artist split normals survive.
        for (var i = 0; i < data.triangles.Length; i += 3)
        {
            var a = data.triangles[i]; var b = data.triangles[i + 1]; var c = data.triangles[i + 2];
            var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).normalized;
            _flatNormals[a] = _flatNormals[b] = _flatNormals[c] = normal;
        }
        _mesh = new Mesh { name = "Calypso StickyNote (artist mesh)" };
        _mesh.vertices = vertices;
        _mesh.normals = _suppliedNormals;
        _mesh.uv = uvs;
        _mesh.colors = colors;
        _mesh.triangles = data.triangles;
        _mesh.RecalculateBounds();
        _mesh.RecalculateTangents();
        // This game's LoadImage wrapper routes through a missing ReadOnlySpan
        // method. Use the verified IntPtr/int wrapper and a complete RGBA mip chain.
        _sharedPaper ??= NoteTextureCodec.DecodeBase(Resource("paper.note"));
        _paperUsers++;
        _ownsPaperBase = true;
        var rgba = artworkKey == null ? _sharedPaper
            : NoteTextureCodec.Restore(_sharedPaper, Resource($"Artwork.{artworkKey}.notedelta"));
        _paper = new Texture2D(512, 512, TextureFormat.RGBA32, true, false) { name = $"Calypso StickyNote {artworkKey ?? "blank"}" };
        var pinned = GCHandle.Alloc(rgba, GCHandleType.Pinned);
        try { _paper.LoadRawTextureData(pinned.AddrOfPinnedObject(), rgba.Length); }
        finally { pinned.Free(); }
        _paper.Apply(true, true);
        _paper.wrapMode = TextureWrapMode.Clamp;
        _paper.filterMode = FilterMode.Trilinear;
        _material = new Material(source.sharedMaterial) { name = "Calypso StickyNote (game shader, opaque)" };
        if (!_material.HasProperty("_BaseMap") || !_material.HasProperty("_AlbedoColor"))
            throw new InvalidOperationException($"Unsupported game paper shader: {_material.shader.name}.");
        // The game's PhysicalMap packing is custom. First validate the supplied
        // geometry/albedo without borrowing the statue's normal or biome maps.
        foreach (var key in new[] { "_AlbedoColor", "_BaseColor", "_Color" })
            if (_material.HasProperty(key)) _material.SetColor(key, Color.white);
        foreach (var key in new[] { "_NormalStrenght", "_RENDER_NormalStrenght", "_Reflection", "_RENDER_Reflection",
            "_TexLerp", "_USEBIOME", "_UseBiome", "_INNER", "_BEVELDECAL", "_USEDAMAGEFEEDBACK", "_USESHRINK",
            "_Metallic", "_MetallicIntensity", "_EmissionIntensity", "_MASK_EmissionIntensity" })
            if (_material.HasProperty(key)) _material.SetFloat(key, 0f);
        foreach (var keyword in new[] { "_USEBIOME_ON", "_INNER_ON", "_BEVELDECAL_ON", "_USEDAMAGEFEEDBACK_ON", "_USESHRINK_ON" })
            _material.DisableKeyword(keyword);
        if (_material.HasProperty("_MaskMap")) _material.SetTexture("_MaskMap", Texture2D.whiteTexture);
        if (_material.HasProperty("_BaseMapSec")) _material.SetTexture("_BaseMapSec", Texture2D.whiteTexture);
        if (_material.HasProperty("_EmissionColor")) _material.SetColor("_EmissionColor", Color.black);
        if (_material.HasProperty("_AO")) _material.SetFloat("_AO", 1f);
        _material.SetTexture("_BaseMap", _paper);
        _attachment = new GameObject("Calypso StickyNote");
        _attachment.layer = head!.gameObject.layer;
        _attachment.transform.SetParent(head, false);
        _attachment.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = _attachment.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _material;
        renderer.renderingLayerMask = source.renderingLayerMask;
        CalypsoExpansionSetPlugin.Instance?.Log.LogInfo(
            $"[Visual] note artwork={artworkKey ?? "blank"}: attached to head, corners={count}, triangles={data.triangles.Length / 3}, "
            + $"shader={_material.shader.name}, opaque paper/tape with baked artwork; supplied normals preserved; normal-map/height disabled.");
    }

    public string SetDiagnosticMode(string mode)
    {
        if (_disposed || _mesh == null || _material == null || _paper == null)
            throw new InvalidOperationException("The visual resources are unavailable.");
        if (mode is not ("texture" or "white" or "flat"))
            return "Modes: texture (baked albedo/artist normals), white (no albedo), flat (white + face normals).";
        _mesh.normals = mode == "flat" ? _flatNormals : _suppliedNormals;
        _mesh.RecalculateTangents();
        _material.SetTexture("_BaseMap", mode == "texture" ? _paper : Texture2D.whiteTexture);
        return $"Visual mode={mode}. Local diagnostic for this type's note only; select texture to restore.";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_attachment != null) UObject.Destroy(_attachment);
        if (_material != null) UObject.Destroy(_material);
        if (_paper != null) UObject.Destroy(_paper);
        if (_mesh != null) UObject.Destroy(_mesh);
        _attachment = null; _material = null; _paper = null; _mesh = null;
        if (_ownsPaperBase)
        {
            _ownsPaperBase = false;
            if (--_paperUsers == 0) _sharedPaper = null;
        }
    }
}
