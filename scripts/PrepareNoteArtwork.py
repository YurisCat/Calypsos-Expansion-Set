"""Bake a transparent square drawing onto the current artist note's front UV.

Usage: python scripts/PrepareNoteArtwork.py --key jumping --input drawing.png
Requires Pillow. Does not build/deploy the plugin or modify the input file.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
# Optional local research tools; normal installations can use pip install Pillow.
if (ROOT / ".local/research/pydeps").is_dir():
    sys.path.insert(0, str(ROOT / ".local/research/pydeps"))
from PIL import Image
from NoteTextureFormat import pack, unpack

ASSETS = ROOT / "Assets/StickyNote"
FRONT_UV = (0.0203103777, 0.01061930135, 0.9824627042, 0.8712632656)


def compose(base: Image.Image, artwork: Image.Image) -> Image.Image:
    """Canvas top follows +V; canvas left follows min U (head-local +X)."""
    if base.size != (512, 512):
        raise ValueError("The current note contract requires a 512x512 base atlas.")
    base = base.convert("RGBA")
    if base.getchannel("A").getextrema() != (255, 255):
        raise ValueError("The paper/tape base must remain opaque.")
    if artwork.width != artwork.height or not 16 <= artwork.width <= 4096:
        raise ValueError("Artwork must be square, from 16 to 4096 pixels.")
    if "A" not in artwork.getbands() and "transparency" not in artwork.info:
        raise ValueError("Use a PNG with a transparent background.")
    artwork = artwork.convert("RGBA")
    if artwork.getchannel("A").getextrema()[0] == 255:
        raise ValueError("Artwork is fully opaque; hide the paper/reference layer before export.")
    u0, v0, u1, v1 = FRONT_UV
    box = (round(u0 * 512), round((1 - v1) * 512), round(u1 * 512), round((1 - v0) * 512))
    resized = artwork.resize((box[2] - box[0], box[3] - box[1]), Image.Resampling.LANCZOS)
    result = base.copy()
    result.alpha_composite(resized, dest=box[:2])
    return result


def mip_bytes(image: Image.Image) -> bytes:
    """Unity RGBA32, bottom row first per level, largest level first."""
    levels = []
    while True:
        levels.append(image.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes())
        if image.size == (1, 1):
            break
        image = image.resize((max(1, image.width // 2), max(1, image.height // 2)), Image.Resampling.BOX)
    return b"".join(levels)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--key", required=True)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--preview", type=Path, help="Optional composed atlas PNG for inspection.")
    args = parser.parse_args()
    if not re.fullmatch(r"[a-z][a-z0-9_]{0,63}", args.key):
        parser.error("key must be a stable lowercase identifier, e.g. jumping")
    source = args.input.resolve()
    source_bytes = source.read_bytes()
    with Image.open(io.BytesIO(source_bytes)) as art, Image.open(ASSETS / "paper.png") as base:
        if art.format != "PNG":
            parser.error("input must be PNG")
        composed = compose(base, art)
        source_size = art.size
        paper = mip_bytes(base.convert("RGBA"))
    raw = mip_bytes(composed)
    if len(raw) != 1398100:
        raise ValueError("Unexpected mip chain length.")
    artwork_path = ASSETS / "Artwork" / f"{args.key}.png"
    shared_path = ASSETS / "paper.note"
    if not shared_path.exists() or unpack(shared_path.read_bytes()) != paper:
        parser.error("Shared paper differs or is missing; repack all note resources before importing.")
    packed = pack(raw, paper)
    if unpack(packed, paper) != raw:
        raise ValueError("Lossless note round trip failed.")
    baked_path = ASSETS / "Baked" / f"{args.key}.notedelta"
    metadata_path = baked_path.with_suffix(".json")
    if source in (baked_path.resolve(), metadata_path.resolve()):
        parser.error("input cannot be one of the output files")
    if args.preview:
        preview = args.preview.resolve()
        if preview in (source, artwork_path.resolve(), baked_path.resolve(), metadata_path.resolve(),
                       (ASSETS / "paper.png").resolve(), shared_path.resolve()):
            parser.error("preview cannot overwrite a source or runtime resource")
        if preview.exists():
            parser.error("preview already exists; choose a new delivery filename")
        preview.parent.mkdir(parents=True, exist_ok=True)
        composed.save(preview, format="PNG")
    artwork_path.parent.mkdir(parents=True, exist_ok=True)
    baked_path.parent.mkdir(parents=True, exist_ok=True)
    # Preserve the exact submitted PNG, including its alpha, for future editing.
    if source != artwork_path.resolve():
        artwork_path.write_bytes(source_bytes)
    baked_path.write_bytes(packed)
    metadata = {
        "schema": "note-artwork-v2",
        "key": args.key,
        "source_size": source_size,
        "source_sha256": hashlib.sha256(source_bytes).hexdigest(),
        "base_sha256": hashlib.sha256((ASSETS / "paper.png").read_bytes()).hexdigest(),
        "mesh_sha256": hashlib.sha256((ASSETS / "note.mesh.json").read_bytes()).hexdigest(),
        "front_uv": FRONT_UV,
        "canvas_orientation": "top=+V; left=minU; no artwork mirroring",
        "runtime_format": "512x512 RGBA32; bottom-up rows; complete mip chain; opaque",
        "storage": "CSN1 v1; zlib-compressed XOR against shared paper.note",
        "paper_rgba_sha256": hashlib.sha256(paper).hexdigest(),
        "packed_sha256": hashlib.sha256(packed).hexdigest(),
        "packed_bytes": len(packed),
        "rgba_sha256": hashlib.sha256(raw).hexdigest(),
    }
    metadata_path.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(metadata, ensure_ascii=False, indent=2))
    print("Prepared resources. Rebuild/redeploy the plugin and restart the game to apply.")


if __name__ == "__main__":
    main()
