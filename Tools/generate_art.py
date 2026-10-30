"""
Pour Decisions - procedural art generator.

Draws every sprite the game uses with Pillow and writes them to
Assets/PourDecisions/Resources/Art. It also writes art_manifest.json, which
holds the real-world glass dimensions (cm) and bottle interior polygons
(Unity units). The game reads that manifest, so the pixels and the pour math
always come from the same numbers.

Run from the repo root:  python3 Tools/generate_art.py
"""
import json
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets/PourDecisions/Resources/Art")
DATA = os.path.join(ROOT, "Assets/PourDecisions/Resources/Data")
FONTS = os.path.join(ROOT, "Assets/PourDecisions/Resources/Fonts")
os.makedirs(ART, exist_ok=True)
os.makedirs(DATA, exist_ok=True)

SS = 3                 # supersampling factor for smooth edges
CM_PER_UNIT = 3.5      # 1 Unity unit = 3.5 real centimeters
PPU = 140              # sprite pixels per Unity unit (40 px per cm)
PX_CM = PPU / CM_PER_UNIT
random.seed(7)


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


# ---------------------------------------------------------------- helpers
class Canvas:
    """RGBA image drawn at SS x resolution, saved downsampled."""

    def __init__(self, w, h, bg=(0, 0, 0, 0)):
        self.w, self.h = int(round(w)), int(round(h))
        self.img = Image.new("RGBA", (self.w * SS, self.h * SS), bg)

    def s(self, pts):
        return [(x * SS, y * SS) for x, y in pts]

    def layer(self):
        return Image.new("RGBA", self.img.size, (0, 0, 0, 0))

    def mask(self):
        return Image.new("L", self.img.size, 0)

    def poly(self, pts, fill=None, outline=None, width=1, target=None):
        d = ImageDraw.Draw(target or self.img)
        if fill is not None:
            d.polygon(self.s(pts), fill=fill)
        if outline is not None:
            p = self.s(pts)
            d.line(p + [p[0]], fill=outline, width=max(1, int(width * SS)), joint="curve")

    def line(self, pts, fill, width=1, target=None):
        ImageDraw.Draw(target or self.img).line(self.s(pts), fill=fill, width=max(1, int(width * SS)), joint="curve")

    def ellipse(self, box, fill=None, outline=None, width=1, target=None):
        x0, y0, x1, y1 = box
        ImageDraw.Draw(target or self.img).ellipse((x0 * SS, y0 * SS, x1 * SS, y1 * SS), fill=fill,
                                                   outline=outline, width=max(1, int(width * SS)))

    def arc(self, box, a0, a1, fill, width=1, target=None):
        x0, y0, x1, y1 = box
        ImageDraw.Draw(target or self.img).arc((x0 * SS, y0 * SS, x1 * SS, y1 * SS), a0, a1, fill=fill,
                                               width=max(1, int(width * SS)))

    def rrect(self, box, r, fill=None, outline=None, width=1, target=None):
        x0, y0, x1, y1 = box
        ImageDraw.Draw(target or self.img).rounded_rectangle((x0 * SS, y0 * SS, x1 * SS, y1 * SS), r * SS,
                                                             fill=fill, outline=outline,
                                                             width=max(1, int(width * SS)))

    def text(self, xy, txt, fnt_name, size, fill, anchor="mm", stroke=0, stroke_fill=None, target=None):
        f = font(fnt_name, int(size * SS))
        ImageDraw.Draw(target or self.img).text((xy[0] * SS, xy[1] * SS), txt, font=f, fill=fill, anchor=anchor,
                                                stroke_width=int(stroke * SS), stroke_fill=stroke_fill)

    def paste(self, layer, mask=None):
        if mask is not None:
            a = layer.getchannel("A")
            from PIL import ImageChops
            layer = layer.copy()
            layer.putalpha(ImageChops.multiply(a, mask))
        self.img.alpha_composite(layer)

    def blur_paste(self, layer, radius):
        self.img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(radius * SS)))

    def save(self, name):
        out = self.img.resize((self.w, self.h), Image.LANCZOS)
        out.save(os.path.join(ART, name + ".png"))
        return out


def vgrad(size, top, bottom):
    """Vertical gradient image (RGBA)."""
    w, h = size
    col = Image.new("RGBA", (1, h))
    for y in range(h):
        t = y / max(1, h - 1)
        col.putpixel((0, y), tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(4)))
    return col.resize((w, h))


def hgrad(size, left, right):
    w, h = size
    row = Image.new("RGBA", (w, 1))
    for x in range(w):
        t = x / max(1, w - 1)
        row.putpixel((x, 0), tuple(int(left[i] + (right[i] - left[i]) * t) for i in range(4)))
    return row.resize((w, h))


def cyl_grad(size, dark, light, hi_pos=0.3):
    """Horizontal gradient that looks like a lit cylinder."""
    w, h = size
    row = Image.new("RGBA", (w, 1))
    for x in range(w):
        t = x / max(1, w - 1)
        k = max(0.0, 1 - abs(t - hi_pos) / max(hi_pos, 1 - hi_pos)) ** 1.5
        row.putpixel((x, 0), tuple(int(dark[i] + (light[i] - dark[i]) * k) for i in range(4)))
    return row.resize((w, h))


def fill_poly_with(cv, pts, tex):
    m = cv.mask()
    ImageDraw.Draw(m).polygon(cv.s(pts), fill=255)
    layer = cv.layer()
    layer.paste(tex.resize(cv.img.size) if tex.size != cv.img.size else tex, (0, 0))
    cv.paste(layer, m)


def radial(size, color, falloff=1.6):
    w, h = size
    img = Image.new("RGBA", (w, h))
    cx, cy = (w - 1) / 2, (h - 1) / 2
    px = img.load()
    for y in range(h):
        for x in range(w):
            d = math.hypot((x - cx) / cx, (y - cy) / cy)
            a = max(0.0, 1 - d) ** falloff
            px[x, y] = (color[0], color[1], color[2], int(color[3] * a))
    return img


