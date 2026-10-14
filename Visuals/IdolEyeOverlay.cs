using UnityEngine;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Visuals;

// Native ScoreTool tweens own the original eye PropertyBlock. Draw a separately
// owned visual over its shared mesh, without changing the native renderer.
internal sealed class IdolEyeOverlay : IDisposable
{
    private static readonly int[] ColorProperties = {
        Shader.PropertyToID("_AlbedoColor"), Shader.PropertyToID("_COLOR_AlbedoColor"),
        Shader.PropertyToID("_EmissionColor"), Shader.PropertyToID("_MASK_EmissionColor"),
        Shader.PropertyToID("_FresnelColor")
    };
    private readonly Renderer _source;
    private Renderer? _renderer;
    private GameObject? _attachment;
    private Material? _material;
    private readonly MaterialPropertyBlock _properties = new();

    internal static void Validate(ScoreTool? idol)
    {
        if (idol == null) throw new InvalidOperationException("The idol eye owner is missing.");
        var eye = idol.transform.Find("Dice/SM_IdoleBody/SM_IdolHead/SM_IdolEyes");
        var renderer = eye != null ? eye.GetComponent<Renderer>() : null;
        var filter = eye != null ? eye.GetComponent<MeshFilter>() : null;
        if (renderer == null || renderer.sharedMaterial == null || filter == null || filter.sharedMesh == null
            || !renderer.sharedMaterial.HasProperty("_EmissionColor")
            || !renderer.sharedMaterial.HasProperty("_EmissionIntensity"))
            throw new InvalidOperationException("The idol eye mesh/material or emission shader is missing.");
    }

    internal IdolEyeOverlay(ScoreTool idol, string materialName, string attachmentName, Color color)
    {
        Validate(idol);
        var eye = idol.transform.Find("Dice/SM_IdoleBody/SM_IdolHead/SM_IdolEyes");
        _source = eye.GetComponent<Renderer>()!;
        var filter = eye.GetComponent<MeshFilter>()!;
        _material = new Material(_source.sharedMaterial) { name = materialName };
        try
        {
            _material.SetFloat("_EmissionIntensity", 2f);
            if (_material.HasProperty("_MASK_EmissionIntensity")) _material.SetFloat("_MASK_EmissionIntensity", 2f);
            var queue = _source.sharedMaterial.renderQueue;
            _material.renderQueue = queue is >= 0 and < 2499 ? queue + 1 : 2001;
            _attachment = new GameObject(attachmentName) { layer = eye.gameObject.layer };
            _attachment.transform.SetParent(eye, false);
            _attachment.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            _renderer = _attachment.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.renderingLayerMask = _source.renderingLayerMask;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            Apply(color);
        }
        catch { Dispose(); throw; }
    }

    internal bool Refresh()
    {
        if (_material == null || _source == null || _renderer == null) return false;
        _renderer.enabled = _source.enabled && !_source.forceRenderingOff;
        return true;
    }

    internal void Apply(Color color)
    {
        if (_material == null || _renderer == null) return;
        foreach (var property in ColorProperties)
            if (_material.HasProperty(property)) _properties.SetColor(property, color);
        _renderer.SetPropertyBlock(_properties);
    }

    public void Dispose()
    {
        var material = _material;
        _material = null;
        try { if (_attachment != null) UObject.Destroy(_attachment); }
        finally
        {
            if (material != null) UObject.Destroy(material);
            _attachment = null;
            _renderer = null;
        }
    }
}
