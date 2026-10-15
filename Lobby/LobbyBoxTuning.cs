namespace CalypsoExpansionSet.Lobby;

internal static class LobbyBoxTuning
{
    // Signed velocity, not position snapping: returning to the press origin stops
    // movement, and a small deflection remains useful for precise placement.
    internal static float DragVelocity(float displacement, int group)
    {
        if (!float.IsFinite(displacement) || group is < 0 or > 3) return 0;
        var distance = Math.Abs(displacement);
        if (distance <= 2) return 0;
        var normalized = Math.Min((distance - 2) / 100f, 4f);
        var rate = group == 1 ? 30f : group == 2 ? 0.35f : group == 3 ? 0.5f : 0.4f;
        return Math.Sign(displacement) * normalized * normalized * rate;
    }

    internal static float LightEnvelope(bool available, bool enabled, float intensity, float original)
    {
        if (!available || !enabled || !float.IsFinite(intensity) || !float.IsFinite(original)
            || original <= 0.0001f || intensity <= 0) return 0;
        return Math.Clamp(intensity / original, 0, 4);
    }

    internal static bool PointInAim(float forwardDistance, float perpendicularSquared, float range, float radius)
        => float.IsFinite(forwardDistance) && float.IsFinite(perpendicularSquared)
            && forwardDistance > 0 && forwardDistance <= range
            && perpendicularSquared >= 0 && perpendicularSquared <= radius * radius;

    // Bottom-center pivot: account for the rear-most billboard corner, rather
    // than pushing only its origin out of a wall and leaving an edge embedded.
    internal static float PanelOutwardOffset(float basePlaneDistance, float rightDotNormal,
        float upDotNormal, float width, float height)
    {
        var rearCorner = -Math.Abs(rightDotNormal) * width * 0.5f + Math.Min(0, upDotNormal) * height;
        return Math.Max(0.4f, 0.25f - basePlaneDistance - rearCorner);
    }

    internal static float FillIntensity(float baseline, float envelope, float scale, float brightness) =>
        baseline * envelope * scale * scale * brightness;
}
