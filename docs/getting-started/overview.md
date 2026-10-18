# What Omsi Prefetch is

Omsi Prefetch (module id `omsi-prefetch`, version `1.0.0-beta.1`) is a runtime module of OmsiLaunch that makes OMSI 2 load maps and vehicles faster. It does this in two ways that work together: before OMSI is started it reads the files the session will need into memory, and while OMSI runs it removes most of the cost of the tiny file calls the game makes. This page says what the module does for you, what you get, what it does not change, and which parts it is made of. Installation is in [installation](installation.md); the first launch is in [first run](first-run.md).

Related pages: [installation](installation.md), [first run](first-run.md), [measuring the gain](measuring-the-gain.md), [how prefetching works](../concepts/how-it-works.md), [settings reference](../reference/settings.md), [known limitations](../reference/known-limitations.md), [FAQ](../reference/faq.md).

## Why OMSI is slow to load

OMSI 2 reads its content in very small pieces. A single session on a large map issues tens of millions of `ReadFile` calls of a few bytes each (39.8 to 48.8 million per session in the study this module comes from). Each call costs a trip into the operating system, so loading is limited by the number of calls, not by how fast the disk is. A faster disk helps very little; fewer calls help a lot.

## What the module does for you

| What happens | When | Effect |
| --- | --- | --- |
| The module reads the map and, as far as the memory budget allows, brings its files into RAM: what earlier sessions of this map used, the map-wide files, the tiles nearest the start, the map's AI vehicles, more tiles around the start. | Before `Omsi.exe` is started. OmsiLaunch starts OMSI only when this is ready. | OMSI finds its files already in memory. |
| The module answers OMSI's byte-by-byte reads from 64 KiB blocks instead of asking the operating system once per byte. | While OMSI runs. | Time inside `ReadFile` drops by two orders of magnitude. |
| The module collects OMSI's byte-by-byte writes of the terrain cache (`.rdy`) in 256 KiB blocks. | While OMSI runs. | Time inside `WriteFile` drops. |
| The module follows where you are on the map and brings in the tiles around you before OMSI asks for them. | While OMSI runs. | Fewer freezes while tiles load. |
| The module remembers what OMSI used on each map. | At the end of every session. | The next session of the same map starts from what you really use. |

You do not run anything. The module has no menu and no window unless you ask for one. Once installed it takes part in every session that OmsiLaunch starts (activation `default`), and you can switch it off per session or permanently ([turning it off](../guides/turning-off.md)).

## What you get

The comparison below is the one that isolates the module's mechanisms: the same optimized map package of the RMG Leste map, first with the plugin only observing and then acting, with the operating system's file cache emptied before the run with the mechanisms on (study runs R2-A and R3-A). The module packages the mechanisms of the loading optimization study of 30/09/2026: the map manifest learned from earlier sessions, tile anticipation, a vehicle cache, read-ahead of OMSI's small reads and write-behind of its terrain-cache writes. These results are taken as valid for the module.

| Indicator | Without | With | Change |
| --- | ---: | ---: | ---: |
| Until traffic is loaded, after "Loading Situation" | 190 s | 51 s | 3.7x faster |
| Until the game is usable (first 60 s without a freeze of 1 s or more) | 265 s | 71 s | 3.7x faster |
| Time frozen (frames of 1 s or more) | 75.0 s | 17.9 s | 4.2x less |
| Longest freeze | 35.4 s | 7.2 s | 4.9x less |
| "Loading Tile" step, average per cycle | 1538 ms | 382 ms | 4.0x faster |
| "Updating Terrain" step, average per cycle | 3744 ms | 549 ms | 6.8x faster |
| Time inside `ReadFile`, all threads | 146.6 s | 0.8 s | 183x less |
| Time inside `WriteFile`, all threads | 22.4 s | 3.8 s | 5.9x less |

The gain comes from the cost of tens of millions of tiny system calls, not from the speed of the disk. What does **not** change: the median frame time (about 52 ms, 19 to 20 frames per second) and the parts that are pure game CPU ("Refresh Schedules", about 2.1 to 2.2 s per cycle). The study is a single machine and a single map (n = 1); results on other hardware and maps will differ. [Measuring the gain](measuring-the-gain.md) shows how to compare on your own machine.

## What the module does not do

- It does not raise the frame rate. Frame time is game CPU and graphics; the module acts on loading and on freezes caused by loading.
- It does not change any file of your OMSI installation. It opens files for reading and keeps its own data below `.omsilaunch\module-data\omsi-prefetch\` ([safety](../concepts/safety.md)).
- It does not change add-ons, licenses, entitlements or ownership of any content. It only reads files inside your OMSI installation.
- It does not simulate keyboard, mouse or windows of OMSI, and it does not need OMSI to be modified: it works through the bridge of OmsiLaunch, which hooks imports of `Omsi.exe`.

## The three parts

| Part | Where it runs | What it does |
| --- | --- | --- |
| `Prefetch.Preloader.exe` (C#, x64) | A session program that OmsiLaunch starts **before** OMSI | Reads the requested map and holds as much as the budget allows in RAM, reports ready (only then OmsiLaunch starts OMSI), follows the session and remembers what the game used. |
| `prefetch-native.dll` (C++, x86) | Inside OMSI, loaded by the OmsiLaunch bridge | Read-ahead, write-behind and read-only open through hooks on `Omsi.exe`'s file functions, and use reports to the session program. |
| `Prefetch.Module.dll` (C#) | Inside OMSI | The managed entry the bridge requires; it only reports milestones (`armed`, `gameplay-ready`, `stopping`). |

The module is not part of OmsiLaunch and is not distributed with it. It is free software under the GNU General Public License, version 3; it uses OmsiLaunch, which is licensed under the GNU Lesser General Public License, version 3 only (see `NOTICE.txt` in the module folder).

## Next

[Installation](installation.md) · [First run](first-run.md) · [Measuring the gain](measuring-the-gain.md)
