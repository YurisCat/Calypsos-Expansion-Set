namespace CalypsoExpansionSet.Lobby;

internal static class LobbyBoxFraming
{
    internal readonly record struct View(float Distance, float LeftOffset);

    // Cover center at 75% of the screen; all corners within x=57..93%, y=25..75%.
    internal static View Calculate(float halfWidth, float halfHeight, float halfDepth, float verticalFov, float aspect, float nearClip)
    {
        if (!float.IsFinite(halfWidth) || halfWidth <= 0 || !float.IsFinite(halfHeight) || halfHeight <= 0
            || !float.IsFinite(halfDepth) || halfDepth < 0 || !float.IsFinite(verticalFov) || verticalFov is <= 1 or >= 179
            || !float.IsFinite(aspect) || aspect <= 0 || !float.IsFinite(nearClip) || nearClip < 0)
            throw new ArgumentOutOfRangeException(nameof(halfWidth), "Invalid box framing dimensions or lens.");
        var tanV = MathF.Tan(verticalFov * MathF.PI / 360);
        var tanH = tanV * aspect;
        var distance = Math.Max(halfHeight / (0.5f * tanV) + halfDepth,
            (halfWidth + 0.86f * tanH * halfDepth) / (0.36f * tanH));
        distance = Math.Max(distance, halfDepth + nearClip + 0.03f);
        return new View(distance, 0.5f * tanH * distance);
    }
}
