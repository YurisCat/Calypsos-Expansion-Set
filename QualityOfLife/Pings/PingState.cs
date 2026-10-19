using System.Buffers.Binary;

namespace CalypsoExpansionSet.QualityOfLife.Pings;

internal readonly record struct PingPoint(float X, float Y, float Z)
{
    internal bool Finite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
}

internal readonly record struct PingEntry(uint Owner, uint Target, byte Slot, PingPoint Point, double Expires, uint Revision);
internal readonly record struct PingRequest(uint Sequence, uint Epoch, uint Target, PingPoint Origin, PingPoint Direction);
internal readonly record struct PingCancel(uint Sequence, uint Epoch, uint Revision);

// One dictionary entry per user is the shared quota for both locations and items.
internal sealed class PingState
{
    internal const double Lifetime = 60;
    internal const int Capacity = 128;
    internal readonly Dictionary<uint, PingEntry> Entries = new();
    private readonly List<uint> _removed = new();
    private uint _revision;

    internal void Put(uint owner, uint target, byte slot, PingPoint point, double now)
    {
        // NetworkTime can be identical for requests accepted in the same frame.
        _revision = _revision == uint.MaxValue ? 1 : _revision + 1;
        Entries[owner] = new PingEntry(owner, target, slot, point, now + Lifetime, _revision);
    }

    internal bool Cancel(uint owner, uint revision) =>
        Entries.TryGetValue(owner, out var row) && row.Revision == revision && Entries.Remove(owner);

    internal bool Prune(double now, Func<PingEntry, bool> valid)
    {
        _removed.Clear();
        foreach (var row in Entries.Values)
            if (row.Expires <= now || !valid(row)) _removed.Add(row.Owner);
        foreach (var key in _removed) Entries.Remove(key);
        return _removed.Count != 0;
    }

    internal static bool Newer(uint candidate, uint previous) =>
        candidate != 0 && unchecked((int)(candidate - previous)) > 0;
}

// A bounded, exact-length wire format shared by the real handlers and offline checks.
internal static class PingWire
{
    internal const uint Magic = 0x43455350;
    internal const byte Protocol = 3, RequestKind = 1, SnapshotKind = 2, CancelKind = 3;
    internal const int CommonSize = 22, RequestSize = 50, CancelSize = 26, SnapshotHeader = 32, EntrySize = 33;

    private static void U32(Span<byte> b, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b[at..], value);
    private static uint U32(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static void F32(Span<byte> b, int at, float value) => U32(b, at, BitConverter.SingleToUInt32Bits(value));
    private static float F32(ReadOnlySpan<byte> b, int at) => BitConverter.UInt32BitsToSingle(U32(b, at));
    private static void F64(Span<byte> b, int at, double value) =>
        BinaryPrimitives.WriteInt64LittleEndian(b[at..], BitConverter.DoubleToInt64Bits(value));
    private static double F64(ReadOnlySpan<byte> b, int at) =>
        BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(b[at..]));
    private static void Point(Span<byte> b, int at, PingPoint p)
    { F32(b, at, p.X); F32(b, at + 4, p.Y); F32(b, at + 8, p.Z); }
    private static PingPoint Point(ReadOnlySpan<byte> b, int at) => new(F32(b, at), F32(b, at + 4), F32(b, at + 8));

    private static void Header(Span<byte> b, byte kind, ulong manifest, uint sequence, uint epoch)
    {
        U32(b, 0, Magic); b[4] = Protocol; b[5] = kind;
        BinaryPrimitives.WriteUInt64LittleEndian(b[6..], manifest);
        U32(b, 14, sequence); U32(b, 18, epoch);
    }

    private static bool Header(ReadOnlySpan<byte> b, byte kind, ulong manifest) => b.Length >= CommonSize
        && U32(b, 0) == Magic && b[4] == Protocol && b[5] == kind
        && BinaryPrimitives.ReadUInt64LittleEndian(b[6..]) == manifest
        && U32(b, 14) != 0 && U32(b, 18) != 0;

    internal static byte[] Request(PingRequest row, ulong manifest)
    {
        var bytes = new byte[RequestSize]; Header(bytes, RequestKind, manifest, row.Sequence, row.Epoch);
        U32(bytes, 22, row.Target); Point(bytes, 26, row.Origin); Point(bytes, 38, row.Direction);
        return bytes;
    }

