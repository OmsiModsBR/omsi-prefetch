# Measuring the gain

This page shows how to compare OMSI loading with and without Omsi Prefetch on your own machine, which numbers the module gives you, and which pitfalls make a comparison wrong. The published results come from one machine and one map (n = 1) and are listed in the [overview](overview.md#what-you-get); yours will differ with the hardware, the map and the memory you give the module.

Related pages: [overview](overview.md), [first run](first-run.md), [diagnosing slowness](../guides/diagnosing-slowness.md), [log format](../reference/log-format.md), [events](../reference/events-and-notices.md), [turning it off](../guides/turning-off.md).

## What to compare

Compare the same start (same map, same entrypoint or saved situation, same vehicle) under two conditions:

| Condition | How to start it |
| --- | --- |
| Without the module | Add `/no-module:omsi-prefetch` to the command line. |
| With the module | The same command line without that flag (the module is on by default). |

Measure from the moment you start the command to the moment the game is usable. Take two times, because the module moves part of the work in front of OMSI:

| Interval | Where it ends |
| --- | --- |
| Start of the command until the OMSI window appears | The module's preparation is in this interval. It is longer with the module. |
| OMSI window until the game is usable | Loading proper. This is where the module saves most. A simple definition: the first 60 seconds without a freeze of one second or more, which is the one the study used. |

The total of both is what you wait. The first indicator of the study's table (same optimized map, plugin observing versus acting) is counted from "Loading Situation"; keep both intervals in your own notes so that you can compare like with like.

## Rules for a fair comparison

1. **Learn first.** The first session of a map is a learning session ([first run](first-run.md)). Discard it and compare later sessions.
2. **Empty the file cache for the runs you compare.** After a run, Windows keeps the files in its own cache, and the next run is fast even without the module. The study emptied the operating system's file cache before the run with the mechanisms on. Restarting Windows does that; so does emptying the standby list with a tool such as Sysinternals RAMMap. Use the same method before every timed run.
3. **Repeat.** Do at least three runs per condition and compare the medians.
4. **Change one thing at a time.** Do not change settings between the two conditions; changing a setting between runs of the same condition is a different comparison ([guide by memory size](../guides/choosing-settings-by-memory.md)).
5. **Close other programs** that compete for memory. The budget is computed from the free memory at the start ([memory and budget](../concepts/memory-and-budget.md)); a different amount of free memory is a different budget. The log line `budget ... free ...` records it.

## What the module tells you

### The log

The log of each session ([log format](../reference/log-format.md)) has the numbers that explain a result:

| Line | What it tells you |
| --- | --- |
| `budget <size> (setting auto, ...), free <n> MB of <n> MB` | The memory the module was allowed to hold, and how much was free. |
| `prepare: <step>: <n> files, <size>` | How much each step held (`learned`, `map`, `start tiles`, `tiles around the start`). |
| `prepared in <s> s: <n> files, <size> of <size>` | How long the preparation took and why it ended (`budget reached`, `free memory low`, `time limit reached`, or nothing special when everything fit). |
| `end: <n> files held (<size>), used <n> of them (<size>), <n> used outside memory (<size>); feed <n> lines` | The result of the session: how many held files the game really used, and how many files it used that memory did not hold. |

A good session has many files in "used" and few "used outside memory". Many held files that were never used mean the budget is spent on the wrong things (lower `tile_radius`, a smaller vehicle scope). Many files used outside memory mean the budget was too small or the learned list is still thin (run the map again, or raise the budget).

### The events

While the session runs, `OmsiLaunch.exe events read` (or `events watch`) shows the module's events. Two carry numbers ([events](../reference/events-and-notices.md)):

- `module.omsi-prefetch.prepared`: `held_mb`, `budget_mb`, `files`, `ms` (the preparation time) and `first_open`;
- `module.omsi-prefetch.stats`, emitted when the module stops with a normal exit of OMSI: `app_reads` (the `ReadFile` calls OMSI made through the module) against `real_reads` (the ones that reached the operating system), `served_from_buffer`, and the write-behind counters. The ratio `app_reads` / `real_reads` is the reduction in system calls. To capture the event, run `events watch` in a second console before you leave OMSI. A session ended with `session stop` or the tray terminates OMSI without a normal exit, and this event is not emitted ([safety](../concepts/safety.md#what-happens-when-something-goes-wrong)).

### The monitor window

Start the session with `/prefetch-monitor` to watch the module live: how much is held against the budget, which files the game uses, and which files it used that memory did not hold. The window costs a little CPU; do not use it for timed runs.

### Estimating a map before you play it

The repository's test program can read an installation and report what the module would see on a map, without holding or writing anything: `Prefetch.Tests.exe --survey <OMSI folder> <maps\<map>\global.cfg>` prints the tile count, the size of the tile index, the AI vehicles and the user vehicles, with timings. It is built by `tools\Build-Modules.ps1` and is a developer tool; it is not in the module folder. Berlin-Spandau of the clean installation reads as 369 tiles, 4,664 files and 597 MB.

## Reading the result

Expect the biggest gains in the loading phase after the world starts and in the freezes while tiles load (the study: `ReadFile` time and the "Loading Tile" and "Updating Terrain" steps), and no change in frame rate. If the loading time after OMSI opens did not improve, read [diagnosing slowness](../guides/diagnosing-slowness.md): the usual causes are a budget that is too small for the machine's free memory, a map whose learned list does not exist yet, and another plugin that conflicts.

## Next

[Choosing settings by memory size](../guides/choosing-settings-by-memory.md) · [Diagnosing slowness](../guides/diagnosing-slowness.md) · [Log format](../reference/log-format.md)
