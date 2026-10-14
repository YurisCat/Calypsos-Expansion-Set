using UnityEngine;

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
    private IdolEyeOverlay? _overlay;
    private readonly VoiceEyeBlend _blend = new();
    private bool _moving, _reported, _reportedMoving;

    internal VoiceEyeGlow(ScoreTool idol)
    {
        _overlay = new IdolEyeOverlay(idol, "Calypso Voice Eyes (local holder)", "Calypso Voice Eye Feedback", IdleColor);
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
        if (_overlay == null) return;
        if (!_overlay.Refresh())
        {
            Dispose();
            return;
        }
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
        _overlay?.Apply(Color.Lerp(IdleColor, MovingColor, amount));
    }

    public void Dispose()
    {
        _overlay?.Dispose();
        _overlay = null;
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
