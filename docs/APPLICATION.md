# Workshop verification application (draft)

Fill in `<WORKSHOP LINK>` after the Workshop item is published, then post this in the
game's modding channel.

---

Requesting Workshop verification for the files capability.

I maintain this mod as NaHz:

**Probably Taken From Stardew** 1.0.0 (虚空当铺：可能是从牢乡那拿的)
`<WORKSHOP LINK — to be added after upload>`

It is a two-way bridge between Probably Stolen and Stardew Valley. On this side it reads its
own `Data/*.json` (type rules, incoming shipments, claimed-item state) and its own
`Textures/**` (item icons), and it writes item icons and the claimed-item state back there.
To set up the other half it unpacks `Bridge/payload.json` into the sibling install at
`steamapps/common/Stardew Valley/Mods/DeepSpacePortal/` (manifest, one DLL, one PNG), writes
`bridge.json` next to it with the data paths, and installs `StardewModdingAPI.exe` there only
when SMAPI is missing. It resolves the game root from `Application.dataPath`'s parent, because
Workshop mods live under `steamapps/workshop/content/<appid>/<id>` and walking up from the mod
folder cannot reach it. Payload files are stored as gzip+base64 inside one plain JSON file, so
the mod folder contains no DLL or EXE of its own beyond its listed assembly.

The mod touches no save files: save-side data goes through ModHelper's ModData helpers. It does
not read Steam files, the registry, credentials, browser data or any other user directory, and it
makes no network requests. It patches nothing with Harmony, and uses no reflection, P/Invoke,
dynamic code or threads. Both manifests declare only `files`.

Complete source and build instructions for this release:
https://github.com/Polymerizedcat/probably-taken-from-stardew

File-access inventory written for review (every path read or written):
https://github.com/Polymerizedcat/probably-taken-from-stardew/blob/main/docs/FILES.md

DLL SHA-256:
```
StardewPortal.dll   DBA0B220D54CDC5BCC7381411B9AA0A317BE31FECCFDF7AAE0BEE99DCA99A538
DeepSpacePortal.dll EEA47B0F7144DBEB9BB1214633B58203E246280C35455F6A9175079486D97EFA
```
(the second one ships inside `Bridge/payload.json`; `verify_payload.py` round-trips it byte for byte)

The build passed the v4 scanner with `files` as the only detected capability and no deprecated
or hard-blocked findings. Please review this release for the upcoming Workshop files verification
requirement.
