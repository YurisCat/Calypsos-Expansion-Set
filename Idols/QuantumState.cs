using System.Buffers.Binary;
using System.Numerics;

namespace CalypsoExpansionSet.Idols;

internal enum QuantumObservation { Unknown, Unseen, Seen }

// An unseen object cannot arm itself, including immediately after a teleport.
internal sealed class QuantumCycle
{
    internal bool Armed { get; private set; }

    internal void Saw() => Armed = true;
    internal bool Advance(QuantumObservation observation, double now)
    {
        if (observation == QuantumObservation.Seen) { Saw(); return false; }
        if (observation != QuantumObservation.Unseen || !Armed || !double.IsFinite(now)) return false;
        // Consume the edge even if the bounded placement search fails. Never
        // leave a delayed move waiting for a player or obstacle to move.
        Armed = false;
        return true;
    }
    internal void Reset() => Armed = false;
}

internal readonly record struct QuantumPoint(float X, float Y, float Z)
{
    internal bool Valid => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z)
        && Math.Abs(X) <= 100000 && Math.Abs(Y) <= 100000 && Math.Abs(Z) <= 100000;
}

[Flags]
internal enum QuantumFlags : byte { None = 0, Active = 1, InHall = 2 }
[Flags]
internal enum QuantumSight : byte { Unknown = 0, Valid = 1, Visible = 2, Saw = 4 }
internal readonly record struct QuantumEntry(uint Id, uint Revision, QuantumFlags Flags,
    QuantumPoint Position);
internal readonly record struct QuantumReport(uint Id, uint Revision, QuantumSight Sight);

// Capture once from the actual rendered camera, then test every candidate on
// the server without another destination proposal/acknowledgement round trip.
internal readonly record struct QuantumCamera(Vector4 X, Vector4 Y, Vector4 Z, Vector4 W)
{
    private static bool FiniteVector(Vector4 v) => float.IsFinite(v.X) && float.IsFinite(v.Y)
        && float.IsFinite(v.Z) && float.IsFinite(v.W) && Math.Abs(v.X) <= 1e8f
        && Math.Abs(v.Y) <= 1e8f && Math.Abs(v.Z) <= 1e8f && Math.Abs(v.W) <= 1e8f;
    internal bool Finite => FiniteVector(X) && FiniteVector(Y) && FiniteVector(Z) && FiniteVector(W);
    internal bool Valid
    {
        get
        {
            if (!Finite) return false;
            var determinant = new Matrix4x4(X.X, X.Y, X.Z, X.W, Y.X, Y.Y, Y.Z, Y.W,
                Z.X, Z.Y, Z.Z, Z.W, W.X, W.Y, W.Z, W.W).GetDeterminant();
            return float.IsFinite(determinant) && determinant != 0;
        }
    }
    internal QuantumFrustum Frustum => new(X, Y, Z, W);
}

internal struct QuantumConsensus
{
    private bool _unknown, _seen;
    internal QuantumObservation Observation => _seen ? QuantumObservation.Seen
        : _unknown ? QuantumObservation.Unknown : QuantumObservation.Unseen;
    internal QuantumConsensus(bool rosterReady, int observers)
    {
        _unknown = !rosterReady || observers == 0; _seen = false;
    }
    internal void Missing() => _unknown = true;
    internal void Add(QuantumReport report, double sampled, double now, uint revision)
    {
        if (report.Revision != revision || !QuantumWire.Fresh(sampled, now) || !report.Sight.HasFlag(QuantumSight.Valid))
        { Missing(); return; }
        if (report.Sight.HasFlag(QuantumSight.Visible)) _seen = true;
    }
}

