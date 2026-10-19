namespace CalypsoExpansionSet.QualityOfLife.Pings;

internal static class PingPresentation
{
    // World units, without assuming a real-world distance unit.
    internal const float NearDistance = 3f, FarDistance = 35f;
    private const float SmoothSeconds = .12f;

    internal static int Compare(PingEntry a, PingEntry b)
    {
        if (a.Target != b.Target) return a.Target.CompareTo(b.Target);
        if (a.Target != 0 && a.Revision != b.Revision)
            return PingState.Newer(a.Revision, b.Revision) ? -1 : 1;
        return a.Slot.CompareTo(b.Slot);
    }

    internal static (float Scale, float Alpha) AtDistance(float distance)
    {
        var t = Math.Clamp((distance - NearDistance) / (FarDistance - NearDistance), 0f, 1f);
        t = t * t * (3 - 2 * t);
        return (1.2f - .5f * t, 1f - .45f * t);
    }

    internal static float SmoothDistance(float current, float target, float deltaTime) =>
        current + (target - current) * (1 - MathF.Exp(-MathF.Max(0, deltaTime) / SmoothSeconds));
}
