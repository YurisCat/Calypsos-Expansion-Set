namespace CalypsoExpansionSet.Idols;

// No native objects or transport in the rule itself; server and client share
// the same bounded multiplier/color mapping. Only the server advances fatigue.
internal readonly record struct RelayCarrier(ulong AccountId, long SessionId);

internal sealed class RelayFatigue
{
    internal const float SoloSeconds = 180f;
    internal const float MinimumMultiplier = 0.25f;
    internal const int MaximumPlayers = 64;
    internal RelayCarrier LastCarrier { get; private set; }
    internal bool HasCarrier { get; private set; }
    internal bool Held { get; private set; }
    internal float Amount { get; private set; }

    internal bool PickUp(RelayCarrier carrier)
    {
        var reset = !HasCarrier || LastCarrier != carrier;
        if (reset) Amount = 0f;
        HasCarrier = true;
        LastCarrier = carrier;
        Held = true;
        return reset;
    }

    internal void Release() => Held = false;

    internal void Advance(float seconds, int onlinePlayers)
    {
        if (!Held || !float.IsFinite(seconds) || seconds <= 0f) return;
        var count = Math.Clamp(onlinePlayers, 1, MaximumPlayers);
        Amount = Math.Clamp(Amount + seconds * count / SoloSeconds, 0f, 1f);
    }

    internal static float Multiplier(float amount) =>
        1f - (1f - MinimumMultiplier) * Math.Clamp(float.IsFinite(amount) ? amount : 0f, 0f, 1f);
}