# ---------------------------------------------------------------- glasses
GLASS_EDGE = (205, 232, 245)
GLASS_LINE = (120, 165, 190, 235)

# id, display name, interior bottom/top radius (cm), interior height, base thickness, wall, kind
GLASSES = [
    dict(id="shot", name="Shot Glass", rb=1.6, rt=2.1, h=5.2, base=1.5, wall=0.32, kind="tumbler"),
    dict(id="shooter", name="Tall Shooter", rb=1.45, rt=1.7, h=9.0, base=1.2, wall=0.28, kind="tumbler"),
    dict(id="rocks", name="Rocks Glass", rb=3.6, rt=4.1, h=8.5, base=1.4, wall=0.3, kind="tumbler"),
    dict(id="highball", name="Highball", rb=3.0, rt=3.3, h=14.0, base=1.0, wall=0.26, kind="tumbler"),
    dict(id="pint", name="Pint Glass", rb=3.0, rt=4.3, h=14.5, base=0.8, wall=0.26, kind="tumbler"),
    dict(id="martini", name="Martini Glass", rb=0.25, rt=5.5, h=7.0, base=8.5, wall=0.22, kind="martini"),
]


def frustum_volume(rb, rt, H, h):
    r = rb + (rt - rb) * (h / H)
    return math.pi * h / 3 * (rb * rb + rb * r + r * r)


def height_for_volume(rb, rt, H, v):
    lo, hi = 0.0, H
    for _ in range(40):
        mid = (lo + hi) / 2
        if frustum_volume(rb, rt, H, mid) < v:
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2


def draw_glass(g):
    rb, rt, H, base, wall = g["rb"], g["rt"], g["h"], g["base"], g["wall"]
    extra_r = 0.0
    if g["kind"] == "martini":
        extra_r = max(0.0, 3.6 - (rt + wall))
    half_w = (rt + wall + extra_r + 0.35) * PX_CM
    W = 2 * half_w
    pad_b = 0.15 * PX_CM
    Hpx = (base + H + 0.7) * PX_CM + pad_b
    cx = W / 2
    yb = Hpx - pad_b               # counter contact
    yib = yb - base * PX_CM         # interior bottom
    yit = yib - H * PX_CM           # interior top (rim)

    def ix(y, side):              # inner wall x at image y
        t = (yib - y) / (yib - yit)
        r = rb + (rt - rb) * t
        return cx + side * r * PX_CM

    inner = [(ix(yit, -1), yit), (ix(yit, 1), yit), (ix(yib, 1), yib), (ix(yib, -1), yib)]
    wpx = wall * PX_CM
    back = Canvas(W, Hpx)
    front = Canvas(W, Hpx)

    # ---- back: faint interior tint and the far half of the rim
    fill_poly_with(back, inner, vgrad(back.img.size, (215, 238, 250, 46), (190, 222, 240, 70)))
    rx = (rt + wall * 0.5) * PX_CM
    ry = max(3.0, 0.16 * rt * PX_CM)
    back.arc((cx - rx, yit - ry, cx + rx, yit + ry), 180, 360, (235, 248, 255, 200), 2.2)

    # ---- front: walls
    if g["kind"] == "martini":
        stem_w = 0.55 * PX_CM
        foot_r = 3.6 * PX_CM
        bowl_bottom = yib + 0.9 * PX_CM
        outer = [(ix(yit, -1) - wpx, yit), (ix(yit, 1) + wpx, yit), (cx + stem_w / 2, bowl_bottom),
                 (cx - stem_w / 2, bowl_bottom)]
        lw = [(ix(yit, -1) - wpx, yit), (ix(yit, -1), yit), (ix(yib, -1), yib), (cx, yib + 2),
              (cx - stem_w / 2, bowl_bottom)]
        rw = [(ix(yit, 1), yit), (ix(yit, 1) + wpx, yit), (cx + stem_w / 2, bowl_bottom), (cx, yib + 2),
              (ix(yib, 1), yib)]
        for p in (lw, rw):
            front.poly(p, fill=GLASS_EDGE + (120,))
        stem = [(cx - stem_w / 2, bowl_bottom), (cx + stem_w / 2, bowl_bottom), (cx + stem_w / 2, yb - 0.5 * PX_CM),
                (cx - stem_w / 2, yb - 0.5 * PX_CM)]
        fill_poly_with(front, stem, cyl_grad(front.img.size, GLASS_EDGE + (150,), (255, 255, 255, 230)))
        front.ellipse((cx - foot_r, yb - 0.75 * PX_CM, cx + foot_r, yb + 0.05 * PX_CM), fill=GLASS_EDGE + (165,),
                      outline=GLASS_LINE, width=1.6)
        front.poly(outer, outline=GLASS_LINE, width=1.8)
        front.line([(cx - stem_w / 2, bowl_bottom), (cx - stem_w / 2, yb - 0.5 * PX_CM)], GLASS_LINE, 1.5)
        front.line([(cx + stem_w / 2, bowl_bottom), (cx + stem_w / 2, yb - 0.5 * PX_CM)], GLASS_LINE, 1.5)
    else:
        obl = (ix(yib, -1) - wpx, yb)
        obr = (ix(yib, 1) + wpx, yb)
        lw = [(ix(yit, -1) - wpx, yit), (ix(yit, -1), yit), (ix(yib, -1), yib), (ix(yib, -1), yb), obl]
        rw = [(ix(yit, 1), yit), (ix(yit, 1) + wpx, yit), obr, (ix(yib, 1), yb), (ix(yib, 1), yib)]
        for p in (lw, rw):
            front.poly(p, fill=GLASS_EDGE + (125,))
        # heavy glass base with a lens-like gradient
        basep = [(ix(yib, -1), yib), (ix(yib, 1), yib), (ix(yib, 1), yb), (ix(yib, -1), yb)]
        fill_poly_with(front, basep, vgrad(front.img.size, (225, 244, 252, 150), (175, 215, 235, 205)))
        front.line([(ix(yib, -1), yib), (ix(yib, 1), yib)], (255, 255, 255, 170), 1.6)
        # small ellipse that hints at the bottom of the bowl
        bry = max(2.0, 0.14 * rb * PX_CM)
        front.arc((ix(yib, -1), yib - bry, ix(yib, 1), yib + bry), 0, 180, (240, 250, 255, 140), 1.4)
        outer = [(ix(yit, -1) - wpx, yit), (ix(yit, 1) + wpx, yit), obr, obl]
        front.poly(outer, outline=GLASS_LINE, width=1.8)
        front.line([(ix(yit, -1), yit), (ix(yib, -1), yib)], (150, 195, 215, 150), 1.2)
        front.line([(ix(yit, 1), yit), (ix(yib, 1), yib)], (150, 195, 215, 150), 1.2)

    # glossy highlight streaks
    hl = front.layer()
    x0 = ix(yit, -1) + 0.45 * PX_CM
    x1 = ix(yib, -1) + 0.45 * PX_CM if g["kind"] != "martini" else cx - 0.3 * PX_CM
    y_end = yib - 0.4 * PX_CM if g["kind"] != "martini" else yib - 0.2 * PX_CM
    front.line([(x0, yit + 0.5 * PX_CM), (x1, y_end)], (255, 255, 255, 120), 0.32 * PX_CM, target=hl)
    front.line([(x0 + 0.5 * PX_CM, yit + 0.8 * PX_CM), (x1 + 0.4 * PX_CM, yit + (y_end - yit) * 0.45)],
               (255, 255, 255, 90), 0.12 * PX_CM, target=hl)
    xr0 = ix(yit, 1) - 0.35 * PX_CM
    front.line([(xr0, yit + 0.6 * PX_CM), (xr0 - (rt - rb) * PX_CM * 0.8, yit + (y_end - yit) * 0.7)],
               (255, 255, 255, 60), 0.15 * PX_CM, target=hl)
    front.blur_paste(hl, 1.0)

    # near half of the rim
    front.arc((cx - rx, yit - ry, cx + rx, yit + ry), 0, 180, (245, 252, 255, 235), 2.4)
    front.arc((cx - rx, yit - ry, cx + rx, yit + ry), 180, 360, (200, 230, 245, 120), 1.2)

    back.save(f"glass_{g['id']}_back")
    front.save(f"glass_{g['id']}_front")
    cap = frustum_volume(rb, rt, H, H)
    return dict(id=g["id"], name=g["name"], bottomRadiusCm=rb, topRadiusCm=rt,
                heightCm=H, baseCm=base, wallCm=wall, capacityMl=round(cap, 1),
                pivotX=0.5, pivotY=pad_b / Hpx, widthPx=int(round(W)), heightPx=int(round(Hpx)))


