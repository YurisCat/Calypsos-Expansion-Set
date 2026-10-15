using System.IO.Compression;
using System.Runtime.InteropServices;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Lobby;

internal sealed class LobbyBoxModel : IDisposable
{
    private readonly List<UObject> _owned = new();
    internal GameObject Root { get; }
    internal BoxCollider Target { get; private set; } = null!;
    internal Bounds LocalBounds { get; private set; }

    internal LobbyBoxModel(Transform parent, Material source)
    {
        Root = new GameObject("Calypso Expansion Box") { hideFlags = HideFlags.DontSave };
        Root.SetActive(false);
        Root.transform.SetParent(parent, false);
        try { Load(source); Root.SetActive(true); }
        catch { Dispose(); throw; }
    }

    private T Own<T>(T resource) where T : UObject { _owned.Add(resource); return resource; }
    private static Vector3 Vector(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    private void Load(Material source)
    {
        using var stream = typeof(LobbyBoxModel).Assembly.GetManifestResourceStream("CalypsoExpansionSet.LobbyBox.box.cesmodel")
            ?? throw new InvalidDataException("Missing embedded lobby model.");
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new BinaryReader(gzip);
        if (reader.ReadInt32() != 0x43455342 || reader.ReadInt32() != 1) throw new InvalidDataException("Invalid model header.");
        var parts = reader.ReadInt32();
        if (parts is < 1 or > 16) throw new InvalidDataException("Invalid model part count.");
        var bounds = new Bounds();
        var hasPoint = false;
        for (var part = 0; part < parts; part++)
        {
            var name = reader.ReadString();
            if (name.Length > 256) throw new InvalidDataException("Invalid part name.");
            var count = reader.ReadInt32();
            if (count is < 3 or > 65535) throw new InvalidDataException("Invalid vertex count.");
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count];
            var colors = new Color[count];
            for (var i = 0; i < count; i++)
            {
                vertices[i] = Vector(reader); normals[i] = Vector(reader);
                uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle()); colors[i] = Color.white;
                if (!Finite(vertices[i]) || vertices[i].sqrMagnitude > 4f || !Finite(normals[i])
                    || !float.IsFinite(uv[i].x) || !float.IsFinite(uv[i].y))
                    throw new InvalidDataException("Invalid mesh attributes.");
                if (!hasPoint) { bounds = new Bounds(vertices[i], Vector3.zero); hasPoint = true; }
                else bounds.Encapsulate(vertices[i]);
            }
            var indexCount = reader.ReadInt32();
            if (indexCount is < 3 or > 196605 || indexCount % 3 != 0) throw new InvalidDataException("Invalid triangle count.");
            var indices = new int[indexCount];
            for (var i = 0; i < indexCount; i++)
            {
                indices[i] = reader.ReadInt32();
                if (indices[i] < 0 || indices[i] >= count) throw new InvalidDataException("Invalid triangle index.");
            }
            var width = reader.ReadInt32(); var height = reader.ReadInt32();
            if (width is < 1 or > 2048 || height is < 1 or > 2048) throw new InvalidDataException("Invalid albedo size.");
            var length = checked(width * height * 4);
            var pixels = reader.ReadBytes(length);
            if (pixels.Length != length) throw new EndOfStreamException("Truncated albedo.");
            var texture = Own(new Texture2D(width, height, TextureFormat.RGBA32, false, false)
                { name = "Calypso box albedo", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear });
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try { texture.LoadRawTextureData(handle.AddrOfPinnedObject(), pixels.Length); }
            finally { handle.Free(); }
            texture.Apply(false, true);
            var material = Own(new Material(source) { name = "Calypso box material" });
            if (!material.HasProperty("_BaseMap")) throw new InvalidOperationException("Unsupported box material shader.");
            foreach (var property in new[] { "_AlbedoColor", "_BaseColor", "_Color" })
                if (material.HasProperty(property)) material.SetColor(property, Color.white);
            foreach (var property in new[] { "_NormalStrenght", "_RENDER_NormalStrenght", "_Reflection",
                "_RENDER_Reflection", "_TexLerp", "_USEBIOME", "_UseBiome", "_INNER", "_BEVELDECAL",
                "_USEDAMAGEFEEDBACK", "_USESHRINK", "_Metallic", "_MetallicIntensity", "_EmissionIntensity", "_MASK_EmissionIntensity" })
                if (material.HasProperty(property)) material.SetFloat(property, 0f);
            foreach (var keyword in new[] { "_USEBIOME_ON", "_INNER_ON", "_BEVELDECAL_ON", "_USEDAMAGEFEEDBACK_ON", "_USESHRINK_ON" })
                material.DisableKeyword(keyword);
            if (material.HasProperty("_MaskMap")) material.SetTexture("_MaskMap", Texture2D.whiteTexture);
            if (material.HasProperty("_BaseMapSec")) material.SetTexture("_BaseMapSec", Texture2D.whiteTexture);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            if (material.HasProperty("_AO")) material.SetFloat("_AO", 1f);
            material.SetTexture("_BaseMap", texture);
            var mesh = Own(new Mesh { name = name });
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.colors = colors; mesh.triangles = indices;
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            var obj = new GameObject(name) { hideFlags = HideFlags.DontSave };
            obj.transform.SetParent(Root.transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        if (reader.BaseStream.ReadByte() != -1) throw new InvalidDataException("Unexpected model trailing data.");
        LocalBounds = bounds;
        // A local query target only: no rigidbody, collision response or network behaviour.
        Root.layer = 2; // Ignore Raycast; queried explicitly with Collider.Raycast.
        Target = Root.AddComponent<BoxCollider>();
        Target.isTrigger = true; Target.center = bounds.center; Target.size = bounds.size;
    }

    public void Dispose()
    {
        if (Root != null) { Root.SetActive(false); UObject.Destroy(Root); }
        foreach (var obj in _owned) if (obj != null) UObject.Destroy(obj);
        _owned.Clear();
    }
}
