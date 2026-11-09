#!/usr/bin/env python3
"""Round-trip check: the text payload must decode back to byte-identical files."""
import base64
import gzip
import hashlib
import json
import os
import sys

ROOT = r"D:\EntertainmentsPro\SteamLibrary\steamapps\common\Probably Stolen"
PAYLOAD = os.path.join(ROOT, "Mods", "StardewPortal", "Bridge", "payload.json")
SV_SRC = os.path.join(ROOT, ".dsh_tools", "src", "sv")
SMAPI = os.path.join(ROOT, ".dsh_tools", "smapi_payload")

EXPECT_MOD = {
    "manifest.json": os.path.join(SV_SRC, "manifest.json"),
    "DeepSpacePortal.dll": os.path.join(SV_SRC, "bin", "DeepSpacePortal.dll"),
    "assets/portal.png": os.path.join(SV_SRC, "assets", "portal.png"),
}


def sha(path):
    with open(path, "rb") as handle:
        return hashlib.sha256(handle.read()).hexdigest()


def main():
    payload = json.load(open(PAYLOAD, encoding="utf-8"))
    files = payload["files"]
    problems = []

    mod_entries = {f["path"]: f for f in files if f["root"] == "mod"}
    root_entries = {f["path"]: f for f in files if f["root"] == "root"}

    for rel, source in EXPECT_MOD.items():
        entry = mod_entries.get(rel)
        if not entry:
            problems.append(f"missing mod entry: {rel}")
            continue
        data = base64.b64decode(entry["b64"])
        if entry.get("gzip"):
            data = gzip.decompress(data)
        if hashlib.sha256(data).hexdigest() != sha(source):
            problems.append(f"content mismatch: {rel}")
        print(f"  mod/{rel}: {len(data)} bytes ok")

    # every bundled SMAPI file must match its source byte for byte
    mismatched = 0
    for rel, entry in root_entries.items():
        source = os.path.join(SMAPI, rel.replace("/", os.sep))
        if not os.path.exists(source):
            problems.append(f"smapi entry without source: {rel}")
            continue
        data = base64.b64decode(entry["b64"])
        if entry.get("gzip"):
            data = gzip.decompress(data)
        if hashlib.sha256(data).hexdigest() != sha(source):
            mismatched += 1
            problems.append(f"smapi content mismatch: {rel}")

    print(f"  smapi: {len(root_entries)} file(s) decoded, {mismatched} mismatch(es)")
    print(f"  payload bytes: {os.path.getsize(PAYLOAD)}")

    if problems:
        print("RESULT: FAIL")
        for problem in problems[:20]:
            print("  -", problem)
        return 1

    print("RESULT: PASS (payload round-trips to identical bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
