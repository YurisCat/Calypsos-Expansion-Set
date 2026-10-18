namespace CalypsoExpansionSet.Curses;

// Real accounts survive reconnects; offline/local test identities belong to one connection lifetime.
internal readonly record struct DisguiseCarrier(ulong Account, ulong Session)
{
    internal static DisguiseCarrier ForConnection(ulong account, ulong session, bool localMultiplayer) =>
        new(account, account == 0 || localMultiplayer ? session : 0);
}
internal readonly record struct DisguiseLook(byte Character, byte Cloth, byte Headgear, string Nick);
internal readonly record struct DisguiseParticipant(DisguiseCarrier Carrier, uint User, uint Player, DisguiseLook Look);
internal readonly record struct DisguiseEntry(uint User, uint Player, DisguiseLook Look);

internal sealed class DisguiseRound
{
    internal const int MaximumPlayers = 128;
    private sealed record Assignment(DisguiseCarrier Donor, DisguiseLook Look);
    private readonly Dictionary<DisguiseCarrier, Assignment> _assignments = new();
    private readonly Dictionary<DisguiseCarrier, DisguiseLook> _present = new();
    internal bool Active => _assignments.Count != 0;
    internal uint Revision { get; private set; }

    internal bool Start(IReadOnlyList<DisguiseParticipant> players, Func<int, int> random)
    {
        if (Active || players.Count is < 2 or > MaximumPlayers) return false;
        var order = players.ToArray();
        var carriers = new HashSet<DisguiseCarrier>();
        var users = new HashSet<uint>();
        var entities = new HashSet<uint>();
        foreach (var player in order)
            if (player.Carrier == default || player.User == 0 || player.Player == 0
                || !carriers.Add(player.Carrier) || !users.Add(player.User) || !entities.Add(player.Player)) return false;
        for (var index = order.Length - 1; index > 0; index--)
        {
            var other = random(index + 1);
            if ((uint)other > index) throw new ArgumentOutOfRangeException(nameof(random));
            (order[index], order[other]) = (order[other], order[index]);
        }
        for (var index = 0; index < order.Length; index++)
        {
            var donor = order[(index + 1) % order.Length];
            _assignments.Add(order[index].Carrier, new Assignment(donor.Carrier, donor.Look));
        }
        Advance();
        return true;
    }

    internal void Snapshot(IReadOnlyList<DisguiseParticipant> players, List<DisguiseEntry> entries)
    {
        entries.Clear();
        _present.Clear();
        if (!Active) return;
        foreach (var player in players)
        {
            _present[player.Carrier] = player.Look;
            if (!_assignments.TryGetValue(player.Carrier, out var assignment))
            {
                if (_assignments.Count >= MaximumPlayers) continue;
                // Insert a late join into the existing cycle; everyone else keeps their partner.
                var predecessor = _assignments.First();
                assignment = predecessor.Value;
                _assignments.Add(player.Carrier, assignment);
                _assignments[predecessor.Key] = new Assignment(player.Carrier, player.Look);
                Advance();
            }
        }
        var changed = false;
        foreach (var player in players)
            if (_assignments.TryGetValue(player.Carrier, out var assignment))
            {
                // Keep the partner across role changes; refresh the saved outfit
                // only when that partner selects a different character.
                if (_present.TryGetValue(assignment.Donor, out var look) && look.Character != assignment.Look.Character)
                {
                    assignment = assignment with { Look = look };
                    _assignments[player.Carrier] = assignment;
                    changed = true;
                }
                entries.Add(new DisguiseEntry(player.User, player.Player, assignment.Look));
            }
        if (changed) Advance();
    }

    internal void Clear()
    {
        _assignments.Clear();
        _present.Clear();
        Advance();
    }

    private void Advance()
    {
        Revision++;
        if (Revision == 0) Revision++;
    }
}
