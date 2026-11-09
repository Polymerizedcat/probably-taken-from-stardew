"""Generates the portal ledger panel (pure starry sky) and its chrome icons."""
import math
import os
import random

from PIL import Image, ImageDraw

PS = r"D:\EntertainmentsPro\SteamLibrary\steamapps\common\Probably Stolen\Mods\StardewPortal\Textures\Paper"

W, H = 620, 360
RADIUS = 90

SKY_IN = (238, 232, 250)      # centre: light "paper" so the text stays readable
SKY_OUT = (58, 44, 96)        # rim: deep space

FILL = (231, 224, 248, 255)
EDGE = (94, 78, 128, 255)

STONE = (124, 112, 152, 255)
STONE_LIGHT = (150, 138, 178, 255)
STONE_DARK = (98, 88, 124, 255)
EDGE_IN = (176, 163, 214, 255)
SPARK = (240, 234, 255, 255)
VORTEX_OUT = (108, 78, 158, 255)
VORTEX_MID = (140, 106, 200, 255)
VORTEX_IN = (176, 148, 226, 255)
VORTEX_CORE = (214, 198, 245, 255)


def panel():
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = img.load()
    cx, cy = W / 2.0, H / 2.0
    inner_w = W / 2.0 - RADIUS
    inner_h = H / 2.0 - RADIUS

    def sdf(x, y):
        qx = abs(x - cx) - inner_w
        qy = abs(y - cy) - inner_h
        ax, ay = max(qx, 0.0), max(qy, 0.0)
        return math.hypot(ax, ay) + min(max(qx, qy), 0.0) - RADIUS

    # base sky gradient, masked to the rounded panel
    for y in range(H):
        for x in range(W):
            if sdf(x + 0.5, y + 0.5) > 0.5:
                continue
            t = math.hypot((x - cx) / (W * 0.5), (y - cy) / (H * 0.5))
            k = (t - 0.55) / 0.45
            k = 0.0 if k < 0 else (1.0 if k > 1 else k * k * (3 - 2 * k))
            r = int(SKY_IN[0] + (SKY_OUT[0] - SKY_IN[0]) * k)
            g = int(SKY_IN[1] + (SKY_OUT[1] - SKY_IN[1]) * k)
            b = int(SKY_IN[2] + (SKY_OUT[2] - SKY_IN[2]) * k)
            px[x, y] = (r, g, b, 255)

    # AI star texture (quantised to keep the pixel-art look), faded towards the centre
    ai_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "stars_ai.png")
    if os.path.exists(ai_path):
        src = Image.open(ai_path).convert("RGBA")
        # crop away the left edge artifact and the cloudy bottom
        src = src.crop((int(src.width * 0.14), 0, int(src.width * 0.92), int(src.height * 0.55)))
        src = src.resize((W, H), Image.NEAREST)
        src = src.quantize(colors=28, method=Image.FASTOCTREE).convert("RGBA")
        sp = src.load()

        layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        lp = layer.load()
        for y in range(H):
            if sdf(0, y) > 0.5 and sdf(W - 1, y) > 0.5 and sdf(cx, y) > 0.5:
                continue
            for x in range(W):
                if sdf(x + 0.5, y + 0.5) > 0.5:
                    continue
                r, g, b, _ = sp[x, y]
                t = math.hypot((x - cx) / (W * 0.5), (y - cy) / (H * 0.5))
                k = (t - 0.30) / 0.70
                k = 0.0 if k < 0 else (1.0 if k > 1 else k)
                alpha = int(255 * (0.06 + 0.80 * k))
                # fade the AI texture near the top-left corner (it has a diagonal artifact)
                corner = math.hypot(x - 8, y - 8) / 150.0
                if corner < 1.0:
                    alpha = int(alpha * corner)
                if alpha > 0:
                    lp[x, y] = (r, g, b, alpha)
        img.alpha_composite(layer)

    # crisp stars on top
    stars = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sdraw = ImageDraw.Draw(stars)
    rng = random.Random(20261108)
    for _ in range(150):
        x, y = rng.randrange(14, W - 14), rng.randrange(14, H - 14)
        if sdf(x, y) > -6:
            continue
        t = math.hypot((x - cx) / (W * 0.5), (y - cy) / (H * 0.5))
        if t < 0.42 and rng.random() < 0.75:
            continue
        colour = (255, 255, 255, rng.randint(120, 225)) if rng.random() < 0.6 else (226, 214, 255, rng.randint(100, 190))
        size = rng.choice((1, 1, 2))
        sdraw.rectangle([x, y, x + size - 1, y + size - 1], fill=colour)
    img.alpha_composite(stars)

    # hard rounded mask: no ring, no border, nothing outside the panel
    mask = Image.new("L", (W, H), 0)
    mdraw = ImageDraw.Draw(mask)
    mdraw.rounded_rectangle([0, 0, W - 1, H - 1], radius=RADIUS, fill=255)
    img.putalpha(mask)

    img.save(os.path.join(PS, "panel_portal.png"))
    return img


def icon(name, painter):
    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    painter(draw)
    img.save(os.path.join(PS, name + ".png"))


def close_icon(d):
    d.ellipse([2, 2, 29, 29], fill=FILL, outline=EDGE, width=2)
    d.line([(11, 11), (20, 20)], fill=EDGE, width=3)
    d.line([(20, 11), (11, 20)], fill=EDGE, width=3)


def zoom_icon(d):
    d.ellipse([2, 2, 29, 29], fill=FILL, outline=EDGE, width=2)
    d.line([(9, 13), (9, 9), (13, 9)], fill=EDGE, width=2)
    d.line([(23, 13), (23, 9), (19, 9)], fill=EDGE, width=2)
    d.line([(9, 19), (9, 23), (13, 23)], fill=EDGE, width=2)
    d.line([(23, 19), (23, 23), (19, 23)], fill=EDGE, width=2)
    d.rectangle([15, 15, 16, 16], fill=EDGE)


def flip_icon(d, direction):
    d.ellipse([2, 2, 29, 29], fill=FILL, outline=EDGE, width=2)
    if direction > 0:
        d.line([(13, 10), (19, 16), (13, 22)], fill=EDGE, width=3)
    else:
        d.line([(19, 10), (13, 16), (19, 22)], fill=EDGE, width=3)


if __name__ == "__main__":
    os.makedirs(PS, exist_ok=True)
    panel()
    icon("ui_close", close_icon)
    icon("ui_zoom", zoom_icon)
    icon("ui_flip_left", lambda d: flip_icon(d, -1))
    icon("ui_flip_right", lambda d: flip_icon(d, 1))
    for name in ("panel_portal", "ui_close", "ui_zoom", "ui_flip_left", "ui_flip_right"):
        with Image.open(os.path.join(PS, name + ".png")) as im:
            print(name, im.size, im.mode)
