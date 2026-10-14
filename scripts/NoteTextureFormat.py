"""Lossless shared-paper/XOR note resources; decoded bytes remain RGBA32 mips."""
from __future__ import annotations

import hashlib
import struct
import zlib

MAGIC = 0x314E5343
RAW_LENGTH = 1398100
HEADER_LENGTH = 112


def pack(raw: bytes, paper: bytes | None = None) -> bytes:
    if len(raw) != RAW_LENGTH or paper is not None and len(paper) != RAW_LENGTH:
        raise ValueError("Expected the complete 512x512 RGBA32 mip chain.")
    difference = bytes(a ^ b for a, b in zip(raw, paper)) if paper is not None else raw
    payload = zlib.compress(difference, level=9)
    header = struct.pack("<IBBHI", MAGIC, 1, int(paper is not None), 0, RAW_LENGTH)
    header += hashlib.sha256(paper).digest() if paper is not None else bytes(32)
    header += hashlib.sha256(raw).digest() + hashlib.sha256(payload).digest()
    return header + struct.pack("<I", len(payload)) + payload


def unpack(packed: bytes, paper: bytes | None = None) -> bytes:
    if len(packed) < HEADER_LENGTH:
        raise ValueError("Truncated note header.")
    magic, version, kind, reserved, raw_length = struct.unpack_from("<IBBHI", packed)
    if (magic, version, kind, reserved, raw_length) != (MAGIC, 1, int(paper is not None), 0, RAW_LENGTH):
        raise ValueError("Invalid note header.")
    base_hash = hashlib.sha256(paper).digest() if paper is not None else bytes(32)
    payload = packed[HEADER_LENGTH:]
    if (packed[12:44] != base_hash or len(payload) != struct.unpack_from("<I", packed, 108)[0]
            or not payload or hashlib.sha256(payload).digest() != packed[76:108]):
        raise ValueError("Note base or payload differs.")
    decoder = zlib.decompressobj()
    raw = decoder.decompress(payload, RAW_LENGTH + 1)
    if len(raw) != RAW_LENGTH or not decoder.eof or decoder.unused_data or decoder.unconsumed_tail:
        raise ValueError("Invalid note decompression size/trailer.")
    if paper is not None:
        if len(paper) != RAW_LENGTH:
            raise ValueError("Invalid shared paper.")
        raw = bytes(a ^ b for a, b in zip(raw, paper))
    if hashlib.sha256(raw).digest() != packed[44:76]:
        raise ValueError("Restored mip chain differs.")
    return raw
