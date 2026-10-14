"""Pack/verify the shared paper and lossless artwork differences from PNG sources.

Before migration, legacy RGBA mip chains are compared byte for byte as well.
This tool never deletes sources. Normal builds only read the packed resources.
"""
from __future__ import annotations

import argparse
import hashlib
import json

from PrepareNoteArtwork import ASSETS, compose, mip_bytes, Image
from NoteTextureFormat import pack, unpack


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", action="store_true", help="Check existing files without writing.")
    args = parser.parse_args()
    with Image.open(ASSETS / "paper.png") as image:
        base = image.convert("RGBA")
    paper = mip_bytes(base)
    legacy_base = ASSETS / "paper.rgba"
    if legacy_base.exists() and legacy_base.read_bytes() != paper:
        raise ValueError("PNG base differs from the previous RGBA mip chain.")
    packed_base = pack(paper)
    assert unpack(packed_base) == paper
    target_base = ASSETS / "paper.note"
    if args.verify:
        assert target_base.read_bytes() == packed_base
    else:
        target_base.write_bytes(packed_base)
    before, after, count = len(paper), len(packed_base), 0
    for record in sorted((ASSETS / "Baked").glob("*.json")):
        metadata = json.loads(record.read_text(encoding="utf-8"))
        key = metadata["key"]
        source = ASSETS / "Artwork" / f"{key}.png"
        assert hashlib.sha256(source.read_bytes()).hexdigest() == metadata["source_sha256"], key
        with Image.open(source) as artwork:
            raw = mip_bytes(compose(base, artwork))
        assert hashlib.sha256(raw).hexdigest() == metadata["rgba_sha256"], f"{key}: mip bytes changed"
        legacy = record.with_suffix(".rgba")
        if legacy.exists():
            assert legacy.read_bytes() == raw, f"{key}: legacy pixels differ"
        packed = pack(raw, paper)
        assert unpack(packed, paper) == raw
        target = record.with_suffix(".notedelta")
        metadata.update(
            schema="note-artwork-v2",
            storage="CSN1 v1; zlib-compressed XOR against shared paper.note",
            paper_rgba_sha256=hashlib.sha256(paper).hexdigest(),
            packed_sha256=hashlib.sha256(packed).hexdigest(),
            packed_bytes=len(packed),
        )
        if args.verify:
            assert target.read_bytes() == packed
            assert json.loads(record.read_text(encoding="utf-8")) == metadata
        else:
            target.write_bytes(packed)
            record.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        before += len(raw)
        after += len(packed)
        count += 1
        print(f"{key}: {len(raw)} -> {len(packed)} bytes; exact mip chain restored")
    print(json.dumps({"artworks": count, "beforeBytes": before, "afterBytes": after,
                      "savedBytes": before - after, "savedPercent": round((1 - after / before) * 100, 2)}))


if __name__ == "__main__":
    main()
