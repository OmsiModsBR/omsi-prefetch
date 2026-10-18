# How prefetching works

This page describes Omsi Prefetch end to end: the three components, the order of events from the start of a session to its end, what each component is responsible for, and how they talk to each other. The other concept pages go into each mechanism: the [session program](session-program.md), the [native shim](native-shim.md), the [learned data](learned-data.md), [memory and budget](memory-and-budget.md), [vehicle scopes](vehicle-scopes.md), [pre-open](pre-open.md) and [safety](safety.md). Everything here is taken from the module's source under `prefetch\`.

Related pages: [overview](../getting-started/overview.md), [first run](../getting-started/first-run.md), [settings reference](../reference/settings.md), [files and directories](../reference/files-and-directories.md).

## Two problems, two mechanisms

OMSI's cost of loading is made of two things, and the module has one mechanism for each:

| Problem | Mechanism | Component |
| --- | --- | --- |
| The files are not in memory when OMSI asks for them, and the first read of a cold file is slow. | **Prefetch into RAM**: read the files the session will use before OMSI asks for them, and keep them mapped in memory. | `Prefetch.Preloader.exe` (outside OMSI) |
| OMSI asks for its data a few bytes at a time, tens of millions of times. | **Read-ahead and write-behind**: answer those small reads from 64 KiB blocks and collect the small writes of the terrain cache in 256 KiB blocks. | `prefetch-native.dll` (inside OMSI) |

A third element ties them together: the native component reports to the session program what OMSI uses, so that the program learns, per map, which files to bring in next time and where on the map the player is now.

## The components and how they connect

```text
OmsiLaunch (controller, x64)
  │  starts before OMSI, writes the session context on stdin
  ▼
Prefetch.Preloader.exe (x64, session program)            Omsi.exe (x86)
  holds files in RAM                                        ├─ OmsiLaunch plugin + bridge
  reads the map, the tiles, the vehicles                    ├─ prefetch-native.dll   (hooks on kernel32 / d3dx9 imports)
  creates the session pipe  ◄──── U / T / W / X lines ──────┤       read-ahead, write-behind, read-only open
  saves learned data                                        └─ Prefetch.Module.dll   (events: armed, gameplay-ready, stopping)
```

| Link | What travels |
| --- | --- |
| Controller to preloader, standard input | The session context (the first line), then `omsi-started` with OMSI's process id, then `stop` (the end of the input is a stop too). |
| Preloader to controller, standard output | `ready`, events (`prepared`, `session`) and, if the preparation fails, an `error` line. |
| Native component to preloader, the session pipe | Use reports, one UTF-8 line each: `U` first use of a file, `T` an open of a `tile_X_Y.map`, `W` the game wrote to a file, `X` OMSI cannot replace a file that is held (release it now). |
| Components to OmsiLaunch, events | `module.omsi-prefetch.*` runtime events ([events](../reference/events-and-notices.md)). |

The pipe is `\\.\pipe\OmsiLaunch.Module.<session id without dashes>.omsi-prefetch`. The preloader creates it, restricted to the current user, **before** it reports ready, and accepts only OMSI's own process ([safety](safety.md)).

## A session, in order

| Step | Where | What happens |
| --- | --- | --- |
| 1 | OmsiLaunch | The plan includes `omsi-prefetch` (activation `default`) with its effective settings. |
| 2 | Preloader | Starts; reads the context; opens its log; computes the memory budget from the memory free at that moment ([memory and budget](memory-and-budget.md)). |
| 3 | Preloader | Creates the session pipe. |
| 4 | Preloader | Reads the map's `global.cfg`: tiles, entrypoints, files the map cites. Opens the map's data folder, removes the least recently used maps' data if the folder exceeds `data_limit_mb`, loads the learned list, the written list and the tile index. |
| 5 | Preloader | **Prepare**: holds files in this order until the budget, five minutes or low free memory stops it: (1) what earlier sessions of the map used, in first-use order; (2) the map-wide files; (3) the tiles nearest the start (radius 2, or `tile_radius` if smaller); (4) the map's AI vehicles; (5) more tiles around the start, up to `tile_radius`. |
| 6 | Preloader | Emits `prepared`, writes `ready`. |
| 7 | OmsiLaunch | Starts `Omsi.exe`, then tells the preloader its process id (`omsi-started`). |
| 8 | OMSI | The bridge loads `prefetch-native.dll`, which reads its settings, requests the hooks, starts the feed to the pipe and emits `hooks`; then `Prefetch.Module.dll` is armed (`armed`). |
| 9 | Preloader | **Session**: five background activities start ([session program](session-program.md)): the tile survey, the position prediction, the user vehicles, the pre-open and the memory guard; the native feed is served; learned data is saved every two minutes. |
| 10 | OMSI | Loads the world. Its small reads are served from blocks, its terrain-cache writes are collected, the files it needs are already in memory. |
| 11 | OMSI | Exits. The native component drains its buffers and emits `stats`; the managed component emits `stopping`. |
| 12 | OmsiLaunch, preloader | OmsiLaunch writes `stop` to the preloader. It stops its activities, saves the learned data, emits `session`, releases every held file and exits. OmsiLaunch then restores its own session files. |

## What is held, and why in a separate process

The files are held by the preloader, a separate 64-bit process, not by OMSI. OMSI is a 32-bit process with at most a few gigabytes of address space, which it needs for its own world; a 64-bit helper can hold up to 12 GB without taking any of it. Each file is mapped read-only and every page is touched, so the pages are really in memory and belong to the preloader. The pages stay resident while the game reads the same files through its own opens. The file handle is closed at once, so OMSI's own opens never meet a sharing conflict. See [memory and budget](memory-and-budget.md).

## What the module learns and keeps

For every map the module keeps a short list of the files the game used (in first-use order), a list of the files the game wrote, and an index of the files each tile needs. The second session of a map starts from what you really use instead of from a guess ([learned data](learned-data.md)). All of it lives in `.omsilaunch\module-data\omsi-prefetch\`; nothing is written into OMSI's own folders ([safety](safety.md), [files and directories](../reference/files-and-directories.md)).

## Degradation

The module is built to fail small. Each of its parts can fail without taking the session down, and the failure policy of the module is `degrade` ([errors and degradation](../reference/errors-and-degradation.md)):

- no memory to hold anything (the automatic budget is zero): nothing is held, but the native read-ahead and write-behind and the learning still work;
- no map in the session, or a map that cannot be read: nothing is held and nothing is learned (the data is kept per map), but the native read-ahead and write-behind still work;
- the session program cannot be reached from OMSI for ten seconds: the native component stops reporting; read-ahead and write-behind keep working;
- a hook cannot be installed: the features that depend on it stay off and OMSI runs as without the module.

## Next

[Session program](session-program.md) · [Native shim](native-shim.md) · [Learned data](learned-data.md)
