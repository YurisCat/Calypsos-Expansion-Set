namespace CalypsoExpansionSet.Idols;

internal sealed class VoiceInputGate
{
    internal const double SampleTimeoutSeconds = 0.25;
    private const double ResponseSeconds = 0.08;
    private const float ReleaseRatio = 0.8f;
    private double _lastTime = double.NaN;
    private float _level;
    internal bool Moving { get; private set; }

    internal bool Evaluate(float amplitude, float threshold, double sampledAt, double now)
    {
        if (!float.IsFinite(amplitude) || amplitude < 0f || !float.IsFinite(threshold) || threshold <= 0f
            || !double.IsFinite(sampledAt) || !double.IsFinite(now) || now < sampledAt
            || now - sampledAt > SampleTimeoutSeconds)
        {
            Reset();
            return false;
        }

        if (double.IsNaN(_lastTime)) _level = amplitude;
        else
        {
            var elapsed = Math.Clamp(now - _lastTime, 0d, 0.1d);
            _level += (amplitude - _level) * (float)(1d - Math.Exp(-elapsed / ResponseSeconds));
        }
        _lastTime = now;
        Moving = _level >= threshold * (Moving ? ReleaseRatio : 1f);
        return Moving;
    }

    internal void Reset()
    {
        _lastTime = double.NaN;
        _level = 0f;
        Moving = false;
    }
}
