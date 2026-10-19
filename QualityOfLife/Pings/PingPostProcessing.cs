using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.QualityOfLife.Pings;

// Capture the passes actually executed for the gameplay camera. Volume defaults
// cannot describe per-camera blending, overscan, or a disabled post-process pass.
internal sealed class PingPostProcessing : IDisposable
{
    private Material? _panini;
    private MaterialPropertyBlock? _properties;
    private Vector4 _parameters, _sourceExtents, _distortion1, _distortion2;
    private bool _hasPanini;

    internal void Begin()
    {
        _hasPanini = false;
        _distortion1 = _distortion2 = Vector4.zero;
    }

    internal void Panini(PostProcessPassRenderGraph.PaniniProjectionPassData data)
    {
        var source = data.material;
        if (source == null) return;
        if (_panini == null || _panini.shader != source.shader)
        {
            if (_panini != null) UObject.Destroy(_panini);
            _panini = new Material(source) { name = "Calypso ping Panini", hideFlags = HideFlags.DontSave };
        }
        else _panini.CopyPropertiesFromMaterial(source);
        _parameters = data.paniniParams;
        var extents = data.sourceExtents;
        _sourceExtents = new Vector4(extents.x, extents.y, 0, 0);
        _hasPanini = true;
    }

    internal void Distortion(Material material)
    {
        if (!material.IsKeywordEnabled("_DISTORTION")) { _distortion1 = _distortion2 = Vector4.zero; return; }
        _distortion1 = material.GetVector("_Distortion_Params1");
        _distortion2 = material.GetVector("_Distortion_Params2");
    }

    internal void Configure(CommandBuffer commands)
    {
        commands.SetGlobalVector("_CESPingDistortion1", _distortion1);
        commands.SetGlobalVector("_CESPingDistortion2", _distortion2);
    }

    internal bool HasPanini => _hasPanini;
    internal string Description => $"Panini={_hasPanini}, params={_parameters}, extents={_sourceExtents}, "
        + $"lens1={_distortion1}, lens2={_distortion2}";

    internal void Warp(CommandBuffer commands, RenderTexture source, RenderTargetIdentifier destination)
    {
        _properties ??= new MaterialPropertyBlock();
        _properties.Clear();
        _properties.SetTexture("_BlitTexture", source);
        _properties.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
        _properties.SetVector("_RTHandleScale", Vector4.one);
        _properties.SetVector("_GlobalMipBias", Vector4.zero);
        _properties.SetVector("_ScreenSize", new Vector4(source.width, source.height, 1f / source.width, 1f / source.height));
        _properties.SetVector("_Params", _parameters);
        _properties.SetVector("_SourceExtents", _sourceExtents);
        commands.SetRenderTarget(destination);
        // Use the game's shader, including its overscan variant, with a private
        // material/property block. No shared material or URP globals are edited.
        commands.DrawProcedural(Matrix4x4.identity, _panini!, 0, MeshTopology.Triangles, 3, 1, _properties);
    }

    public void Dispose()
    {
        Begin();
        if (_panini != null) UObject.Destroy(_panini);
        _panini = null;
        _properties?.Clear(); _properties = null;
    }
}

[HarmonyPatch(typeof(PostProcessPassRenderGraph.__c), "_RenderPaniniProjection_b__75_0")]
internal static class PingPaniniPatch
{
    private static void Postfix(PostProcessPassRenderGraph.PaniniProjectionPassData data)
        => CalypsoExpansionSetPlugin.Instance?.Pings?.CapturePanini(data);
}

[HarmonyPatch(typeof(PostProcessPassRenderGraph), nameof(PostProcessPassRenderGraph.SetupLensDistortion))]
internal static class PingDistortionPatch
{
    private static void Postfix(Material material)
        => CalypsoExpansionSetPlugin.Instance?.Pings?.CaptureDistortion(material);
}
