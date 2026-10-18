# Test coverage

This page lists what the offline tests of Omsi Prefetch `1.0.0-beta.1` cover, by gate name, and what they do not. The tests are built and run by `tools\Build-Modules.ps1` (unless `-SkipTests` is given) and never start OMSI. The list below is taken from `prefetch\tests\Prefetch.Tests\Program.cs` (the session program and package gates) and `prefetch\tests\native\ShimTests.cpp` (the native component). When this page and the tests disagree, the tests win and the page is a bug.

Related pages: [known limitations](../reference/known-limitations.md), [overview](../getting-started/overview.md), [native shim](../concepts/native-shim.md), [session program](../concepts/session-program.md).

## What the tests are

| Suite | What it is | How it runs |
| --- | --- | --- |
| `Prefetch.Tests.exe` (C#, x64) | Unit gates on a synthetic installation (a small OMSI folder in a temporary directory: one map with three tiles and two entrypoints, ground textures, an AI list, scenery objects with models and textures, an AI bus and a user bus), then the packaged session program run end to end. | `Prefetch.Tests.exe --package <artifacts\modules\omsi-prefetch>`; without `--package` the `package.manifest` gate is skipped and the program built next to the tests is used. |
| `ShimTests.exe` (C++, x86) | The native component, loaded as a DLL against a **fake bridge** (hook requests are served by a stand-in table; the detours are called directly), with real files and a real named pipe. | `ShimTests.exe`; each scenario runs in its own process. |
| `Prefetch.Tests.exe --survey <OMSI folder> <maps\<map>\global.cfg>` | Not a gate: reads a real installation read-only and reports the map, the tile index and the vehicles with timings. | Manual ([measuring the gain](../getting-started/measuring-the-gain.md#estimating-a-map-before-you-play-it)). |

## Session program gates (`Prefetch.Tests.exe`)

| Gate | What it checks |
| --- | --- |
| `text.encodings-and-tokens` | UTF-16, UTF-8 with BOM and Windows-1252 decoding; CRLF, LF and CR line breaks; the trailing line break; the tokens of a line (first tab field and quoted strings) and that section lines have none. |
| `text.o3d-texture-names` | The texture name stored in a `.o3d` (length-prefixed, image extension) is found, and a file with none yields none. |
| `settings.budget` | The automatic budget is 70 % of the free memory after 4 GB, at most 12 GB, zero when little is free; an explicit value; choices and lists; the defaults. |
| `map.global-cfg` | Friendly name, tiles, entrypoints, start tiles in file order then name order, every entrypoint when none is presented, ground textures cited, map-wide files (no tiles, no `.osn`), a missing or outside map is refused, the fingerprint follows the tiles. |
| `tiles.closure-and-index` | A tile's files (the tile file first, never the streamed `LM.bmp`), the tiles around a point, the saved tile index loaded back with the same files, and an index of another fingerprint not loaded. |
| `vehicles.scopes` | The map's AI found from its lists; the `load`, `essential` and `complete` scopes (the files each includes and excludes); sounds off; user vehicles; listed user vehicles with the log of a skipped entry. |
| `settings.usage` | The defaults of the reserve, the floor, the vehicle cap, the pre-open values, the monitor off, the debug log off, the first-open notice and the data limit; values read and clamped to their bounds; the automatic budget follows `reserve_mb`; an explicit budget stops loading at 1 GB free. |
| `disk.log-and-data-bounded` | A log never passes 1 MiB, only three are kept, verbose lines are not written without `debug_log`, the limit is marked once; the data of old maps is removed least recently used first, never the current map, even at a zero limit. |
| `holdings.hold-release` | A file is mapped and held once; files the game rewrites and session files are never held; budget and accounting; a missing file; first use and first-use order; a written file is released at once and never held again; release under memory pressure. |
| `holdings.game-streamed-files-never-held` | Every extension the game rewrites or streams (`.osn .rdy .tbmp .odr .tmp .log .sav .bak .owt .prt .lm.bmp`) is denied and never mapped; the map-wide files and a tile's own files exclude the streamed files; the tile file itself is still held. |
| `feed.release-is-never-queued` | A release (`X` line) is answered at once behind a burst of slow `U` lines, and survives a flood larger than the queue, which drops and counts hints. |
| `package.manifest` (with `--package`) | The packaged folder is a valid module for OmsiLaunch's own manifest parser and pins; identity, activation and bridge protocol; the host and native components; 29 settings, the flags of `ram_budget_mb` and `monitor`, the monitor and the debug log off by default; the conflict with `OmsiLoadingPoC.dll`; exactly the seven files of the module; the LGPL text. |
| `session.end-to-end` | The packaged session program run through OmsiLaunch's own session-program launcher (job object) with a fake OMSI client: **first session** (ready, `prepared` with `first_open`, start tiles, map and AI held, a tile beyond `tile_radius` not held, rewritten files not held, `omsi-started`, the pipe feed, release at OMSI's request, stop, exit code 0, the `session` summary with every line received, the learned list in first-use order, the written list, the saved tile index, one log); **second session** (not a first open, a learned file held, a replaced file not held again, a client that is not OMSI disconnected and counted, its lines ignored). |

## Native component scenarios (`ShimTests.exe`)

| Scenario | What it covers |
| --- | --- |
| `main` | All seven kernel32 hooks installed; the feed configured. Read-ahead correctness on a file of 300 KB (every byte read one at a time with a position and size query before each, then 4,000 random seeks from the beginning, the current position and the end with random read sizes, end of file, a negative position and a position past 4 GiB, each checked against the expected bytes and positions) and a file with a non-ASCII name; write-behind with its flushes; read-only open and the reopen when the game writes; tile reports; the release of a mapped file the game replaces; drain with open handles; the use reports (first use only, first write only, tile opens as `T`); and the statistics (reads served from the buffer and system reads under one twentieth of the application's, write-behind real writes under 20, no failures, one read-only open and one reopen, one release request, no dropped lines). |
| `missing-hooks` | A refused position hook (`SetFilePointer`): nothing is shimmed, reads stay exact, a write to a read-only candidate is opened as asked, no feed. |
| `features-off` | All three features off by setting: only the use reports remain; application reads equal system reads. |
| `protocol1` | A bridge table without `get_value` (protocol 1): the component refuses to start. |
| `core-refused` | `CreateFileW` refused: the start fails, and `stop` still emits the statistics. |

## What the tests do not do

- They never start OMSI, OmsiLaunch's plugin or the real bridge: the bridge is a fake in the native tests, and the session program talks to a fake OMSI client.
- They do not measure speed. The measured gain of the mechanisms is the result of the loading optimization study of 30/09/2026 on the RMG Leste map, which the repository README states as valid for the module ([overview](../getting-started/overview.md#what-you-get)).
- They do not exercise a very large map, a low-memory machine or concurrent programs that touch the held files.
- They do not test the two windows (the first-open window and the monitor).

## Documentation checks

`tools\Check-Docs.ps1` checks these pages: every relative link and anchor resolves, every setting and flag of `prefetch\module.template.json` appears in [settings](../reference/settings.md) with its default and bounds, and every page is listed in the [index](../README.md). Run it from the repository root after editing the documentation:

```text
powershell -File tools\Check-Docs.ps1
```

## Next

[Known limitations](../reference/known-limitations.md) · [Overview](../getting-started/overview.md) · [Index](../README.md)
