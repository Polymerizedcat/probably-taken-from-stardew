"""Generates the 16x16 pixel-art icons used by the Stardew <-> pawnshop portal mod."""
import os
from PIL import Image

OUT_PS = r"D:\EntertainmentsPro\SteamLibrary\steamapps\common\Probably Stolen\Mods\StardewPortal\Textures\Items"
OUT_SV = r"D:\EntertainmentsPro\SteamLibrary\steamapps\common\Probably Stolen\.dsh_tools\src\sv\assets"

TRANSPARENT = (0, 0, 0, 0)


def new_sheet():
    return Image.new("RGBA", (16, 16), TRANSPARENT)


def portal_book():
    """A small purple ledger with a glowing portal on the cover."""
    img = new_sheet()
    px = img.load()

    leather = (86, 44, 112, 255)
    leather_dark = (58, 28, 78, 255)
    leather_light = (118, 68, 148, 255)
    page = (226, 214, 190, 255)
    gold = (214, 176, 84, 255)
    glow = (150, 236, 236, 255)
    glow_core = (236, 255, 255, 255)

    # page block on the right
    for y in range(3, 14):
        for x in range(11, 14):
            px[x, y] = page
    # cover
    for y in range(2, 15):
        for x in range(2, 12):
            px[x, y] = leather
    # spine + shading
    for y in range(2, 15):
        px[2, y] = leather_dark
        px[3, y] = leather_light
    for x in range(2, 12):
        px[x, 2] = leather_dark
        px[x, 14] = leather_dark

    # gold trim
    for x in range(4, 11):
        px[x, 4] = gold
    for x in range(4, 11):
        px[x, 12] = gold

    # portal swirl on the cover
    cx, cy = 7, 8
    ring = [(cx - 2, cy - 2), (cx - 1, cy - 2), (cx, cy - 3), (cx + 1, cy - 2),
            (cx + 2, cy - 1), (cx + 2, cy), (cx + 1, cy + 1), (cx, cy + 2),
            (cx - 1, cy + 2), (cx - 2, cy + 1), (cx - 2, cy), (cx - 3, cy - 1)]
    for (x, y) in ring:
        px[x, y] = glow
    px[cx, cy] = glow_core
    px[cx - 1, cy - 1] = glow_core
    img.save(os.path.join(OUT_PS, "portal_book.png"))
    return img


def portal_object():
    """A stone ring with a glowing portal inside, for the Stardew Valley object."""
    img = new_sheet()
    px = img.load()

    stone = (108, 104, 118, 255)
    stone_dark = (72, 68, 82, 255)
    stone_light = (142, 138, 154, 255)
    glow = (128, 92, 196, 255)
    glow_light = (176, 138, 232, 255)
    spark = (232, 214, 255, 255)

    # outer ring (roughly circular arch)
    for y in range(1, 15):
        for x in range(1, 15):
            dx, dy = x - 7.5, y - 7.5
            d = (dx * dx + dy * dy) ** 0.5
            if 4.6 <= d <= 6.6:
                px[x, y] = stone
            elif d < 4.6:
                px[x, y] = glow
    # shading
    for y in range(1, 15):
        for x in range(1, 15):
            if px[x, y] == stone and (x + y) % 5 == 0:
                px[x, y] = stone_dark
            if px[x, y] == stone and (x * 3 + y) % 7 == 0:
                px[x, y] = stone_light
    # inner swirl
    for (x, y) in [(6, 5), (7, 5), (8, 6), (8, 7), (7, 8), (6, 8), (5, 7), (5, 6), (7, 7)]:
        px[x, y] = glow_light
    px[7, 7] = spark
    px[9, 4] = spark
    img.save(os.path.join(OUT_SV, "portal.png"))
    return img


if __name__ == "__main__":
    os.makedirs(OUT_PS, exist_ok=True)
    os.makedirs(OUT_SV, exist_ok=True)
    portal_book()
    portal_object()
    for path in (os.path.join(OUT_PS, "portal_book.png"), os.path.join(OUT_SV, "portal.png")):
        with Image.open(path) as check:
            print(path, check.size, check.mode)