# ---------------------------------------------------------------- bottles
BOTTLES = [
    dict(id="whiskey", brand="OLD COPPERLINE", sub="Kentucky Style Whiskey", bodyW=1.30, bodyH=2.0, shoulderH=0.35,
         style="square", neckW=0.36, neckL=0.75, wall=0.06, baseT=0.14, tint=(220, 228, 232), label=(242, 230, 200),
         ink=(90, 45, 20), accent=(170, 90, 35), labelY=(0.25, 0.75), ml=750),
    dict(id="vodka", brand="GLACIER NINE", sub="Triple Filtered Vodka", bodyW=0.95, bodyH=2.5, shoulderH=0.6,
         style="round", neckW=0.30, neckL=0.70, wall=0.05, baseT=0.15, tint=(230, 240, 250), label=(40, 85, 160),
         ink=(255, 255, 255), accent=(150, 205, 245), labelY=(0.30, 0.62), ml=750),
    dict(id="tequila", brand="SOL DORADO", sub="Reposado Tequila", bodyW=1.25, bodyH=1.7, shoulderH=0.55,
         style="straight", neckW=0.34, neckL=0.80, wall=0.06, baseT=0.16, tint=(235, 238, 230), label=(250, 245, 230),
         ink=(205, 90, 30), accent=(240, 170, 40), labelY=(0.2, 0.72), ml=750),
    dict(id="rum", brand="BRASS ANCHOR", sub="Dark Spiced Rum", bodyW=1.15, bodyH=2.1, shoulderH=0.5,
         style="round", neckW=0.34, neckL=0.85, wall=0.06, baseT=0.14, tint=(150, 105, 70), label=(30, 30, 35),
         ink=(225, 185, 95), accent=(190, 140, 60), labelY=(0.28, 0.70), ml=750),
    dict(id="gin", brand="JUNIPER & FINCH", sub="London Dry Gin", bodyW=1.05, bodyH=2.3, shoulderH=0.3,
         style="square", neckW=0.36, neckL=0.62, wall=0.06, baseT=0.15, tint=(140, 200, 165), label=(245, 245, 238),
         ink=(30, 80, 60), accent=(70, 140, 100), labelY=(0.3, 0.66), ml=750),
    dict(id="coffee", brand="MIDNIGHT ROAST", sub="Coffee Liqueur", bodyW=1.10, bodyH=1.55, shoulderH=0.55,
         style="round", neckW=0.32, neckL=0.75, wall=0.06, baseT=0.14, tint=(170, 120, 70), label=(250, 205, 70),
         ink=(55, 30, 20), accent=(200, 60, 40), labelY=(0.18, 0.62), ml=700),
]


def shoulder_curve(style, a, b, H, n=10):
    """Points along the shoulder from body (y=-H) to neck (y=0), right side.
    a = half neck width, b = half body width. Returned as (x, y) in units, y up."""
    pts = []
    for i in range(n + 1):
        s = i / n
        if style == "straight":
            x = b + (a - b) * s
        elif style == "square":
            x = a + (b - a) * (max(0.0, 1 - s ** 4)) ** 0.25
        else:
            x = a + (b - a) * math.sqrt(max(0.0, 1 - s * s))
        pts.append((x, -H + H * s))
    return pts


