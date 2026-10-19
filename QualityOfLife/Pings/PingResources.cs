using System.Security.Cryptography;
using BepInEx;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace CalypsoExpansionSet.QualityOfLife.Pings;

internal sealed class PingResources : IDisposable
{
    private AssetBundle? _bundle;
    internal Material Material { get; }

    internal PingResources()
    {
        try
        {
            using var stream = typeof(PingResources).Assembly.GetManifestResourceStream("CalypsoExpansionSet.Pings.bundle")
                ?? throw new InvalidDataException("Shared-ping shader bundle is missing.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();
            if (bytes.Length is < 1024 or > 1024 * 1024) throw new InvalidDataException("Invalid ping bundle size.");
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var directory = Path.Combine(Paths.CachePath, CalypsoExpansionSetPlugin.PluginId);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "pings-" + hash + ".bundle");
            if (File.Exists(path))
            {
                if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0
                    || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) != hash)
                    throw new InvalidDataException("Shared-ping cache does not match the embedded bundle.");
            }
            else
            {
                using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                file.Write(bytes);
            }
            // Use the retained native requests, as for the held screen effects.
            _bundle = AssetBundle.LoadFromFileAsync(path).assetBundle;
            if (_bundle == null) throw new InvalidOperationException("Cannot load shared-ping assets.");
            var shader = _bundle.LoadAssetAsync("Assets/SharedPing.shader", Il2CppType.Of<Shader>()).asset?.TryCast<Shader>();
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Shared-ping shader is unavailable.");
            shader.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            Material = new Material(shader) { name = "Calypso shared pings", hideFlags = HideFlags.DontSave };
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        if (Material != null) UnityEngine.Object.Destroy(Material);
        if (_bundle != null) _bundle.Unload(true);
        _bundle = null;
    }
}
