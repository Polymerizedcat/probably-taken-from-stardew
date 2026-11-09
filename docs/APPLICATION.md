# Workshop verification application (final)

Posting text for the game's modding channel. The Workshop item is published:
https://steamcommunity.com/sharedfiles/filedetails/?id=3836592867

---

Requesting Workshop verification for the files capability.

I maintain this mod as NaHz:

**Probably Taken From Stardew** 1.0.0 (虚空当铺：可能是从牢乡那拿的)
https://steamcommunity.com/sharedfiles/filedetails/?id=3836592867

It is a bridge between Probably Stolen and Stardew Valley. On this side it reads its own
`Data/*.json` (type rules, incoming shipments, claimed-item state) and its own `Textures/**`
(item icons), and it writes item icons and the claimed-item state back there. To set up the other
half it unpacks `Bridge/payload.json` into the sibling install at
`steamapps/common/Stardew Valley/Mods/DeepSpacePortal/` (manifest, one DLL, one PNG), writes
`bridge.json` next to it with the data paths, and installs `StardewModdingAPI.exe` there only when
SMAPI is missing. It resolves the game root from `Application.dataPath`'s parent, because Workshop
mods live under `steamapps/workshop/content/<appid>/<id>` and walking up from the mod folder cannot
reach it. Payload files are stored as gzip+base64 inside one plain JSON file, so the mod folder
contains no DLL or EXE of its own beyond its listed assembly.

The mod touches no save files: save-side data goes through ModHelper's ModData helpers. It does
not read Steam files, the registry, credentials, browser data or any other user directory, and it
makes no network requests. It patches nothing with Harmony, and uses no reflection, P/Invoke,
dynamic code or threads. Both manifests declare only `files`.

If `files` is more than you would like to sign off on for a mod of this kind, I am happy to drop
it instead. The Stardew side is just a small SMAPI mod - one DLL, a manifest and one PNG - and I
could ship it inside this item as an ordinary folder with a short "copy this into
Stardew Valley/Mods" note, so the player does the copy and this mod never writes outside its own
directory. The trade-off is one manual copy and a restart on the Stardew side, which seems a
reasonable price for not needing the capability at all. Either build works for me; please let me
know which you would prefer and I will publish that one.

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
or hard-blocked findings. Thank you for taking a look at this release ahead of the Workshop files
verification requirement.