def bottle_shapes(b, inset):
    """Convex pieces (body, shoulder, neck) of the bottle in local units, y up, origin at the neck base."""
    bw = b["bodyW"] / 2 - inset
    nw = b["neckW"] / 2 - inset
    sh, bh = b["shoulderH"], b["bodyH"]
    base = b["baseT"] if inset > 0 else 0.0
    neck_top = b["neckL"] - (0.04 if inset > 0 else 0.0)
    body = [(-bw, -sh - bh + base), (bw, -sh - bh + base), (bw, -sh), (-bw, -sh)]
    right = shoulder_curve(b["style"], nw, bw, sh)
    shoulder = [(x, y) for x, y in right] + [(-x, y) for x, y in reversed(right)]
    neck = [(-nw, 0.0), (nw, 0.0), (nw, neck_top), (-nw, neck_top)]
    return body, shoulder, neck


def poly_area(p):
    a = 0.0
    for i in range(len(p)):
        x0, y0 = p[i]
        x1, y1 = p[(i + 1) % len(p)]
        a += x0 * y1 - x1 * y0
    return abs(a) / 2


def draw_bottle(b):
    pad = 0.12
    half_w = b["bodyW"] / 2 + pad
    top = b["neckL"] + 0.08 + pad
    bottom = -(b["shoulderH"] + b["bodyH"]) - pad
    W = 2 * half_w * PPU
    Hpx = (top - bottom) * PPU

    def P(x, y):  # unit coords (y up) -> image px
        return ((x + half_w) * PPU, (top - y) * PPU)

    outer = bottle_shapes(b, 0.0)
    inner = bottle_shapes(b, b["wall"])
    back = Canvas(W, Hpx)
    front = Canvas(W, Hpx)
    tint = b["tint"]
    galpha = 70

    # ---- back: tinted interior
    for piece in inner:
        fill_poly_with(back, [P(*q) for q in piece],
                       hgrad(back.img.size, tint + (galpha + 30,), tint + (galpha - 10,)))

    # ---- front: full silhouette as glass, then knock the interior back to a thin tint
    sil = front.mask()
    for piece in outer:
        ImageDraw.Draw(sil).polygon(front.s([P(*q) for q in piece]), fill=255)
    hole = front.mask()
    for piece in inner:
        ImageDraw.Draw(hole).polygon(front.s([P(*q) for q in piece]), fill=255)
    from PIL import ImageChops
    walls = ImageChops.subtract(sil, hole)
    wall_layer = Image.new("RGBA", front.img.size, tuple(int(c * 0.8) for c in tint) + (215,))
    front.paste(wall_layer, walls)
    tint_layer = Image.new("RGBA", front.img.size, tint + (28,))
    front.paste(tint_layer, hole)
    # thick glass bottom
    bw2 = b["bodyW"] / 2
    yb = -(b["shoulderH"] + b["bodyH"])
    fill_poly_with(front, [P(-bw2, yb + b["baseT"]), P(bw2, yb + b["baseT"]), P(bw2, yb), P(-bw2, yb)],
                   vgrad(front.img.size, tint + (120,), tuple(int(c * 0.7) for c in tint) + (230,)))

    # label
    ly0 = yb + b["bodyH"] * b["labelY"][0]
    ly1 = yb + b["bodyH"] * b["labelY"][1]
    lab = [P(-bw2 + 0.015, ly1), P(bw2 - 0.015, ly1), P(bw2 - 0.015, ly0), P(-bw2 + 0.015, ly0)]
    (x0, y0), (x1, y1) = lab[0], lab[2]
    front.rrect((x0, y0, x1, y1), 0.04 * PPU, fill=b["label"] + (255,))
    lw, lh = x1 - x0, y1 - y0
    front.rrect((x0 + lw * 0.05, y0 + lh * 0.06, x1 - lw * 0.05, y1 - lh * 0.06), 0.03 * PPU,
                outline=b["accent"] + (255,), width=2.2)
    words = b["brand"].split(" ")
    lines = words[:2] if len(words) <= 2 else [" ".join(words[:-1]), words[-1]]
    # Measure the text so the widest line fits inside the label border.
    size = lh * 0.26
    probe = font("LilitaOne-Regular.ttf", 100)
    widest = max(probe.getlength(l) for l in lines) / 100.0
    size = min(size, lw * 0.82 / widest)
    yy = y0 + lh * (0.32 if len(lines) > 1 else 0.42)
    for i, line in enumerate(lines):
        front.text(((x0 + x1) / 2, yy + i * size * 1.02), line, "LilitaOne-Regular.ttf", size, b["ink"] + (255,))
    sub_probe = font("Nunito-Bold.ttf", 100).getlength(b["sub"]) / 100.0
    front.text(((x0 + x1) / 2, y1 - lh * 0.17), b["sub"], "Nunito-Bold.ttf",
               min(lh * 0.1, lw * 0.85 / sub_probe), b["ink"] + (230,))
    # cork/lip ring on the neck
    nw2 = b["neckW"] / 2
    front.rrect((P(-nw2 - 0.035, b["neckL"] + 0.04)[0], P(0, b["neckL"] + 0.04)[1],
                 P(nw2 + 0.035, 0)[0], P(0, b["neckL"] - 0.1)[1]), 0.03 * PPU,
                fill=tuple(int(c * 0.85) for c in tint) + (240,), outline=(90, 90, 90, 200), width=1.2)
    # silhouette outline
    edge = sil.filter(ImageFilter.FIND_EDGES)
    edge_layer = Image.new("RGBA", front.img.size, (40, 40, 40, 255))
    front.paste(edge_layer, edge.point(lambda v: 255 if v > 40 else 0).filter(ImageFilter.MaxFilter(3)))
    # highlights
    hl = front.layer()
    front.line([P(-bw2 + 0.16, yb + 0.25), P(-bw2 + 0.16, -b["shoulderH"] - 0.05)], (255, 255, 255, 110),
               0.07 * PPU, target=hl)
    front.line([P(-bw2 + 0.3, yb + 0.35), P(-bw2 + 0.3, yb + b["bodyH"] * 0.55)], (255, 255, 255, 60),
               0.03 * PPU, target=hl)
    front.line([P(-nw2 + 0.07, 0.05), P(-nw2 + 0.07, b["neckL"] - 0.15)], (255, 255, 255, 120),
               0.035 * PPU, target=hl)
    front.blur_paste(hl, 1.5)

    back.save(f"bottle_{b['id']}_back")
    front.save(f"bottle_{b['id']}_front")
    pieces = [[dict(x=round(x, 4), y=round(y, 4)) for x, y in piece] for piece in inner]
    return dict(id=b["id"], brand=b["brand"].title(), capacityMl=b["ml"],
                interior=[dict(points=p) for p in pieces], neckTopY=b["neckL"],
                bodyBottomY=yb, bodyHalfWidth=bw2,
                pivotX=half_w / (2 * half_w), pivotY=(-bottom) / (top - bottom),
                widthPx=int(round(W)), heightPx=int(round(Hpx)))


