#!/usr/bin/env python3
"""Builds the offline item/footprint table (kept OUTSIDE the mod folder).

Source: Mods/StardewPortal/Data/stardew_food.csv (the in-game export).
Output: .dsh_tools/docs/stardew_footprint.csv
Rules mirror the mod: types.json -> gridSize.
"""
import csv
import os
from collections import Counter

ROOT = r"D:\EntertainmentsPro\SteamLibrary\steamapps\common\Probably Stolen"
SRC = os.path.join(ROOT, "Mods", "StardewPortal", "Data", "stardew_food.csv")
OUT_DIR = os.path.join(ROOT, ".dsh_tools", "docs")
OUT = os.path.join(OUT_DIR, "stardew_footprint.csv")

SHAPE_BY_ID = {"(O)287": "2x2", "(O)288": "2x2", "(O)74": "2x2", "(O)107": "2x3",
               "(O)348": "对角2格", "(O)346": "对角2格", "(O)303": "对角2格", "(O)459": "对角2格", "(O)350": "对角2格"}
DRINK_IDS = {"(O)614", "(O)395", "(O)253", "(O)167"}
FOOD_CATS = {-5, -6, -7, -14, -25, -75, -79, -80, -81}
WEAPON_CATS = {-98}
ORE_CATS = {-12, -15}


def shape_of(qualified, category):
    if qualified in SHAPE_BY_ID:
        return SHAPE_BY_ID[qualified]
    if qualified in DRINK_IDS:
        return "1x1"
    if category in WEAPON_CATS:
        return "对角2格"
    if category in ORE_CATS:
        return "2x2"
    if category in FOOD_CATS or category == -4:
        return "2x2"
    return "1x1"


def main():
    rows = list(csv.DictReader(open(SRC, encoding="utf-8-sig")))
    out = []
    for row in rows:
        category = int(row["category"]) if row["category"].lstrip("-").isdigit() else 0
        out.append({
            "qualifiedId": row["qualifiedId"],
            "name": row["name"],
            "category": row["category"],
            "edibility": row["edibility"],
            "calories": row["calories"],
            "shape": shape_of(row["qualifiedId"], category),
        })

    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT, "w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(out[0].keys()))
        writer.writeheader()
        writer.writerows(out)

    counts = Counter(r["shape"] for r in out)
    print(f"wrote {OUT} ({len(out)} rows)")
    print("shape distribution:", dict(counts))


if __name__ == "__main__":
    main()
