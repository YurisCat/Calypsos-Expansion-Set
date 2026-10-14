using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace CalypsoExpansionSet.Visuals;

// Pure managed resource decoding. Unity still receives the same RGBA32 mip chain.
internal static class NoteTextureCodec
{
    internal const uint Magic = 0x314E5343; // CSN1
    internal const int RawLength = 1398100, HeaderLength = 112;

    internal static byte[] DecodeBase(byte[] packed) => Decode(packed, null);
    internal static byte[] Restore(byte[] paper, byte[] packed)
    {
        if (paper.Length != RawLength) throw new InvalidDataException("Invalid shared paper mip chain.");
        return Decode(packed, paper);
    }

    private static byte[] Decode(byte[] packed, byte[]? paper)
    {
        if (packed.Length < HeaderLength || packed.Length > HeaderLength + RawLength * 2
            || BinaryPrimitives.ReadUInt32LittleEndian(packed) != Magic || packed[4] != 1
            || packed[5] != (paper == null ? 0 : 1) || packed[6] != 0 || packed[7] != 0
            || BinaryPrimitives.ReadInt32LittleEndian(packed.AsSpan(8)) != RawLength)
            throw new InvalidDataException("Invalid packed note header.");
        var length = BinaryPrimitives.ReadInt32LittleEndian(packed.AsSpan(108));
        if (length <= 0 || length != packed.Length - HeaderLength
            || !SHA256.HashData(packed.AsSpan(HeaderLength)).AsSpan().SequenceEqual(packed.AsSpan(76, 32)))
            throw new InvalidDataException("Invalid packed note payload.");
        var baseHash = paper == null ? new byte[32] : SHA256.HashData(paper);
        if (!baseHash.AsSpan().SequenceEqual(packed.AsSpan(12, 32)))
            throw new InvalidDataException("Note difference requires a different paper base.");
        var raw = new byte[RawLength];
        using (var input = new MemoryStream(packed, HeaderLength, length, false))
        using (var inflated = new ZLibStream(input, CompressionMode.Decompress))
        {
            var offset = 0;
            while (offset < raw.Length)
            {
                var read = inflated.Read(raw, offset, raw.Length - offset);
                if (read == 0) throw new InvalidDataException("Truncated note mip chain.");
                offset += read;
            }
            if (inflated.ReadByte() != -1) throw new InvalidDataException("Oversized note mip chain.");
        }
        if (paper != null)
            for (var index = 0; index < raw.Length; index++) raw[index] ^= paper[index];
        if (!SHA256.HashData(raw).AsSpan().SequenceEqual(packed.AsSpan(44, 32)))
            throw new InvalidDataException("Restored note mip chain hash differs.");
        return raw;
    }
}