# ---------------------------------------------------------------- spouts
def draw_spouts():
    out = {}
    specs = [("speed", 0.46, (170, 175, 182), (235, 240, 245), None, 0.14, 0.075),
             ("slow", 0.42, (170, 175, 182), (235, 240, 245), (210, 40, 40), 0.14, 0.07),
             ("fast", 0.30, (160, 165, 170), (230, 235, 240), None, 0.22, 0.20)]
    for sid, L, dark, light, tip, w0, w1 in specs:
        W = 0.5 * PPU
        Hpx = (L + 0.06) * PPU
        cv = Canvas(W, Hpx)
        cx = W / 2
        ybase = Hpx - 0.02 * PPU
        cork_h = 0.12 * PPU
        # rubber/plastic collar that hugs the neck
        collar = (40, 40, 45)
        cv.rrect((cx - 0.21 * PPU, ybase - cork_h, cx + 0.21 * PPU, ybase), 0.03 * PPU, fill=collar + (255,),
                 outline=(20, 20, 20, 255), width=1.2)
        tube = [(cx - w0 / 2 * PPU, ybase - cork_h), (cx + w0 / 2 * PPU, ybase - cork_h),
                (cx + w1 / 2 * PPU, ybase - L * PPU), (cx - w1 / 2 * PPU, ybase - L * PPU)]
        fill_poly_with(cv, tube, cyl_grad(cv.img.size, dark + (255,), light + (255,), 0.45))
        cv.poly(tube, outline=(70, 70, 75, 255), width=1.2)
        if tip:
            tp = [(cx - w1 / 2 * PPU - 2, ybase - L * PPU + 0.1 * PPU), (cx + w1 / 2 * PPU + 2, ybase - L * PPU + 0.1 * PPU),
                  (cx + w1 / 2 * PPU + 1, ybase - L * PPU), (cx - w1 / 2 * PPU - 1, ybase - L * PPU)]
            cv.poly(tp, fill=tip + (255,), outline=(40, 20, 20, 255), width=1)
        cv.save(f"spout_{sid}")
        out[sid] = dict(id=sid, length=L, pivotY=(0.02 * PPU) / Hpx, widthPx=int(W), heightPx=int(round(Hpx)))
    return out


# ---------------------------------------------------------------- environments
BG_W, BG_H = 1920, 1080   # rendered at 100 PPU so it fills a 10.8 unit tall view
COUNTER_LINE = 795          # pixel row where the counter's back edge sits


def bottle_silhouette(cv, x, ybot, w, h, color, target=None):
    neck_w = w * random.uniform(0.25, 0.35)
    shoulder = h * random.uniform(0.12, 0.25)
    neck = h * random.uniform(0.18, 0.3)
    body_top = ybot - (h - shoulder - neck)
    pts = [(x - w / 2, ybot), (x + w / 2, ybot), (x + w / 2, body_top), (x + neck_w / 2, body_top - shoulder),
           (x + neck_w / 2, ybot - h), (x - neck_w / 2, ybot - h), (x - neck_w / 2, body_top - shoulder),
           (x - w / 2, body_top)]
    cv.poly(pts, fill=color, target=target)
    cv.line([(x - w / 2 + w * 0.2, ybot - 6), (x - w / 2 + w * 0.2, body_top + 4)], (255, 255, 255, 70), w * 0.08,
            target=target)
    if random.random() < 0.7:
        ly = ybot - (ybot - body_top) * random.uniform(0.3, 0.55)
        lc = random.choice([(240, 230, 200, 230), (30, 30, 30, 230), (200, 60, 50, 230), (250, 250, 250, 230)])
        cv.poly([(x - w / 2 + 2, ly), (x + w / 2 - 2, ly), (x + w / 2 - 2, ly - h * 0.18),
                 (x - w / 2 + 2, ly - h * 0.18)], fill=lc, target=target)


