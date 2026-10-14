using System.Security.Cryptography;
using BepInEx;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace CalypsoExpansionSet.Visuals;

internal sealed class ScreenEffectResources : IDisposable, IVisualDiagnostics
{
    private static AssetBundle? _bundle;
    internal static Shader? Shader { get; private set; }
    private static int _references;
    private IDisposable? _note;
    private bool _owned;

    private ScreenEffectResources() { }

    internal static IDisposable Create(GameObject prefab, string artwork)
    {
        var resource = new ScreenEffectResources();
        try
        {
            resource._note = StickyNoteVisuals.Create(prefab, artwork);
            if (_references == 0)
            {
                using var stream = typeof(ScreenEffectResources).Assembly.GetManifestResourceStream(
                    "CalypsoExpansionSet.ScreenEffects.bundle")
                    ?? throw new InvalidOperationException("The screen-effect asset bundle is missing.");
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                var bytes = memory.ToArray();
                if (bytes.Length is < 1024 or > 1024 * 1024)
                    throw new InvalidDataException("Screen-effect bundle size is invalid.");
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                var directory = Path.Combine(Paths.CachePath, CalypsoExpansionSetPlugin.PluginId);
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "screen-" + hash + ".bundle");
                if (File.Exists(path))
                {
                    if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0
                        || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) != hash)
                        throw new InvalidDataException("The private screen-effect cache does not match its embedded source.");
                }
                else
                {
                    using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    file.Write(bytes);
                }
                // This game's metadata retains the asynchronous file/asset
                // methods. The restored LoadFromMemory wrapper is not safe here.
                CalypsoExpansionSetPlugin.Instance?.Log.LogInfo("[Screen assets] loading the verified native file request.");
                var request = AssetBundle.LoadFromFileAsync(path);
                _bundle = request.assetBundle; // Native GetAssetBundleBlocking.
                if (_bundle == null) throw new InvalidOperationException("Cannot load screen-effect assets.");
                CalypsoExpansionSetPlugin.Instance?.Log.LogInfo("[Screen assets] bundle loaded; requesting shader.");
                var shaderRequest = _bundle.LoadAssetAsync("Assets/IdolScreen.shader", Il2CppType.Of<Shader>());
                Shader = shaderRequest.asset?.TryCast<Shader>();
                if (Shader == null || !Shader.isSupported)
                    throw new InvalidOperationException("The screen-effect shader is unavailable on this graphics device.");
                // A managed static reference is invisible to Unity's native
                // UnloadUnusedAssets during the menu-to-world transition.
                // This owned bundle is explicitly unloaded by its last lease.
                Shader.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                CalypsoExpansionSetPlugin.Instance?.Log.LogInfo("[Screen assets] shader ready.");
            }
            _references++;
            resource._owned = true;
            return resource;
        }
        catch
        {
            resource.Dispose();
            if (_references == 0) Unload();
            throw;
        }
    }

    public string SetDiagnosticMode(string mode) =>
        (_note as IVisualDiagnostics)?.SetDiagnosticMode(mode) ?? "No note.";

    public void Dispose()
    {
        _note?.Dispose();
        _note = null;
        if (!_owned) return;
        _owned = false;
        if (--_references == 0) Unload();
    }

    private static void Unload()
    {
        Shader = null;
        if (_bundle != null) _bundle.Unload(true);
        _bundle = null;
    }
}
