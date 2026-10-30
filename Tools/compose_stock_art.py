"""
Pour Decisions - builds the Bar background and counter from the CC0 "Free Bar Assets Pack" by Styloo
(Tools/ThirdParty/StylooBar, see LICENSE.txt there).

The pack's pieces are tiny pixel art, so the scene is laid out on a 320x180 "logical" canvas and then
scaled up exactly 6x with nearest-neighbour, matching the 6px retro pixels used by the rest of the art.
Runs after pixelate_art.py (generate_art.py calls it), so these files are not pixelated twice.

Run from the repo root:  python3 Tools/compose_stock_art.py
"""
import os

from PIL import Image, ImageEnhance

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACK = os.path.join(ROOT, "Tools/ThirdParty/StylooBar")
ART = os.path.join(ROOT, "Assets/PourDecisions/Resources/Art")

SCALE = 6                  # logical pixel -> screen pixel
W, H = 320, 180            # 1920x1080 / 6
COUNTER_TOP = 132          # logical row where the counter sprite begins (pixel row 795 of the screen)
COUNTER_HEIGHT_PX = 285    # counter sprite height the game expects


def piece(name):
    return Image.open(os.path.join(PACK, name + ".png")).convert("RGBA")


def paste(canvas, img, x, y):
    canvas.alpha_composite(img, (int(x), int(y)))


def stock_shelf():
    """The pack's empty shelf, stocked with its bottles, jars and boxes."""
    shelf = piece("shelf")
    items = {"b": piece("bottles"), "j": piece("jars"), "x": piece("boxes")}
    # compartment left edges and floors inside the 117x81 shelf sprite
    cols = [5, 43, 81]
    floors = [20, 38, 56, 74]
    layout = ["bjx", "xbb", "jbj", "bxb"]
    for row, line in enumerate(layout):
        for col, key in enumerate(line):
            item = items[key]
            paste(shelf, item, cols[col] + (32 - item.width) // 2, floors[row] - item.height)
    return shelf


def compose_background():
    bg = piece("wall_bar").resize((W, H), Image.NEAREST)  # the wall is a vertical gradient, so stretching is fine
    paste(bg, piece("beam").resize((W, 14), Image.NEAREST), 0, 4)
    lamp = piece("ampule")
    for x in (52, 84, 232, 262):
        paste(bg, lamp, x, 18)
    shelf = stock_shelf()
    shelf_x = (W - shelf.width) // 2 + 10
    paste(bg, shelf, shelf_x, COUNTER_TOP - shelf.height + 4)
    paste(bg, piece("Greenboard_weird_writing"), shelf_x - 2, 20)
    paste(bg, piece("blueboard_Irasshaimase"), shelf_x + shelf.width - 45, 20)
    paste(bg, piece("vase_flowers"), shelf_x + shelf.width // 2 - 7, COUNTER_TOP - shelf.height + 4 - 26)
    paste(bg, piece("fridge"), 66, COUNTER_TOP - 57)
    paste(bg, piece("attach"), 48, 70)
    paste(bg, piece("watch"), shelf_x + shelf.width + 10, 58)
    paste(bg, piece("umbrella"), shelf_x + shelf.width + 14, COUNTER_TOP - 38)
    # a touch darker so the glass and liquid in front read clearly
    bg = ImageEnhance.Brightness(bg).enhance(0.85)
    bg.resize((W * SCALE, H * SCALE), Image.NEAREST).save(os.path.join(ART, "bg_bar.png"))


def compose_counter():
    """Stretch the middle of the pack's counter across the screen and thicken its top so glasses stand on it."""
    bar = piece("bar")                       # 127x47: wooden top band over a dark front
    middle = bar.crop((24, 0, 100, bar.height))
    # Rows 0-2 are the light top surface, 3-6 the front edge. Doubling the surface (6 rows = 36 px)
    # puts the line the glasses stand on (25 px down) on the wood top instead of the edge.
    surface = middle.crop((0, 0, middle.width, 3))
    rows = [surface, surface, middle.crop((0, 3, middle.width, middle.height))]
    h = sum(r.height for r in rows)
    stacked = Image.new("RGBA", (middle.width, h))
    y = 0
    for r in rows:
        stacked.paste(r, (0, y))
        y += r.height
    full = stacked.resize((W, h), Image.NEAREST).resize((W * SCALE, h * SCALE), Image.NEAREST)
    out = Image.new("RGBA", (W * SCALE, COUNTER_HEIGHT_PX), full.getpixel((W * SCALE // 2, full.height - 1)))
    out.paste(full.crop((0, 0, W * SCALE, min(full.height, COUNTER_HEIGHT_PX))), (0, 0))
    out.save(os.path.join(ART, "counter_bar.png"))


def run():
    compose_background()
    compose_counter()
    print("composed bar background and counter from the Styloo pack")


if __name__ == "__main__":
    run()
