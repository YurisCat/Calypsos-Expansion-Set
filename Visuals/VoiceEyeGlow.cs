using UnityEngine;
using UObject = UnityEngine.Object;

namespace CalypsoExpansionSet.Visuals;

// The native eye tween owns the original renderer's PropertyBlock. A local
// visual-only overlay shares its mesh/transform and draws just after it, so that
// neither the tween nor other instances are overwritten. Destroying our overlay
// restores the original eyes without a saved/stale material or block snapshot.
internal sealed class VoiceEyeGlow : IDisposable
{
    internal const float TransitionSeconds = 0.08f;
    internal static readonly Color IdleColor = new(1f, 0.025f, 0.015f, 1f);
    internal static readonly Color MovingColor = new(0.025f, 0.18f, 1f, 1f);
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
    private readonly VoiceEyeBlend _blend = new();
    private bool _moving, _reported, _reportedMoving;

    internal VoiceEyeGlow(ScoreTool idol)
    {
        var eye = idol.transform.Find("Dice/SM_IdoleBody/SM_IdolHead/SM_IdolEyes");
        _source = eye != null ? eye.GetComponent<Renderer>() : null!;
        var filter = eye != null ? eye.GetComponent<MeshFilter>() : null;
        if (_source == null || _source.sharedMaterial == null || filter == null || filter.sharedMesh == null)
            throw new InvalidOperationException("The voice idol eye renderer/material is missing.");
        _material = new Material(_source.sharedMaterial) { name = "Calypso Voice Eyes (local holder)" };
        try
        {
            if (!_material.HasProperty("_EmissionColor") || !_material.HasProperty("_EmissionIntensity"))
                throw new InvalidOperationException("The voice idol eye shader does not support emission.");
            _material.SetFloat("_EmissionIntensity", 2f);
            if (_material.HasProperty("_MASK_EmissionIntensity")) _material.SetFloat("_MASK_EmissionIntensity", 2f);
            var queue = _source.sharedMaterial.renderQueue;
            _material.renderQueue = queue is >= 0 and < 2499 ? queue + 1 : 2001;
            _attachment = new GameObject("Calypso Voice Eye Feedback");
            _attachment.layer = eye!.gameObject.layer;
            _attachment.transform.SetParent(eye, false);
            _attachment.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            _renderer = _attachment.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.renderingLayerMask = _source.renderingLayerMask;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            Apply(0f);
        }
        catch { Dispose(); throw; }
    }

    internal void SetMoving(bool moving, bool immediate = false)
    {
        _moving = moving;
        if (immediate)
        {
            var previous = _blend.Value;
            _blend.Reset(moving ? 1f : 0f);
            if (previous != _blend.Value) Apply(_blend.Value);
        }
    }

    internal void Tick(float deltaTime)
    {
        if (_material == null) return;
        if (_source == null || _renderer == null)
        {
            Dispose();
            return;
        }
        _renderer.enabled = _source.enabled && !_source.forceRenderingOff;
        var previous = _blend.Value;
        var amount = _blend.Step(_moving, deltaTime);
        if (previous != amount) Apply(amount);
        var plugin = CalypsoExpansionSetPlugin.Instance;
        if (plugin?.DebugTools.Enabled != true) { _reported = false; return; }
        if (!_reported || _reportedMoving != _moving)
        {
            _reported = true;
            _reportedMoving = _moving;
            plugin.Log.LogInfo($"[Voice eyes] {(_moving ? "blue / movement enabled" : "red / idle")}, blend={amount:F2}.");
        }
    }

    private void Apply(float amount)
    {
        var material = _material;
        if (material == null || _renderer == null) return;
        var color = Color.Lerp(IdleColor, MovingColor, amount);
        foreach (var property in ColorProperties)
            if (material.HasProperty(property)) _properties.SetColor(property, color);
        _renderer.SetPropertyBlock(_properties);
    }

    public void Dispose()
    {
        var material = _material;
        _material = null;
        try
        {
            if (_attachment != null) UObject.Destroy(_attachment);
        }
        finally
        {
            if (material != null) UObject.Destroy(material);
            _attachment = null; _renderer = null;
        }
    }
}

internal sealed class VoiceEyeBlend
{
    internal float Value { get; private set; }
    internal float Step(bool moving, float deltaTime)
    {
        var step = float.IsFinite(deltaTime) ? Mathf.Clamp(deltaTime, 0f, 0.1f) / VoiceEyeGlow.TransitionSeconds : 0f;
        Value = Mathf.Clamp(Value + (moving ? step : -step), 0f, 1f);
        return Value;
    }
    internal void Reset(float value) => Value = Mathf.Clamp(value, 0f, 1f);
}