internal readonly struct QuantumFrustum
{
    private readonly Vector4 _left, _right, _bottom, _top, _near, _far;
    internal QuantumFrustum(Vector4 x, Vector4 y, Vector4 z, Vector4 w)
    {
        // Keep a margin at the edge: uncertainty should lock an idol, not move it.
        _left = w * 1.08f + x; _right = w * 1.08f - x;
        _bottom = w * 1.08f + y; _top = w * 1.08f - y;
        _near = w + z; _far = w - z;
    }
    private static bool Outside(Vector4 p, QuantumPoint center, QuantumPoint extents) =>
        p.X * center.X + p.Y * center.Y + p.Z * center.Z + p.W
        + Math.Abs(p.X) * extents.X + Math.Abs(p.Y) * extents.Y + Math.Abs(p.Z) * extents.Z < 0;
    internal bool MayContain(QuantumPoint center, QuantumPoint extents) =>
        !Outside(_left, center, extents) && !Outside(_right, center, extents)
        && !Outside(_bottom, center, extents) && !Outside(_top, center, extents)
        && !Outside(_near, center, extents) && !Outside(_far, center, extents);
}

// Both real transport handlers and offline checks use this exact bounded format.
internal static class QuantumWire
{
    internal const ushort MessageId = 0x4D51;
    internal const uint Magic = 0x43535154;
    internal const byte Protocol = 2, SnapshotKind = 1, ReportKind = 2;
    internal const int Capacity = 64, CommonSize = 30, SnapshotHeader = 32, EntrySize = 21,
        ReportHeader = 112, ReportSize = 9;
    internal const double Freshness = 0.5;

    internal static bool Newer(uint value, uint previous) => value != 0 && unchecked((int)(value - previous)) > 0;
    internal static uint Next(uint value) => value == uint.MaxValue ? 1 : value + 1;
    internal static bool Fresh(double sampled, double now) => double.IsFinite(sampled) && double.IsFinite(now)
        && sampled <= now + 0.15 && now - sampled <= Freshness;

