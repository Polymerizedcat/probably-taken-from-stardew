"""Final Workshop preview: the AI key art + Stardew pixel goods from the mod."""
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = r"D:\EntertainmentsPro\SteamLibrary\steamapps\common\Probably Stolen"
SRC = os.path.join(ROOT, ".dsh_tools", "preview_src")
ITEMS = os.path.join(ROOT, "Mods", "StardewPortal", "Textures", "Items")
BASE = os.path.join(SRC, "final_v2.png")
OUT = os.path.join(ROOT, "Mods", "StardewPortal", "preview.png")

CHICKEN_BOX = None            # already baked into the base art
ADD_SHARD = False             # already baked into the base art   # to the left of the 那 in the title
SHARD_CENTRE = (472, 316)      # the shopkeeper's open palm
SHARD_SIZE = 52
COUNTER_Y = 368                # top edge of the goods row on the red counter mat
GOODS = [("sd_o_24", 306), ("sd_o_258", 352), ("sd_o_424", 398), ("sd_o_348", 444), ("sd_o_72", 490)]
GOOD_SIZE = 46


def load_item(name, size):
    path = os.path.join(ITEMS, name + ".png")
    if not os.path.exists(path):
        return None
    return Image.open(path).convert("RGBA").resize((size, size), Image.NEAREST)


def key_background(path, tolerance=30):
    """Flood-fills the flat backdrop from the corners so only the sprite stays."""
    from collections import deque

    img = Image.open(path).convert("RGBA")
    w, h = img.size
    px = img.load()
    bg = px[0, 0]
    seen = [[False] * h for _ in range(w)]
    queue = deque()

    for corner in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        queue.append(corner)
        seen[corner[0]][corner[1]] = True

    while queue:
        x, y = queue.popleft()
        r, g, b, a = px[x, y]
        if abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2]) > tolerance:
            continue
        px[x, y] = (r, g, b, 0)
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and not seen[nx][ny]:
                seen[nx][ny] = True
                queue.append((nx, ny))

    return img


def glow(size, colour=(160, 250, 255), strength=140):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    c = size / 2.0
    for y in range(size):
        for x in range(size):
            d = ((x - c) ** 2 + (y - c) ** 2) ** 0.5 / c
            if d < 1:
                px[x, y] = (colour[0], colour[1], colour[2], int(strength * (1 - d) ** 2))
    return img.filter(ImageFilter.GaussianBlur(size * 0.05))


def main():
    img = Image.open(BASE).convert("RGBA")
    w, h = img.size

    shard = load_item("sd_o_74", SHARD_SIZE) if ADD_SHARD else None
    if shard:
        cx, cy = SHARD_CENTRE
        img.alpha_composite(glow(150), (cx - 75, cy - 75))
        img.alpha_composite(shard, (cx - shard.width // 2, cy - shard.height // 2))

    if CHICKEN_BOX is not None:
        chicken = key_background(os.path.join(SRC, "icon48.png"), tolerance=45)
        chicken = chicken.resize((CHICKEN_BOX[2], CHICKEN_BOX[2]), Image.NEAREST)
        shadow = Image.new("RGBA", (chicken.width + 24, chicken.height + 24), (0, 0, 0, 0))
        ImageDraw.Draw(shadow).ellipse([12, 12, chicken.width + 12, chicken.height + 12], fill=(0, 0, 0, 150))
        img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(6)), (CHICKEN_BOX[0] - 12, CHICKEN_BOX[1] - 8))
        img.alpha_composite(glow(150, (255, 236, 180), 90), (CHICKEN_BOX[0] - 49, CHICKEN_BOX[1] - 49))
        img.alpha_composite(chicken, (CHICKEN_BOX[0], CHICKEN_BOX[1]))

    for name, x in GOODS:
        icon = load_item(name, GOOD_SIZE)
        if icon is None:
            continue
        shadow = Image.new("RGBA", (icon.width + 12, icon.height + 12), (0, 0, 0, 0))
        ImageDraw.Draw(shadow).ellipse([6, icon.height - 6, icon.width + 6, icon.height + 6], fill=(0, 0, 0, 105))
        img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(3)), (x - 6, COUNTER_Y))
        img.alpha_composite(icon, (x, COUNTER_Y))

    return finish(img)


def finish(img):
    img.convert("RGB").save(OUT, "PNG", optimize=True)
    print("preview:", OUT, img.size, round(os.path.getsize(OUT) / 1024), "KB")


if __name__ == "__main__":
    main()
