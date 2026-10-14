"""Rasterize the workbench's temporary line icons; never modifies hand-drawn art.

Requires Pillow and CairoSVG. The output contains transparent 1024px PNGs and
editable SVGs. Import the PNGs using PrepareNoteArtwork.py with todo_<key>.
"""
from __future__ import annotations

import argparse
import io
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
if (ROOT / ".local/research/pydeps").is_dir():
    sys.path.insert(0, str(ROOT / ".local/research/pydeps"))
import cairosvg
from PIL import Image


def render(item, manifest):
    body = item["svg_body"]
    # This asset source is geometry only: no fonts, links or external resources.
    fragment = ET.fromstring("<g>" + body + "</g>")
    if any(node.tag not in {"g", "path", "circle", "rect", "ellipse", "line", "polyline", "polygon"}
           or any("href" in key or "url(" in value for key, value in node.attrib.items())
           for node in fragment.iter()):
        raise ValueError("Temporary icons must contain only local vector geometry.")
    x0, y0, x1, y1 = manifest["icon_box"]
    scale = min(x1 - x0, y1 - y0) / 24
    svg = (
        '<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">'
        f'<g transform="translate({x0} {y0}) scale({scale})" fill="none" '
        f'stroke="{manifest["stroke"]}" stroke-width="{manifest["stroke_width"]}" '
        f'stroke-linecap="round" stroke-linejoin="round">{body}</g></svg>'
    )
    png = cairosvg.svg2png(bytestring=svg.encode("utf-8"))
    with Image.open(io.BytesIO(png)) as image:
        if image.size != (1024, 1024) or image.mode != "RGBA":
            raise ValueError("Expected transparent 1024x1024 RGBA output.")
        bounds = image.getchannel("A").getbbox()
        if bounds is None or bounds[0] < 103 or bounds[1] < 185 or bounds[2] > 922 or bounds[3] > 922:
            raise ValueError("Icon extends beyond the drawing template's suggested safe area.")
    return svg, png


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="New delivery directory for source PNG/SVG files.")
    args = parser.parse_args()
    manifest = json.loads((ROOT / "Assets/StickyNote/TemporaryIcons.json").read_text(encoding="utf-8"))
    if manifest["schema"] != "ces-todo-note-icons-v1":
        raise ValueError("Unknown temporary icon manifest.")
    prepared = []
    for item in manifest["items"]:
        name = "todo_" + item["key"]
        if any((args.output / (name + extension)).exists() for extension in (".png", ".svg")):
            raise FileExistsError(f"Output already exists: {name}; choose a new directory.")
        svg, png = render(item, manifest)
        prepared.append((name, svg, png))
    args.output.mkdir(parents=True, exist_ok=True)
    for name, svg, png in prepared:
        (args.output / (name + ".svg")).write_text(svg, encoding="utf-8")
        (args.output / (name + ".png")).write_bytes(png)
        print(name)
    print("Transparent icons prepared; original hand-drawn artwork has not been changed.")


if __name__ == "__main__":
    main()
