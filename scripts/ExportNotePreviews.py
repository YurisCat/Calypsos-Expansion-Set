"""Export front-only wiki images from the exact baked game textures.

python scripts/ExportNotePreviews.py --key handdrawn_duck_step
Requires Pillow only when exporting/verifying; the workbench serves static PNGs.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re

from PrepareNoteArtwork import ASSETS, FRONT_UV, Image, front_box
from NoteTextureFormat import unpack


def preview(key: str) -> Image.Image:
    if not re.fullmatch(r"[a-z][a-z0-9_]{0,63}", key):
        raise ValueError("Invalid artwork key.")
    record = json.loads((ASSETS / "Baked" / f"{key}.json").read_text(encoding="utf-8"))
    if record["front_uv"] != list(FRONT_UV):
        raise ValueError(f"{key}: the baked UV contract has changed; review the crop.")
    paper = unpack((ASSETS / "paper.note").read_bytes())
    raw = unpack((ASSETS / "Baked" / f"{key}.notedelta").read_bytes(), paper)
    if hashlib.sha256(raw).hexdigest() != record["rgba_sha256"]:
        raise ValueError(f"{key}: baked texture and metadata differ.")
    # Only the largest mip; runtime rows are bottom-up, PNG rows are top-down.
    atlas = Image.frombytes("RGBA", (512, 512), raw[:512 * 512 * 4])
    return atlas.transpose(Image.Transpose.FLIP_TOP_BOTTOM).crop(front_box()).convert("RGB")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--key", action="append", required=True)
    parser.add_argument("--verify", action="store_true")
    args = parser.parse_args()
    folder = ASSETS / "Previews"
    if not args.verify:
        folder.mkdir(exist_ok=True)
    for key in args.key:
        image = preview(key)
        output = folder / f"{key}.png"
        if args.verify:
            with Image.open(output) as saved:
                if saved.size != image.size or saved.convert("RGB").tobytes() != image.tobytes():
                    raise ValueError(f"{key}: preview differs from the baked front.")
        else:
            image.save(output)
        print(f"{key}: {image.width}x{image.height}, front {front_box()}")


if __name__ == "__main__":
    main()