def draw_bar_bg():
    cv = Canvas(BG_W, BG_H)
    cv.img.paste(vgrad(cv.img.size, (34, 20, 18, 255), (72, 42, 30, 255)), (0, 0))
    # brick wall texture
    brick = cv.layer()
    for row in range(0, 30):
        y = row * 34
        off = 0 if row % 2 == 0 else 50
        for col in range(-1, 21):
            x = col * 100 + off
            c = random.randint(-10, 10)
            cv.rrect((x + 3, y + 3, x + 97, y + 31), 4, fill=(90 + c, 48 + c // 2, 36 + c // 2, 70), target=brick)
    cv.paste(brick)
    # mirror behind the shelves
    cv.rrect((360, 230, 1560, 790), 18, fill=(30, 36, 40, 255), outline=(150, 110, 60, 255), width=10)
    refl = cv.layer()
    cv.poly([(560, 240), (760, 240), (520, 780), (320, 780)], fill=(255, 255, 255, 18), target=refl)
    cv.poly([(900, 240), (960, 240), (720, 780), (660, 780)], fill=(255, 255, 255, 12), target=refl)
    cv.paste(refl)
    # glowing shelves with bottle silhouettes
    for shelf_y in (480, 735):
        glow = cv.layer()
        cv.rrect((380, shelf_y - 180, 1540, shelf_y), 10, fill=(255, 170, 70, 70), target=glow)
        cv.blur_paste(glow, 40)
        bottles = cv.layer()
        x = 410
        while x < 1510:
            w = random.uniform(38, 70)
            h = random.uniform(120, 190)
            col = random.choice([(150, 90, 30), (60, 120, 70), (200, 200, 210), (120, 40, 30), (60, 90, 150),
                                 (180, 140, 60), (90, 50, 80)])
            bottle_silhouette(cv, x + w / 2, shelf_y - 2, w, h, col + (215,), target=bottles)
            x += w + random.uniform(10, 26)
        cv.paste(bottles)
        cv.rrect((360, shelf_y, 1560, shelf_y + 22), 4, fill=(110, 70, 40, 255), outline=(60, 35, 20, 255), width=2)
        cv.line([(365, shelf_y + 3), (1555, shelf_y + 3)], (255, 200, 120, 160), 3)
    # side wooden pillars
    for x0 in (300, 1560):
        fill_poly_with(cv, [(x0, 150), (x0 + 60, 150), (x0 + 60, 800), (x0, 800)],
                       hgrad(cv.img.size, (70, 40, 25, 255), (120, 75, 45, 255)))
    # pendant lamps
    for lx in (520, 960, 1400):
        cv.line([(lx, 0), (lx, 110)], (20, 20, 20, 255), 3)
        cone = cv.layer()
        cv.poly([(lx - 30, 150), (lx + 30, 150), (lx + 260, 900), (lx - 260, 900)], fill=(255, 210, 140, 30),
                target=cone)
        cv.blur_paste(cone, 30)
        glow = cv.layer()
        cv.ellipse((lx - 140, 50, lx + 140, 250), fill=(255, 200, 120, 120), target=glow)
        cv.blur_paste(glow, 35)
        cv.poly([(lx - 18, 108), (lx + 18, 108), (lx + 60, 160), (lx - 60, 160)], fill=(30, 30, 32, 255),
                outline=(120, 90, 50, 255), width=2)
        cv.ellipse((lx - 22, 150, lx + 22, 172), fill=(255, 235, 190, 255))
    # neon sign
    neon = cv.layer()
    # neon sign on the left-hand brick wall, clear of the title logo, the HUD and the customer
    cv.text((150, 580), "OPEN", "LilitaOne-Regular.ttf", 58, (255, 90, 150, 255), target=neon)
    cv.text((150, 640), "LATE", "LilitaOne-Regular.ttf", 58, (255, 90, 150, 255), target=neon)
    cv.blur_paste(neon, 10)
    cv.text((150, 580), "OPEN", "LilitaOne-Regular.ttf", 58, (255, 200, 225, 255))
    cv.text((150, 640), "LATE", "LilitaOne-Regular.ttf", 58, (255, 200, 225, 255))
    out = cv.img.filter(ImageFilter.GaussianBlur(1.2 * SS))
    cv.img = out
    cv.save("bg_bar")


def draw_counters():
    H = 1080 - COUNTER_LINE
    # bar: polished mahogany with a brass foot rail
    cv = Canvas(1920, H)
    fill_poly_with(cv, [(0, 0), (1920, 0), (1920, 50), (0, 50)], vgrad(cv.img.size, (70, 32, 20, 255),
                                                                      (120, 60, 34, 255)))
    sheen = cv.layer()
    cv.poly([(0, 14), (1920, 10), (1920, 22), (0, 26)], fill=(255, 210, 160, 50), target=sheen)
    cv.blur_paste(sheen, 4)
    fill_poly_with(cv, [(0, 50), (1920, 50), (1920, 68), (0, 68)], vgrad(cv.img.size, (190, 120, 70, 255),
                                                                        (90, 45, 25, 255)))
    fill_poly_with(cv, [(0, 68), (1920, 68), (1920, H), (0, H)], vgrad(cv.img.size, (60, 30, 20, 255),
                                                                      (30, 15, 10, 255)))
    for x in range(0, 1920, 240):
        cv.rrect((x + 18, 88, x + 222, H - 20), 8, outline=(85, 45, 28, 255), width=4)
    fill_poly_with(cv, [(0, 175), (1920, 175), (1920, 195), (0, 195)],
                   vgrad(cv.img.size, (255, 230, 150, 255), (150, 105, 40, 255)))
    cv.save("counter_bar")


# ---------------------------------------------------------------- customers
SKINS = [(241, 194, 160), (198, 134, 66), (141, 85, 36), (255, 219, 172), (224, 172, 105), (110, 68, 40)]
SHIRTS = [(40, 150, 150), (230, 180, 50), (140, 40, 50), (170, 140, 210), (240, 120, 100), (40, 55, 95)]
HAIR = [("short", (90, 55, 30)), ("afro", (25, 20, 20)), ("bald", (30, 20, 15)), ("long", (235, 200, 110)),
        ("bun", (20, 20, 25)), ("gray", (170, 170, 175))]


def draw_customer(i, mood):
    W, H = 300, 360
    cv = Canvas(W, H)
    skin, shirt = SKINS[i], SHIRTS[i]
    style, hair = HAIR[i]
    dark_skin = tuple(int(c * 0.82) for c in skin)
    # long hair goes behind the head
    if style == "long":
        cv.rrect((62, 70, 238, 290), 70, fill=hair + (255,))
    # shoulders / shirt
    cv.rrect((30, 255, 270, 420), 70, fill=shirt + (255,))
    cv.poly([(118, 255), (182, 255), (150, 300)], fill=dark_skin + (255,))
    if i in (2, 5):  # collars
        cv.poly([(110, 258), (150, 300), (128, 312), (96, 268)], fill=(245, 245, 245, 255))
        cv.poly([(190, 258), (150, 300), (172, 312), (204, 268)], fill=(245, 245, 245, 255))
    cv.rrect((128, 215, 172, 270), 14, fill=dark_skin + (255,))
    if style == "afro":
        for k in range(16):
            a = k / 16 * math.tau
            x, y = 150 + math.cos(a) * 82, 110 + math.sin(a) * 70
            cv.ellipse((x - 42, y - 42, x + 42, y + 42), fill=hair + (255,))
    if style == "bun":
        cv.ellipse((120, 10, 180, 66), fill=hair + (255,))
    # ears + head
    cv.ellipse((64, 130, 96, 175), fill=dark_skin + (255,))
    cv.ellipse((204, 130, 236, 175), fill=dark_skin + (255,))
    cv.ellipse((75, 55, 225, 235), fill=skin + (255,))
    # hair on top
    if style in ("short", "gray"):
        cv.poly([(76, 135), (80, 85), (120, 50), (180, 48), (220, 85), (224, 135), (210, 100), (150, 82),
                 (95, 100)], fill=hair + (255,))
    elif style == "long":
        cv.poly([(76, 150), (82, 85), (150, 48), (218, 85), (224, 150), (205, 95), (150, 78), (100, 92)],
                fill=hair + (255,))
    elif style == "bun":
        cv.poly([(78, 125), (90, 75), (150, 52), (210, 75), (222, 125), (200, 92), (150, 78), (100, 92)],
                fill=hair + (255,))
    elif style == "afro":
        cv.poly([(80, 120), (95, 70), (150, 55), (205, 70), (220, 120), (200, 90), (150, 80), (100, 90)],
                fill=hair + (255,))
    if style == "bald":
        cv.poly([(95, 175), (205, 175), (200, 215), (150, 240), (100, 215)], fill=hair + (255,))
        cv.ellipse((110, 60, 160, 80), fill=(255, 255, 255, 50))
    if style == "gray":
        cv.poly([(118, 192), (182, 192), (190, 206), (150, 200), (110, 206)], fill=hair + (255,))
    # eyes, brows and mouth by mood
    ink = (40, 30, 30, 255)
    ey = 140
    for ex in (120, 180):
        if mood == "happy":
            cv.arc((ex - 13, ey - 6, ex + 13, ey + 14), 200, 340, ink, 4)
        else:
            cv.ellipse((ex - 10, ey - 10, ex + 10, ey + 10), fill=(255, 255, 255, 255))
            cv.ellipse((ex - 6, ey - 5, ex + 6, ey + 7), fill=ink)
        if mood == "upset":
            dx = 1 if ex < 150 else -1
            cv.line([(ex - 16 * dx, ey - 26), (ex + 14 * dx, ey - 17)], ink, 5)
        else:
            lift = 4 if mood == "happy" else 0
            cv.arc((ex - 16, ey - 34 - lift, ex + 16, ey - 14 - lift), 200, 340, ink, 4)
    if style == "bun":  # glasses
        for ex in (120, 180):
            cv.ellipse((ex - 22, ey - 18, ex + 22, ey + 18), outline=(60, 60, 70, 255), width=4)
        cv.line([(142, ey - 4), (158, ey - 4)], (60, 60, 70, 255), 4)
    cv.arc((140, 150, 160, 178), 20, 160, dark_skin + (255,), 4)
    blush = cv.layer()
    for bx in (100, 200):
        cv.ellipse((bx - 18, 165, bx + 18, 185), fill=(240, 120, 120, 90 if mood == "happy" else 40), target=blush)
    cv.blur_paste(blush, 3)
    if mood == "happy":
        cv.poly([(122, 188), (178, 188), (165, 212), (150, 216), (135, 212)], fill=(120, 40, 40, 255))
        cv.poly([(126, 189), (174, 189), (170, 196), (130, 196)], fill=(255, 255, 255, 255))
    elif mood == "neutral":
        cv.line([(132, 200), (168, 200)], ink, 5)
    else:
        cv.arc((128, 196, 172, 226), 200, 340, ink, 5)
        cv.ellipse((212, 110, 228, 134), fill=(120, 190, 255, 220))
    cv.save(f"customer_{i}_{mood}")


# ---------------------------------------------------------------- UI and fx
def draw_ui():
    cv = Canvas(96, 96)
    cv.rrect((2, 2, 94, 94), 24, fill=(255, 255, 255, 255))
    cv.save("ui_panel")
    cv = Canvas(96, 96)
    cv.rrect((2, 8, 94, 94), 24, fill=(170, 170, 170, 255))
    cv.rrect((2, 2, 94, 86), 24, fill=(255, 255, 255, 255))
    cv.rrect((10, 8, 86, 30), 12, fill=(255, 255, 255, 120))
    cv.save("ui_button")
    # order ticket
    cv = Canvas(400, 520)
    pts = [(10, 30), (390, 30), (390, 490)]
    zig = []
    for k in range(20, -1, -1):
        zig.append((10 + k * 19, 500 if k % 2 == 0 else 488))
    cv.poly([(10, 30), (390, 30), (390, 500)] + zig + [(10, 500)], fill=(252, 248, 235, 255))
    cv.poly([(10, 30), (390, 30), (390, 70), (10, 70)], fill=(200, 60, 50, 255))
    for y in range(110, 470, 40):
        cv.line([(30, y), (370, y)], (215, 205, 185, 255), 1.5)
    cv.ellipse((185, 12, 215, 42), fill=(70, 70, 75, 255))
    cv.ellipse((191, 16, 203, 28), fill=(170, 170, 175, 255))
    cv.save("ui_ticket")
    # speech bubble
    cv = Canvas(520, 260)
    cv.rrect((10, 10, 510, 200), 50, fill=(255, 255, 255, 255), outline=(40, 40, 45, 255), width=5)
    cv.poly([(380, 196), (440, 196), (470, 252)], fill=(255, 255, 255, 255))
    cv.line([(380, 199), (470, 252), (440, 199)], (40, 40, 45, 255), 5)
    cv.save("ui_bubble")
    # tip jar
    W, H = 220, 280
    cv = Canvas(W, H)
    cv.rrect((20, 40, 200, 270), 30, fill=(210, 235, 245, 80), outline=(140, 180, 200, 255), width=4)
    cv.save("tipjar_back")
    cv = Canvas(W, H)
    cv.rrect((20, 40, 200, 270), 30, outline=(140, 180, 200, 255), width=5)
    cv.rrect((30, 22, 190, 50), 10, fill=(190, 225, 240, 200), outline=(140, 180, 200, 255), width=4)
    hl = cv.layer()
    cv.line([(48, 70), (48, 240)], (255, 255, 255, 170), 10, target=hl)
    cv.blur_paste(hl, 2)
    cv.rrect((55, 130, 165, 185), 8, fill=(250, 240, 210, 255), outline=(150, 110, 60, 255), width=3)
    cv.text((110, 158), "TIPS", "LilitaOne-Regular.ttf", 38, (150, 60, 40, 255))
    cv.save("tipjar_front")
    cv = Canvas(48, 48)
    cv.ellipse((3, 3, 45, 45), fill=(240, 190, 50, 255), outline=(170, 120, 20, 255), width=3)
    cv.ellipse((10, 10, 38, 38), outline=(255, 230, 120, 255), width=2)
    cv.text((24, 25), "$", "Nunito-Black.ttf", 24, (170, 120, 20, 255))
    cv.save("coin")
    # stars
    for name, fill, out in (("star_full", (255, 200, 40, 255), (190, 120, 10, 255)),
                            ("star_empty", (90, 80, 90, 160), (60, 50, 60, 200))):
        cv = Canvas(128, 128)
        pts = []
        for k in range(10):
            a = -math.pi / 2 + k * math.pi / 5
            r = 58 if k % 2 == 0 else 26
            pts.append((64 + math.cos(a) * r, 68 + math.sin(a) * r))
        cv.poly(pts, fill=fill, outline=out, width=5)
        cv.save(name)
    # clock icon
    cv = Canvas(64, 64)
    cv.ellipse((4, 4, 60, 60), fill=(255, 255, 255, 255), outline=(40, 40, 45, 255), width=5)
    cv.line([(32, 32), (32, 14)], (40, 40, 45, 255), 5)
    cv.line([(32, 32), (46, 38)], (40, 40, 45, 255), 5)
    cv.save("icon_clock")
    # flame (streak)
    cv = Canvas(64, 64)
    cv.poly([(32, 4), (52, 30), (54, 46), (44, 60), (20, 60), (10, 46), (14, 28), (22, 36)], fill=(245, 120, 40, 255))
    cv.poly([(32, 26), (42, 42), (40, 54), (24, 54), (22, 44)], fill=(255, 215, 80, 255))
    cv.save("icon_flame")
    # logo
    cv = Canvas(1300, 420)
    shadow = cv.layer()
    cv.text((654, 166), "Pour", "LilitaOne-Regular.ttf", 190, (0, 0, 0, 160), target=shadow)
    cv.text((654, 326), "Decisions", "LilitaOne-Regular.ttf", 170, (0, 0, 0, 160), target=shadow)
    cv.blur_paste(shadow, 6)
    cv.text((650, 160), "Pour", "LilitaOne-Regular.ttf", 190, (255, 205, 90, 255), stroke=10,
            stroke_fill=(70, 30, 15, 255))
    cv.text((650, 320), "Decisions", "LilitaOne-Regular.ttf", 170, (255, 245, 225, 255), stroke=10,
            stroke_fill=(70, 30, 15, 255))
    # little bottle pouring into the "o"
    cv.save("logo")


def draw_fx():
    radial((256, 256), (255, 255, 255, 255), 1.8).save(os.path.join(ART, "fx_glow.png"))
    radial((32, 32), (255, 255, 255, 255), 0.6).save(os.path.join(ART, "fx_droplet.png"))
    pud = radial((128, 128), (255, 255, 255, 230), 0.5).resize((128, 40))
    pud.save(os.path.join(ART, "fx_puddle.png"))
    cv = Canvas(64, 64)
    cv.poly([(32, 0), (37, 27), (64, 32), (37, 37), (32, 64), (27, 37), (0, 32), (27, 27)],
            fill=(255, 255, 255, 255))
    cv.save("fx_sparkle")
    # vignette: transparent centre, dark edges
    v = Image.new("RGBA", (512, 288))
    px = v.load()
    for y in range(288):
        for x in range(512):
            d = math.hypot((x - 255.5) / 255.5, (y - 143.5) / 143.5)
            a = min(1.0, max(0.0, (d - 0.25) / 0.95)) ** 1.4
            px[x, y] = (5, 3, 10, int(255 * a))
    v.save(os.path.join(ART, "fx_vignette.png"))
    # white pixel for meshes and solid UI
    Image.new("RGBA", (4, 4), (255, 255, 255, 255)).save(os.path.join(ART, "fx_white.png"))


def contact_sheet(names, path, cols=6, cell=260, bg=(60, 60, 70, 255)):
    rows = (len(names) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cell, rows * cell), bg)
    for i, n in enumerate(names):
        im = Image.open(os.path.join(ART, n + ".png"))
        im.thumbnail((cell - 10, cell - 10))
        sheet.alpha_composite(im, ((i % cols) * cell + (cell - im.width) // 2, (i // cols) * cell + (cell - im.height) // 2))
    sheet.save(path)


if __name__ == "__main__":
    manifest = dict(cmPerUnit=CM_PER_UNIT, pixelsPerUnit=PPU, glasses=[], bottles=[], spouts=[])
    for g in GLASSES:
        manifest["glasses"].append(draw_glass(g))
    for b in BOTTLES:
        manifest["bottles"].append(draw_bottle(b))
    manifest["spouts"] = list(draw_spouts().values())
    draw_bar_bg()
    draw_counters()
    for i in range(6):
        for mood in ("happy", "neutral", "upset"):
            draw_customer(i, mood)
    draw_ui()
    draw_fx()
    with open(os.path.join(DATA, "art_manifest.json"), "w") as f:
        json.dump(manifest, f, indent=1)
    print("glasses:", [(g["id"], g["capacityMl"]) for g in manifest["glasses"]])
    # Retro look: crunch everything down to chunky pixels (see pixelate_art.py).
    import pixelate_art
    pixelate_art.run()
    # Bar background and counter come from a CC0 pack (already pixel art, so composed after the pixel pass).
    import compose_stock_art
    compose_stock_art.run()
    print("bottles:", len(manifest["bottles"]))
