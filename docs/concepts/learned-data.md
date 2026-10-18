# Learned data: the learned list, the written list and the tile index

For every map, Omsi Prefetch keeps three small files that make the next session of the same map better than the first one. This page explains what they contain, how they are built, how they are used, what makes them obsolete, what a second run looks like, where they live and how they are bounded. The format of each file is specified in the [files and directories reference](../reference/files-and-directories.md); how to delete them is in [clearing data](../guides/clearing-data.md).

Related pages: [how prefetching works](how-it-works.md), [session program](session-program.md), [memory and budget](memory-and-budget.md), [first run](../getting-started/first-run.md), [safety](safety.md).

## Where they live

```text
<OMSI root>\.omsilaunch\module-data\omsi-prefetch\
  maps\<folder>-<hash>\learned.txt
  maps\<folder>-<hash>\written.txt
  maps\<folder>-<hash>\tiles.idx
  logs\preloader-<date>-<time>.log
```

`<folder>` is the name of the map's folder, with every character that is not a letter, a digit, `-`, `_` or `.` replaced by `_` and cut to 40 characters. `<hash>` is the first eight hex digits of the SHA-256 of the map identity (`maps\<folder>\global.cfg`, lower case), so two maps never share a folder even when their names clash. The folder is created on the first save.

## The learned list (`learned.txt`)

The list of files OMSI used on this map, one per line, relative to the OMSI folder, in the order the game first used them. It is the most important file: on the next session the module holds these files first, in that order, before anything else.

How it is built: the native component reports the first use of every file (a `U` line). The session program keeps the order of the first reports. At every save, the new list is the old list followed by the files used in this session that were not in it, without duplicates, without files the module never holds (see [safety](safety.md#what-is-never-held)) and without files that no longer exist, at most 200,000 entries.

What is in it: every file inside the OMSI folder that the game opened during the session, not only the map's: vehicles, scenery, textures, sounds, the game's own interface files. Files outside the OMSI folder are ignored.

Two consequences follow:

- The list only grows. A file the game used once in some session stays in it until you clear the data. If you drive many different routes on a map, the list grows to cover all of them.
- First-use order is the order of holding. When the budget is smaller than the list, what the game used last is the part that does not get held before OMSI starts. The tile prediction and the [pre-open](pre-open.md) bring in some of it later.

With `learn` off the list is still read and used, but it is not updated.

## The written list (`written.txt`)

The list of files the game wrote on this map (for example `laststn.osn`, `.rdy` caches, logs, files a script writes). They are never held in memory: a file mapped by another process cannot be replaced or truncated, and holding one the game rewrites would stall it. The list is loaded at the start of every session, and every file the game writes during the session is added to it (a `W` or `X` report). Independently of this list, files with the extensions `.osn .rdy .tbmp .odr .tmp .log .sav .bak .owt .prt .lm.bmp` are never held, and neither are the files the session transaction of OmsiLaunch owns ([safety](safety.md#what-is-never-held)).

## The tile index (`tiles.idx`)

For each tile of the map, the list of the files it needs: the tile's own files, then every object and spline it places with everything those cite ([session program](session-program.md#the-map-wide-files-and-the-tiles)). Without it the module would have to read every tile file at the start of every session to know what to hold around the player.

How it is built: a tile is computed when it is first needed (a start tile before OMSI, a tile around the player during the session), and the survey activity computes the rest in the background, nearest the start first. The index is saved every two minutes and at the end when it has grown. The survey of a large map takes seconds to tens of seconds on a cold disk (Berlin-Spandau of the clean installation: 369 tiles, 4,664 files, 597 MB; 14.4 s cold, 2.3 s warm).

Format: a header line, a `fingerprint` line, then one `F` line per distinct file (an id and its relative path) and one `T` line per tile (the tile's index in the `[map]` list of `global.cfg` and the ids of its files). Each file is stored once, however many tiles use it.

What makes it obsolete: the index carries the **fingerprint** of the map, a hash over the name, size and last-write time of `global.cfg` and of every tile file. If any of them changed, the saved index is discarded when the session starts and rebuilt in the background. It is not compared with the files the tiles cite: a texture or an object changed without touching the tile files is not noticed until you clear the data.

With `survey` off no index is loaded, computed or saved, and the module neither holds the tiles around the start before OMSI nor follows the player during the session.

## What changes on the second run

| | First session of a map | Later sessions |
| --- | --- | --- |
| Learned list | Empty. The module holds only what it can derive: the map-wide files, the tiles near the start (from the survey), the map's AI vehicles. | Held first, in first-use order. |
| Tile index | Computed during the preparation for the start tiles and in the background for the rest. | Loaded from `tiles.idx`; the start tiles are known immediately. |
| Window | The [first-open window](../getting-started/first-run.md#the-first-open-window) is shown. | Not shown. |
| `pre_open` (`learned`) | Nothing to open ahead. | The vehicle files the game used before are opened ahead ([pre-open](pre-open.md)). |
| Written list | Empty. | Known; the files the game wrote are never held. |

## Bounds and clean-up

- The data of all maps together is kept under `data_limit_mb` (default 256 MB, between 16 and 4096). When a session starts and the data is larger, the maps used least recently (by the last-write time of their files) are removed first, never the map of the current session, until it fits. If the map of the current session alone is larger than the limit, it stays. The log says how much was removed.
- The logs are bounded separately: at most three files of at most 1 MiB ([log format](../reference/log-format.md)).
- A save writes a temporary file (`*.tmp`) and replaces the old file with it, so a crash during a save leaves the old file intact; a leftover `*.tmp` is removed with the map's folder.
- Typical sizes are a few megabytes per map. `/clear-module-data:omsi-prefetch` removes everything ([clearing data](../guides/clearing-data.md)).

## Next

[Memory and budget](memory-and-budget.md) · [Vehicle scopes](vehicle-scopes.md) · [Files and directories](../reference/files-and-directories.md)
