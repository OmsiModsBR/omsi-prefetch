# Changelog

All notable changes to Omsi Prefetch (module id `omsi-prefetch`) are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the version numbers follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html), with pre-release suffixes for betas.

## 1.0.0-beta.1 - unreleased

First beta of Omsi Prefetch as an OmsiLaunch runtime module (bridge protocol 2, activation `default`, failure policy `degrade`). It packages the mechanisms of the loading optimization study of 30/09/2026 on the RMG Leste map. The comparison that isolates them (same optimized map package, plugin observing versus acting, file cache emptied before the run with the mechanisms on) measured 3.7x faster until traffic is loaded and until the game is usable, 4.2x less time frozen, 183x less time inside `ReadFile` and 5.9x less inside `WriteFile`; see [docs/getting-started/overview.md](docs/getting-started/overview.md) for the table and its scope (one machine, one map).

### Added

- The session program `Prefetch.Preloader.exe` (x64, started by OmsiLaunch before OMSI): reads the session's map and holds files in RAM up to a budget, in this order: what earlier sessions of the map used, the map-wide files, the tiles nearest the start, the map's AI vehicles, more tiles around the start. OmsiLaunch starts OMSI only when it reports ready.
- During the session: tile anticipation around the player's estimated position, the user's vehicles (`user_vehicles`), vehicle files opened ahead of the game without being held (`pre_open`, and a vehicle's folder in parallel when its preview starts), and a memory guard that releases files the game did not use when free memory falls below 1 GB.
- Learned data per map in `.omsilaunch\module-data\omsi-prefetch\maps\`: the files the game used in first-use order (`learned.txt`), the files it wrote (`written.txt`) and the tile index with the map's fingerprint (`tiles.idx`), saved every two minutes and at the end.
- The native component `prefetch-native.dll` (x86, inside OMSI, hooks requested from the OmsiLaunch bridge by symbolic target): read-ahead of OMSI's byte-by-byte reads in 64 KiB blocks, write-behind of the `.rdy` terrain cache in 256 KiB blocks, read-only open of assets below `read_only_folders`, release of a held file when OMSI cannot replace it (retried for up to 1.5 s), and use reports to the session program.
- The managed component `Prefetch.Module.dll`, which reports `armed`, `gameplay-ready` and `stopping`.
- The vehicle scopes `essential`, `load` and `complete`, for the map's AI vehicles (`ai_vehicles`), the user's vehicles (`user_vehicle_scope`) and the map's own fleet (`fleet_scope`).
- 29 settings in the manifest, all validated by OmsiLaunch, reachable by `/module-setting:omsi-prefetch.<key>=<value>`, `.omsilaunch\modules.ini` and session-profile presets; seven of them have flags (`/prefetch-ram`, `/prefetch-reserve`, `/prefetch-stop-below`, `/prefetch-vehicle-ram`, `/prefetch-monitor`, `/prefetch-pre-open`, `/prefetch-debug`).
- A first-open window, shown while a map is prepared for the first time (`first_open_notice`), and an optional monitor window (`monitor`, off by default). Both are in Portuguese for a Portuguese Windows display language and in English otherwise.
- Bounded disk use: a log of a few dozen lines per session, at most 1 MiB per file and three files kept; the maps' data kept under `data_limit_mb` (256 MB by default), least recently used maps removed first; `/clear-module-data:omsi-prefetch` deletes everything.
- Runtime events `module.omsi-prefetch.armed`, `gameplay-ready`, `stopping`, `hooks`, `stats`, `prepared` and `session`.
- A declared conflict with the proof-of-concept plugin `OmsiLoadingPoC.dll`.
- Offline tests: 13 gates of `Prefetch.Tests.exe` (text, settings, map, tiles, vehicles, disk bounds, holdings, release priority, package and an end-to-end session) and 5 scenarios of `ShimTests.exe` for the native component.
- User documentation under `docs\` (this release's first).

### Not carried over from the proof of concept

`Mode`, `Gate`, `HelperExe`, `ManifestPath` and `LogDir` (OmsiLaunch starts the module, checks the build and gives it its folders), `[Hooks]` (the module hooks only the file functions of `Omsi.exe` and the D3DX loaders: no internal hooks, no `Present`, no sampler), `[Diagnostics]` and `[Profile]` (the proof of concept's profiling) and `ChunkMB` (files are mapped whole).

### Known limitations

See [docs/reference/known-limitations.md](docs/reference/known-limitations.md). The main ones: terminating OMSI loses the bytes still buffered for a `.rdy` terrain cache (a regenerable file); the budget is capped at 12 GB and does not evict tiles the player left; the first session of a map only teaches the module; and the published gain is from one machine and one map.
