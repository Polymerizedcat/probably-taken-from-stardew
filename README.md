# Probably Taken From Stardew

**Probably Stolen × Stardew Valley portal** — set up a portal in Stardew Valley, drop in crops,
fish, cooked dishes, artisan goods, ore and weapons, and ship them into the pawnshop. They arrive
as real items with the original art, the same calories, chemistry and a sensible price.

English | [简体中文](README.zh-CN.md)

> 虚空当铺：可能是从牢乡那拿的 — the Chinese name of this mod.

---

## The two mods

| Component | Game | ID | What it does |
|---|---|---|---|
| `StardewPortal` | Probably Stolen | `StardewPortal` | Pawnshop side: the ledger UI, item definitions, receiving, and installing the other half |
| `DeepSpacePortal` | Stardew Valley | `NaHz.DeepSpacePortal` | Stardew side: the portal object, its chest UI, and writing shipments to the data folder |

When the pawnshop mod is enabled it installs the Stardew-side companion mod (and SMAPI, if it is
missing) into the `Stardew Valley` folder next to the game, under the same `steamapps/common`.

## Features

- **Ledger UI**: a starry paper page in the pawnshop listing incoming shipments, one click to collect
- **Original art**: item icons are cut from Stardew Valley's own sprite sheets at runtime
- **Full attribute mapping**: type tags (food / processed / alcohol / material / ore / weapon /
  contraband), inventory footprint, calories, chemical composition, contraband tier, price conversion
- **Refusal rules**: seeds, fertiliser, tackle, tools, trinkets and furniture stay in the portal with a message
- **Data driven**: `Data/types.json` controls types, footprints, prices, flavour text and chemistry

## Install

**Steam Workshop**: subscribe → **activate** it in the Mods menu → restart the game.
On first enable it asks for the `files` capability (it has to write into the Stardew install).

**Manual**: copy `release/StardewPortal/` into `<Probably Stolen>/Mods/`.

**Stardew side**: nothing to do. The pawnshop mod installs the companion mod (and SMAPI when
missing) into `<steamapps>/common/Stardew Valley/`; launch Stardew with `StardewModdingAPI.exe`.

## Repository layout

```
src/ProbablyStolen/    pawnshop mod source (plain csc build, no NuGet)
src/StardewValley/     Stardew mod source (SMAPI 4.x, net6.0)
tools/                 icon / panel / preview / table generators
docs/                  build notes, file-access inventory, verification notes
release/StardewPortal/ the ready-to-install / upload package
```

## Build

See [docs/BUILD.md](docs/BUILD.md).

## Capabilities and verification

Both manifests declare only **`files`** — no `network`, `process`, `steam`, `dynamic-code`,
`native`, `reflection-external` or `threads`.
The complete list of paths the mods read or write is in [docs/FILES.md](docs/FILES.md);
the Workshop verification notes are in [docs/VERIFICATION.md](docs/VERIFICATION.md).

## Licence

MIT. The Stardew side bundles SMAPI, which is MIT licensed by Pathoschild.
Stardew Valley's artwork is read at runtime and is not redistributed with this mod.
