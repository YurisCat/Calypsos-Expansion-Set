using System.Text.Json;

namespace CalypsoExpansionSet.Lobby;

// Independent of Unity so saved authoring data can be checked without the game.
internal sealed class LobbyBoxPlacement
{
    public int Version { get; set; } = 1;
    public string Model { get; set; } = "expansion_box";
    public string Scene { get; set; } = "";
    public string Anchor { get; set; } = "";
    public string SavedAt { get; set; } = "";
    public float[] Position { get; set; } = new[] { -1.2976f, 2.01f, 4.5197f };
    public float[] Rotation { get; set; } = new[] { 272.44223f, 0f, 0f };
    public float[] Scale { get; set; } = new[] { 1f, 1f, 1f };
    public float[] WorldPosition { get; set; } = new[] { 0f, 0f, 0f };
    // Additive v1 field: older placements keep the first preset.
    public int LightPreset { get; set; } = 1;
    public float LightBrightness { get; set; } = 1f;

    internal void Validate()
    {
        static bool Vector(float[]? v, float limit) =>
            v is { Length: 3 } && v.All(x => float.IsFinite(x) && Math.Abs(x) <= limit);
        if (Version != 1 || Model != "expansion_box" || LightPreset is < 0 or > 5
            || !float.IsFinite(LightBrightness) || LightBrightness is < 0 or > 5
            || !Vector(Position, 200f) || !Vector(Rotation, 36000f)
            || !Vector(Scale, 20f) || Scale.Any(x => x < 0.01f)
            || !Vector(WorldPosition, 100000f))
            throw new InvalidDataException("Invalid lobby-box placement: check model/version, finite coordinates and scale (0.01–20).");
    }

    internal static LobbyBoxPlacement Parse(string json)
    {
        if (json.Length > 16384) throw new InvalidDataException("Placement file is too large.");
        var value = JsonSerializer.Deserialize<LobbyBoxPlacement>(json) ?? throw new InvalidDataException("Empty placement.");
        value.Validate();
        return value;
    }

    internal string Serialize()
    {
        Validate();
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }
}