    private static void U32(Span<byte> b, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b[at..], value);
    private static uint U32(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static void Point(Span<byte> b, int at, QuantumPoint p)
    { U32(b, at, BitConverter.SingleToUInt32Bits(p.X)); U32(b, at + 4, BitConverter.SingleToUInt32Bits(p.Y)); U32(b, at + 8, BitConverter.SingleToUInt32Bits(p.Z)); }
    private static QuantumPoint Point(ReadOnlySpan<byte> b, int at) => new(BitConverter.UInt32BitsToSingle(U32(b, at)),
        BitConverter.UInt32BitsToSingle(U32(b, at + 4)), BitConverter.UInt32BitsToSingle(U32(b, at + 8)));
    private static void Vector(Span<byte> b, int at, Vector4 v)
    { Point(b, at, new(v.X, v.Y, v.Z)); U32(b, at + 12, BitConverter.SingleToUInt32Bits(v.W)); }
    private static Vector4 Vector(ReadOnlySpan<byte> b, int at)
    { var p = Point(b, at); return new(p.X, p.Y, p.Z, BitConverter.UInt32BitsToSingle(U32(b, at + 12))); }
    private static byte[] Header(int length, byte kind, ulong manifest, uint sequence, uint epoch, double time)
    {
        var b = new byte[length]; U32(b, 0, Magic); b[4] = Protocol; b[5] = kind;
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(6), manifest);
        U32(b, 14, sequence); U32(b, 18, epoch);
        BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(22), BitConverter.DoubleToInt64Bits(time));
        return b;
    }
    private static bool ReadHeader(ReadOnlySpan<byte> b, byte kind, ulong manifest,
        out uint sequence, out uint epoch, out double time)
    {
        sequence = epoch = 0; time = double.NaN;
        if (b.Length < CommonSize || U32(b, 0) != Magic || b[4] != Protocol || b[5] != kind
            || BinaryPrimitives.ReadUInt64LittleEndian(b[6..]) != manifest) return false;
        sequence = U32(b, 14); epoch = U32(b, 18);
        time = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(b[22..]));
        return sequence != 0 && epoch != 0 && double.IsFinite(time);
    }

    internal static byte[] Snapshot(IReadOnlyList<QuantumEntry> rows, ulong manifest, uint sequence, uint epoch, double time)
    {
        var b = Header(SnapshotHeader + rows.Count * EntrySize, SnapshotKind, manifest, sequence, epoch, time);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(30), checked((ushort)rows.Count));
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i]; var at = SnapshotHeader + i * EntrySize;
            U32(b, at, r.Id); U32(b, at + 4, r.Revision); b[at + 8] = (byte)r.Flags;
            Point(b, at + 9, r.Position);
        }
        return b;
    }
    internal static bool TrySnapshot(ReadOnlySpan<byte> b, ulong manifest, out uint sequence, out uint epoch,
        out double time, out QuantumEntry[] rows)
    {
        rows = Array.Empty<QuantumEntry>();
        if (!ReadHeader(b, SnapshotKind, manifest, out sequence, out epoch, out time) || b.Length < SnapshotHeader) return false;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(b[30..]);
        if (count > Capacity || b.Length != SnapshotHeader + count * EntrySize) return false;
        var parsed = new QuantumEntry[count]; var ids = new HashSet<uint>();
        for (var i = 0; i < count; i++)
        {
            var at = SnapshotHeader + i * EntrySize;
            var row = new QuantumEntry(U32(b, at), U32(b, at + 4), (QuantumFlags)b[at + 8],
                Point(b, at + 9));
            if (row.Id == 0 || row.Revision == 0 || !ids.Add(row.Id) || !row.Position.Valid
                || row.Flags is not (QuantumFlags.None or QuantumFlags.Active or QuantumFlags.InHall)) return false;
            parsed[i] = row;
        }
        rows = parsed; return true;
    }

    internal static byte[] Report(IReadOnlyList<QuantumReport> rows, uint player, QuantumPoint origin, QuantumCamera camera,
        ulong manifest, uint sequence, uint epoch, double time)
    {
        var b = Header(ReportHeader + rows.Count * ReportSize, ReportKind, manifest, sequence, epoch, time);
        U32(b, 30, player); Point(b, 34, origin);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(46), checked((ushort)rows.Count));
        Vector(b, 48, camera.X); Vector(b, 64, camera.Y); Vector(b, 80, camera.Z); Vector(b, 96, camera.W);
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i]; var at = ReportHeader + i * ReportSize;
            U32(b, at, r.Id); U32(b, at + 4, r.Revision); b[at + 8] = (byte)r.Sight;
        }
        return b;
    }
    internal static bool TryReport(ReadOnlySpan<byte> b, ulong manifest, out uint sequence, out uint epoch,
        out double time, out uint player, out QuantumPoint origin, out QuantumCamera camera, out QuantumReport[] rows)
    {
        player = 0; origin = default; camera = default; rows = Array.Empty<QuantumReport>();
        if (!ReadHeader(b, ReportKind, manifest, out sequence, out epoch, out time) || b.Length < ReportHeader) return false;
        player = U32(b, 30); origin = Point(b, 34); var count = BinaryPrimitives.ReadUInt16LittleEndian(b[46..]);
        if (player == 0 || !origin.Valid || count > Capacity || b.Length != ReportHeader + count * ReportSize) return false;
        camera = new(Vector(b, 48), Vector(b, 64), Vector(b, 80), Vector(b, 96));
        if (!camera.Finite) return false;
        var cameraValid = camera.Valid;
        var parsed = new QuantumReport[count]; var ids = new HashSet<uint>();
        for (var i = 0; i < count; i++)
        {
            var at = ReportHeader + i * ReportSize;
            var row = new QuantumReport(U32(b, at), U32(b, at + 4), (QuantumSight)b[at + 8]);
            if (row.Id == 0 || row.Revision == 0 || !ids.Add(row.Id) || ((byte)row.Sight & ~7) != 0
                || row.Sight != 0 && !row.Sight.HasFlag(QuantumSight.Valid)
                || row.Sight.HasFlag(QuantumSight.Valid) && !cameraValid) return false;
            parsed[i] = row;
        }
        rows = parsed; return true;
    }
}
