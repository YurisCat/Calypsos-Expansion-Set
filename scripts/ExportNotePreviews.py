"""Export yellow wiki notes cropped inside the drawing reference's blue frame.

python scripts/ExportNotePreviews.py --key handdrawn_duck_step
Requires Pillow only when exporting/verifying; the workbench serves static PNGs.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re

from PrepareNoteArtwork import ASSETS, Image


def layout() -> dict:
    value = json.loads((ASSETS / "PreviewLayout.json").read_text(encoding="utf-8"))
    if value["schema"] != "ces-note-preview-v2":
        raise ValueError("Unsupported wiki preview layout.")
    width, height = value["reference_canvas"]
    left, top, right, bottom = value["crop"]
    if not (0 <= left < right <= width and 0 <= top < bottom <= height):
        raise ValueError("Preview crop must be inside the reference canvas.")
    return value


def preview(key: str) -> Image.Image:
    if not re.fullmatch(r"[a-z][a-z0-9_]{0,63}", key):
        raise ValueError("Invalid artwork key.")
    record = json.loads((ASSETS / "Baked" / f"{key}.json").read_text(encoding="utf-8"))
    source = ASSETS / "Artwork" / f"{key}.png"
    if hashlib.sha256(source.read_bytes()).hexdigest() != record["source_sha256"]:
        raise ValueError(f"{key}: source drawing and import metadata differ.")
    settings = layout()
    with Image.open(source) as source_image:
        art = source_image.convert("RGBA")
    width, height = settings["reference_canvas"]
    # Apply the reference selection to the original drawing once. The baked UV
    # rectangle is a different coordinate space and is deliberately not used.
    left, top, right, bottom = settings["crop"]
    box = (round(left * art.width / width), round(top * art.height / height),
           round(right * art.width / width), round(bottom * art.height / height))
    cropped = art.crop(box)
    note = Image.new("RGBA", cropped.size, settings["background"])
    note.alpha_composite(cropped)
    return note.convert("RGB")


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
                    raise ValueError(f"{key}: preview differs from the blue-frame crop.")
        else:
            image.save(output)
        print(f"{key}: {image.width}x{image.height}, blue-frame crop {layout()['crop']}")


if __name__ == "__main__":
    main()
