# OMSI Prefetch (module `omsi-prefetch`)

Published versions © Leo Monteiro / Portal OmsiModsBR - https://github.com/OmsiModsBR, under the GNU General Public License, version 3. Not part of OmsiLaunch and not distributed with it. Built from the mechanisms of the loading optimization study as an OmsiLaunch Runtime Module (bridge protocol 2).

Measured results (the study of 30/09/2026): see the table in the repository's main README.

## How it works

| Part | Where it runs | What it does |
| --- | --- | --- |
| `prefetch-native.dll` (C++, x86) | inside OMSI, loaded by the bridge | Asks the bridge for IAT hooks on `Omsi.exe`'s kernel32 file functions and D3DX loaders. Read-ahead (byte-by-byte reads served from 64 KiB blocks), write-behind (the `.rdy` terrain cache collected in 256 KiB blocks), read-only open of assets below `read_only_folders`, and use reports (U/T/W lines) to the session program. |
| `Prefetch.Module.dll` (C#) | inside OMSI | The managed entry the bridge requires; reports milestones only. |
| `Prefetch.Preloader.exe` (C#, x64) | session program, started by OmsiLaunch **before** OMSI | Reads the requested map and holds in RAM as much as the budget allows, reports ready (only then OmsiLaunch starts OMSI), follows the session, remembers what the game used. |

Before OMSI (in this order, until the budget, the 5-minute limit or low free memory stops it): what earlier sessions of the map used (first-use order), the map-wide files, the tiles nearest the start (radius 2), the map's AI vehicles, more tiles around the start (up to `tile_radius`).

During the session: tiles around the player (centroid of the tile files OMSI opened in the last minute), the user's vehicles (`user_vehicles`), vehicle files opened ahead of the game without being held (`pre_open`), a vehicle's folder opened in parallel when its preview starts (`pre_open_on_first_use`), and a memory guard (below 1 GB free it releases files the game did not use, least valuable first).

First open of a map (nothing learned, no tile index): unless `first_open_notice` is off, the preloader shows its own notice window ("Pré-carregamento sendo inicializado…") until OMSI is started; the tray of OmsiLaunch is not shown yet at that point.

Holding: each file is mapped read-only and every page touched; the file handle is closed at once, so the game's own opens never meet a sharing conflict. Files the game writes are never held (written list, extension list, the session's transaction files). If OMSI tries to replace or truncate a held file (`ERROR_USER_MAPPED_FILE`), the native component asks for its release (X line) and retries for up to 1.5 s; the preloader releases it and never holds it again.

## What OmsiLaunch tells the module

- **The map.** The session program receives the session's map from OmsiLaunch (`world.map` of its context) for every way to start: a new map, the map's last state, or a saved situation (OmsiLaunch resolves the situation's map). Nothing is configured by an ini file, and the module has none.
- **The settings.** Every setting below reaches the module through OmsiLaunch, by any of: the module's own flag (`/prefetch-ram:6144`), `/module-setting:omsi-prefetch.<key>=<value>`, `LaunchSpec.Modules.Settings`, a session profile preset, or `[omsi-prefetch]` in `<OMSI>\.omsilaunch\modules.ini`. OmsiLaunch validates them against the manifest and refuses a value outside its bounds.
  A predefined session (an OmsiLaunch session profile preset) can carry them, one set per map or per way of playing:

  ```yaml
  modules:
    omsi-prefetch:
      settings:
        ram_budget_mb: 8192
        user_vehicles: list
        user_vehicle_list: "Bus1;Bus2"
        monitor: false
  ```

  The command line cannot also set a key the preset sets (`OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT`); `enabled: false` keeps the module out of that session.
- **The monitor window** (what is held in memory) opens **only when OmsiLaunch asks for it**: `/prefetch-monitor`, `monitor=true` in the settings file, a preset or `/module-setting`. It is off by default.

## Settings (`module.json`)

| Key | Default | Flag | Replaces (proof of concept ini) |
| --- | --- | --- | --- |
| `ram_budget_mb` | `auto`: 70 % of the memory free at the start, after keeping `reserve_mb`, at most 12 GB | `/prefetch-ram:<MB>` | `MaxWarmBytesMB` |
| `reserve_mb` | `4096` | `/prefetch-reserve:<MB>` | `MinFreePhysicalMB` |
| `stop_below_free_mb` | `auto`: 2048, or 1024 with an explicit budget | `/prefetch-stop-below:<MB>` | `StopPrefetchBelowFreeMB` |
| `vehicle_budget_mb` | `0` (only the budget limits) | `/prefetch-vehicle-ram:<MB>` | `OrcamentoMB` |
| `monitor` | `false` | `/prefetch-monitor` | (window) |
| `read_ahead`, `write_behind`, `read_only_open` | `true` | | `ReadAhead`, `WriteBehind`, `ReadOnlyOpen` |
| `read_only_folders` | `vehicles\` | | `ReadOnlyPrefixes` |
| `tile_radius` | `12` | | |
| `ai_vehicles` | `complete` (`off`, `essential`, `load`, `complete`) | | `IA`, `ModoIA` |
| `user_vehicles`, `user_vehicle_list`, `user_vehicle_scope` | `none` (`none`, `list`, `all`), empty, `essential` | | `Usuario`, `Lista`, `ModoUsuario` |
| `fleet_scope` | `load` | | `ModoFrota` |
| `sounds` | `true` | | `Sons` |
| `pre_open` | `learned` (`none`, `learned`, `all`, `list`) | `/prefetch-pre-open:<mode>` | `PreAbrir` |
| `pre_open_scope`, `pre_open_list`, `pre_open_max_mb`, `pre_open_threads` | `load`, empty, `2048`, `2` | | `PreAbrirModo`, `PreAbrirLista`, `PreAbrirMaxMB`, `PreAbrirThreads` |
| `pre_open_on_first_use`, `pre_open_first_use_max_mb`, `pre_open_first_use_threads` | `true`, `400`, `4` | | `PreAbrirAoAbrir`, `PreAbrirAoAbrirMaxMB`, `PreAbrirAoAbrirThreads` |
| `learn`, `survey` | `true` | | the manifest and the tile index |
| `first_open_notice` | `true` | | a small window on a map's first open, explaining the wait before OMSI opens |
| `debug_log` | `false` | `/prefetch-debug` | `Verbose` |
| `data_limit_mb` | `256` | | |

Not carried over, on purpose: `Mode`, `Gate`, `HelperExe`, `ManifestPath` and `LogDir` (OmsiLaunch starts the module, checks the build and gives it its folders), `[Hooks]` (the module hooks only the file functions of `Omsi.exe`; no internal hooks, no `Present`, no sampler), `[Diagnostics]` and `[Profile]` (the proof of concept's profiling) and `ChunkMB` (files are mapped whole).

Activation `default`: once installed, the module runs in every session; `/no-module:omsi-prefetch` (or `enabled=false` in `.omsilaunch\modules.ini`) switches it off. Failure policy `degrade`. Conflicts: `OmsiLoadingPoC.dll`.

## Disk use

The module is made for use, not for debugging. Its footprint in `<OMSI>\.omsilaunch\module-data\omsi-prefetch\` is bounded:

- the log is a few dozen lines per session, at most 1 MiB per file (one marker line, then the rest is dropped) and the three most recent files are kept; per-file and per-vehicle lines exist only with `debug_log`;
- what each map taught the module (`learned.txt`, `written.txt`, `tiles.idx`) is a few MB per map, and the maps' data stays under `data_limit_mb` (256 MB): when it grows past it, the maps used least recently are removed first, never the current one;
- it writes no CSV, no sample traces and no copies of OMSI's files.

## Data (`<OMSI>\.omsilaunch\module-data\omsi-prefetch\`)

| File | Content |
| --- | --- |
| `maps\<folder>-<hash>\learned.txt` | Files the game used on the map, first-use order (installation-relative). |
| `maps\<folder>-<hash>\written.txt` | Files the game wrote: never held. |
| `maps\<folder>-<hash>\tiles.idx` | Tile index (files each tile needs), with the map's fingerprint (global.cfg and every tile file). |
| `logs\preloader-*.log` | The three most recent session logs (at most 1 MiB each). |

`/clear-module-data:omsi-prefetch` deletes all of it.

## Build and test

```powershell
tools\Build-Modules.ps1
```

Builds the three components, publishes the preloader as one framework-dependent file, assembles `artifacts\modules\omsi-prefetch\` with `module.json` pinned, and runs:

- `ShimTests.exe` (native, 5 scenarios on real files with a fake bridge): read-ahead correctness with every seek form, write-behind with a concurrent reader, read-only open and reopen on write, tile reports, the release of a mapped file the game replaces, stop with open handles, protocol 1 refusal, missing position hooks, features off, a refused core hook.
- `Prefetch.Tests.exe --package <dir>` (C#): text and model parsing, the budget, `global.cfg`, tile closures and the saved index, vehicle scopes, holdings, the package validated by OmsiLaunch's own manifest parser and pins, and the packaged session program run end to end through OmsiLaunch's session-program launcher (job object) with a fake OMSI client: two sessions (first open, learned order, written list, release on request, a client that is not OMSI refused).

`Prefetch.Tests.exe --survey <OMSI folder> <maps\<map>\global.cfg>` reads a real installation read-only and reports the map, the tile index and the vehicles with timings (Berlin-Spandau of the clean install: 369 tiles, 4,664 files, 597 MB; 14.4 s cold, 2.3 s warm).

## Open points

- OMSI's presented entrypoint order is not known offline: the start tiles of the file order and of the name order are both used.
- A file still open through a read-only handle reopened for writing when the module stops is handed back to the game's read-only handle; a later write to it would be refused (needs a write to a vehicle asset that is open at that very moment).
- As in the study that measured it, terminating OMSI while it writes a `.rdy` loses the bytes still buffered (the file is a regenerable cache).
