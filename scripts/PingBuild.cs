// Compiled only inside the isolated Unity resource builder.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PingBuild
{
    public static void Build()
    {
        string output = Environment.GetEnvironmentVariable("CES_PING_BUNDLE_OUTPUT");
        if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Missing output directory.");
        Directory.CreateDirectory(output);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
            new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12 });
        AssetDatabase.ImportAsset("Assets/SharedPing.shader", ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/SharedPing.shader");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Ping shader failed to compile.");
        var build = new AssetBundleBuild { assetBundleName = "shared_pings.bundle", assetNames = new[] { "Assets/SharedPing.shader" } };
        var result = BuildPipeline.BuildAssetBundles(output, new[] { build },
            BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        if (result == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Ping bundle build failed.");
        Debug.Log("CES_PING_BUNDLE_READY");
    }
}