    internal static bool TryRequest(ReadOnlySpan<byte> b, ulong manifest, out PingRequest row)
    {
        row = default;
        if (b.Length != RequestSize || !Header(b, RequestKind, manifest)) return false;
        row = new PingRequest(U32(b, 14), U32(b, 18), U32(b, 22), Point(b, 26), Point(b, 38));
        return row.Origin.Finite && row.Direction.Finite;
    }

    internal static byte[] Cancel(PingCancel row, ulong manifest)
    {
        var bytes = new byte[CancelSize]; Header(bytes, CancelKind, manifest, row.Sequence, row.Epoch);
        U32(bytes, 22, row.Revision);
        return bytes;
    }

    internal static bool TryCancel(ReadOnlySpan<byte> b, ulong manifest, out PingCancel row)
    {
        row = default;
        if (b.Length != CancelSize || !Header(b, CancelKind, manifest) || U32(b, 22) == 0) return false;
        row = new PingCancel(U32(b, 14), U32(b, 18), U32(b, 22));
        return true;
    }

    internal static byte[] Snapshot(IEnumerable<PingEntry> rows, int count, ulong manifest, uint sequence, uint epoch, double now)
    {
        if (count < 0 || count > PingState.Capacity) throw new InvalidDataException("Ping capacity exceeded.");
        var bytes = new byte[SnapshotHeader + count * EntrySize];
        Header(bytes, SnapshotKind, manifest, sequence, epoch); F64(bytes, 22, now);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(30), (ushort)count);
        var at = SnapshotHeader;
        foreach (var row in rows)
        {
            U32(bytes, at, row.Owner); U32(bytes, at + 4, row.Target); bytes[at + 8] = row.Slot;
            Point(bytes, at + 9, row.Point); F64(bytes, at + 21, row.Expires);
            U32(bytes, at + 29, row.Revision); at += EntrySize;
        }
        if (at != bytes.Length) throw new InvalidDataException("Ping count changed while writing.");
        return bytes;
    }

    internal static bool TrySnapshot(ReadOnlySpan<byte> b, ulong manifest, out uint sequence, out uint epoch,
        out double sentAt, out PingEntry[] rows)
    {
        sequence = epoch = 0; sentAt = 0; rows = Array.Empty<PingEntry>();
        if (b.Length < SnapshotHeader || !Header(b, SnapshotKind, manifest)) return false;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(b[30..]);
        sentAt = F64(b, 22);
        if (count > PingState.Capacity || b.Length != SnapshotHeader + count * EntrySize
            || !double.IsFinite(sentAt) || sentAt < 0) return false;
        var owners = new HashSet<uint>();
        var slots = new HashSet<byte>();
        var revisions = new HashSet<uint>();
        var incoming = new PingEntry[count];
        for (var i = 0; i < count; i++)
        {
            var at = SnapshotHeader + i * EntrySize;
            var row = new PingEntry(U32(b, at), U32(b, at + 4), b[at + 8], Point(b, at + 9), F64(b, at + 21), U32(b, at + 29));
            if (row.Owner == 0 || row.Slot >= PingState.Capacity || !row.Point.Finite || !double.IsFinite(row.Expires)
                || row.Expires <= sentAt || row.Expires > sentAt + PingState.Lifetime + 0.01
                || row.Revision == 0 || !revisions.Add(row.Revision)
                || !owners.Add(row.Owner) || !slots.Add(row.Slot)) return false;
            incoming[i] = row;
        }
        sequence = U32(b, 14); epoch = U32(b, 18); rows = incoming;
        return true;
    }
}

internal static class PingProjection
{
    // Clip x/y with positive forward distance; behind-camera directions must not
    // mirror to the wrong edge. The exact rear-axis tie points downward.
    internal static (float X, float Y, bool Edge) Place(float x, float y, float forward, float width, float height, float margin)
    {
        if (forward > 0 && Math.Abs(x) <= 1 && Math.Abs(y) <= 1)
            return ((x + 1) * width * 0.5f, (y + 1) * height * 0.5f, false);
        var dx = x * width * 0.5f; var dy = y * height * 0.5f;
        if (Math.Abs(dx) + Math.Abs(dy) < 0.001f) dy = -1;
        var halfW = Math.Max(1, width * 0.5f - margin); var halfH = Math.Max(1, height * 0.5f - margin);
        var scale = Math.Min(halfW / Math.Max(0.0001f, Math.Abs(dx)), halfH / Math.Max(0.0001f, Math.Abs(dy)));
        return (width * 0.5f + dx * scale, height * 0.5f + dy * scale, true);
    }
}
