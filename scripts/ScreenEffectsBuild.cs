// Copied into the isolated Unity asset-builder project, never into the MOD DLL.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ScreenEffectsBuild
{
    public static void Build()
    {
        string output = Environment.GetEnvironmentVariable("CES_SCREEN_BUNDLE_OUTPUT");
        if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Missing bundle output directory.");
        Directory.CreateDirectory(output);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
            new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12 });
        AssetDatabase.ImportAsset("Assets/IdolScreen.shader", ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/IdolScreen.shader");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("The screen-effect shader failed to compile.");
        var build = new AssetBundleBuild
        {
            assetBundleName = "idol_screen.bundle",
            assetNames = new[] { "Assets/IdolScreen.shader" }
        };
        var result = BuildPipeline.BuildAssetBundles(output, new[] { build },
            BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows64);
        if (result == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("Screen-effect bundle build failed.");
        Debug.Log("CES_SCREEN_BUNDLE_READY");
    }
}
