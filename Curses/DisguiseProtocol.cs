using System.Buffers.Binary;
using System.Text;

namespace CalypsoExpansionSet.Curses;

internal static class DisguiseProtocol
{
    internal const ushort MessageId = 0x4D4F;
    internal const uint Magic = 0x43455344;
    internal const byte Version = 2;
    internal const int HeaderLength = 25, EntryLength = 13, MaximumNickBytes = 128;
    internal const int MaximumLength = HeaderLength + DisguiseRound.MaximumPlayers * (EntryLength + MaximumNickBytes);
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal sealed record Snapshot(uint Sequence, uint Revision, bool Active, DisguiseEntry[] Entries, bool PlayArrival);

    internal static string LimitNick(string nick)
    {
        // Enumerating runes also replaces unpaired UTF-16 surrogates before encoding.
        var result = new StringBuilder(Math.Min(nick.Length, MaximumNickBytes));
        var bytes = 0;
        foreach (var rune in nick.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > MaximumNickBytes) break;
            result.Append(rune.ToString());
            bytes += rune.Utf8SequenceLength;
        }
        return result.ToString();
    }

    internal static byte[] Encode(ulong manifest, uint sequence, uint revision, bool active,
        IReadOnlyList<DisguiseEntry> entries, bool playArrival = false)
    {
        if (sequence == 0 || entries.Count > DisguiseRound.MaximumPlayers || !active && entries.Count != 0
            || playArrival && (!active || entries.Count < 2))
            throw new ArgumentException("Invalid disguise snapshot.");
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Utf8);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write((byte)(playArrival ? 2 : 1));
        writer.Write(manifest);
        writer.Write(sequence);
        writer.Write(revision);
        writer.Write(active);
        writer.Write((ushort)entries.Count);
        foreach (var entry in entries)
        {
            var nick = Utf8.GetBytes(entry.Look.Nick);
            if (nick.Length > MaximumNickBytes) throw new ArgumentException("Disguise nickname exceeds its limit.");
            writer.Write(entry.User);
            writer.Write(entry.Player);
            writer.Write(entry.Look.Character);
            writer.Write(entry.Look.Cloth);
            writer.Write(entry.Look.Headgear);
            writer.Write((ushort)nick.Length);
            writer.Write(nick);
        }
        return stream.ToArray();
    }

    internal static bool TryDecode(ReadOnlySpan<byte> data, ulong manifest, uint previousSequence, uint previousRevision,
        out Snapshot? snapshot)
    {
        snapshot = null;
        if (data.Length < HeaderLength || data.Length > MaximumLength
            || BinaryPrimitives.ReadUInt32LittleEndian(data) != Magic || data[4] != Version || data[5] is < 1 or > 2
            || BinaryPrimitives.ReadUInt64LittleEndian(data[6..]) != manifest || data[22] > 1) return false;
        var sequence = BinaryPrimitives.ReadUInt32LittleEndian(data[14..]);
        var revision = BinaryPrimitives.ReadUInt32LittleEndian(data[18..]);
        var active = data[22] != 0;
        var playArrival = data[5] == 2;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data[23..]);
        if (!Newer(sequence, previousSequence) || revision != previousRevision && !Newer(revision, previousRevision)
            || active && revision == 0 || count > DisguiseRound.MaximumPlayers || !active && count != 0
            || playArrival && (!active || count < 2)
            || data.Length < HeaderLength + count * EntryLength) return false;
        var entries = new DisguiseEntry[count];
        var users = new HashSet<uint>();
        var players = new HashSet<uint>();
        var offset = HeaderLength;
        for (var index = 0; index < count; index++)
        {
            if (data.Length - offset < EntryLength) return false;
            var row = data[offset..];
            var user = BinaryPrimitives.ReadUInt32LittleEndian(row);
            var player = BinaryPrimitives.ReadUInt32LittleEndian(row[4..]);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(row[11..]);
            // These are vanilla character/color/headgear IDs, not arbitrary asset IDs.
            if (user == 0 || player == 0 || !users.Add(user) || !players.Add(player)
                || row[8] > 7 || row[9] > 8 || (row[10] & ~6) != 0
                || length > MaximumNickBytes || data.Length - offset - EntryLength < length) return false;
            string nick;
            try { nick = Utf8.GetString(row.Slice(EntryLength, length)); }
            catch (DecoderFallbackException) { return false; }
            entries[index] = new DisguiseEntry(user, player, new DisguiseLook(row[8], row[9], row[10], nick));
            offset += EntryLength + length;
        }
        if (offset != data.Length) return false;
        snapshot = new Snapshot(sequence, revision, active, entries, playArrival);
        return true;
    }

    internal static bool Newer(uint candidate, uint previous) => candidate != 0 && unchecked((int)(candidate - previous)) > 0;
}
