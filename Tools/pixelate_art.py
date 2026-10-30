"""
Pour Decisions - retro pixel pass.

Runs after generate_art.py. Each sprite is shrunk to a low resolution, limited to a small palette
and a few alpha levels, then scaled back up with nearest-neighbour so it keeps its exact size and
pivot (the art manifest and physics stay valid). Unity imports these with Point filtering so the
pixels stay crisp.

Run from the repo root:  python3 Tools/pixelate_art.py
"""
import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets/PourDecisions/Resources/Art")

# Size of one "retro pixel" in source pixels. Chosen so pixels look about the same size on screen.
BLOCK_BY_PREFIX = [
    ("bg_", 6), ("counter_", 6), ("customer_", 6),
    ("bottle_", 4), ("glass_", 6), ("spout_", 4),
    ("ui_ticket", 5), ("ui_bubble", 5), ("ui_panel", 4), ("ui_button", 4),
    ("tipjar_", 5), ("logo", 7), ("star_", 4), ("icon_", 3), ("coin", 3),
]
# Soft effect textures stay smooth (glows, vignette, the white pixel used for meshes).
SKIP_PREFIXES = ("fx_",)
PALETTE_COLORS = 24
BACKGROUND_COLORS = 40  # backgrounds have more going on (and a neon sign to keep)
ALPHA_LEVELS = 4


def block_for(name):
    for prefix, size in BLOCK_BY_PREFIX:
        if name.startswith(prefix):
            return size
    return None


def quantize_alpha(a, levels):
    step = 255 / (levels - 1)
    return a.point(lambda v: int(round(v / step) * step))


def pixelate(path, block):
    img = Image.open(path).convert("RGBA")
    w, h = img.size
    small = img.resize((max(1, w // block), max(1, h // block)), Image.BOX)
    r, g, b, a = small.split()
    colors = BACKGROUND_COLORS if os.path.basename(path).startswith("bg_") else PALETTE_COLORS
    rgb = Image.merge("RGB", (r, g, b)).quantize(colors=colors, method=Image.MEDIANCUT,
                                                 dither=Image.Dither.NONE).convert("RGB")
    out = Image.merge("RGBA", (*rgb.split(), quantize_alpha(a, ALPHA_LEVELS)))
    # Scale straight back to the exact original size so pivots and glass interiors line up with the physics.
    out.resize((w, h), Image.NEAREST).save(path)


def run():
    count = 0
    for f in sorted(os.listdir(ART)):
        if not f.endswith(".png") or f.startswith(SKIP_PREFIXES):
            continue
        block = block_for(f)
        if block:
            pixelate(os.path.join(ART, f), block)
            count += 1
    print("pixelated", count, "sprites")


if __name__ == "__main__":
    run()
