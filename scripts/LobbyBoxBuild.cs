// Runs only in the isolated Unity editor project; not compiled into the plugin.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class LobbyBoxBuild
{
    public static void Build()
    {
        var output = Environment.GetEnvironmentVariable("CES_BOX_OUTPUT");
        if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("CES_BOX_OUTPUT is required.");
        const string path = "Assets/Source/box.fbx";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.isReadable = true;
        importer.importAnimation = false;
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var filters = model.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0) throw new InvalidDataException("No static meshes.");
        var points = filters.SelectMany(f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v))).ToArray();
        var bounds = new Bounds(points[0], Vector3.zero);
        foreach (var point in points) bounds.Encapsulate(point);
        var size = bounds.size;
        float factor = 0.4f / Mathf.Max(size.x, size.y, size.z);
        var origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        var report = new StringBuilder();
        using (var file = File.Create(output))
        using (var gzip = new GZipStream(file, System.IO.Compression.CompressionLevel.Optimal))
        using (var writer = new BinaryWriter(gzip))
        {
            writer.Write(0x43455342); writer.Write(1);
            writer.Write(filters.Sum(f => f.sharedMesh.subMeshCount));
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                var materials = filter.GetComponent<Renderer>().sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var material = sub < materials.Length ? materials[sub] : null;
                    var texture = material != null ? material.mainTexture as Texture2D : null;
                    var texturePath = texture != null ? AssetDatabase.GetAssetPath(texture) : "";
                    report.AppendLine(filter.name + " | " + (material != null ? material.name : "(none)") + " | " + texturePath
                        + " | determinant=" + filter.transform.localToWorldMatrix.determinant
                        + " | diffuse=" + (material != null ? material.color.ToString() : "white"));
                    // Untextured FBX materials retain their diffuse color; don't invent a texture assignment.
                    if (texture != null)
                    {
                        var ti = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                        ti.isReadable = true;
                        ti.textureCompression = TextureImporterCompression.Uncompressed;
                        ti.maxTextureSize = 2048;
                        ti.mipmapEnabled = false;
                        ti.SaveAndReimport();
                        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    }
                    var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
                    var triangles = mesh.GetTriangles(sub);
                    if (filter.transform.localToWorldMatrix.determinant < 0)
                        for (int i = 0; i < triangles.Length; i += 3)
                        { int swap = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = swap; }
                    writer.Write(filter.name + "/" + sub);
                    writer.Write(vertices.Length);
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Write(writer, (filter.transform.TransformPoint(vertices[i]) - origin) * factor);
                        Write(writer, filter.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(normals[i]).normalized);
                        writer.Write(uv[i].x); writer.Write(uv[i].y);
                    }
                    writer.Write(triangles.Length);
                    foreach (int index in triangles) writer.Write(index);
                    writer.Write(texture != null ? texture.width : 1); writer.Write(texture != null ? texture.height : 1);
                    var pixels = texture != null ? texture.GetPixels32()
                        : new[] { (Color32)(material != null ? material.color : Color.white) };
                    foreach (var color in pixels)
                    {
                        writer.Write(color.r); writer.Write(color.g); writer.Write(color.b); writer.Write(color.a);
                    }
                }
            }
        }
        File.WriteAllText(output + ".txt", report + "\nOriginal bounds: " + bounds + "\nNormalized bounds: " + size * factor);
        Debug.Log("CES_BOX_READY\n" + report);
    }

    private static void Write(BinaryWriter writer, Vector3 value)
    { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
}
