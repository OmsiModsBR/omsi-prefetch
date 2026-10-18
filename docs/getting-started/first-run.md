# First run

This page walks through the first session of a map with Omsi Prefetch installed: what the module does before OMSI opens, what the first-open window means, what changes on the second run, and where to look afterwards. It assumes the module is installed and visible to OmsiLaunch as described in [installation](installation.md). You start sessions exactly as before (the module needs no flag); nothing on this page changes the command line.

Related pages: [installation](installation.md), [measuring the gain](measuring-the-gain.md), [how prefetching works](../concepts/how-it-works.md), [session program](../concepts/session-program.md), [learned data](../concepts/learned-data.md), [log format](../reference/log-format.md).

## What happens when you start a session

Start any session as usual, for example:

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1
```

The module receives the session's map from OmsiLaunch for every way of starting (a new map, the last state of a map, or a saved situation: OmsiLaunch resolves the situation's map). In order:

1. OmsiLaunch plans the session and the module joins it (activation `default`).
2. OmsiLaunch starts `Prefetch.Preloader.exe` **before** OMSI and gives it the session context. The module's log file is opened and the first lines are written (session id, budget, free memory).
3. The preloader reads the map's `global.cfg`, loads what it learned about this map in earlier sessions, and starts to hold files in RAM, in this order: what earlier sessions used (first-use order), the map-wide files, the tiles nearest the start, the map's AI vehicles, more tiles around the start. It stops at the memory budget, at five minutes of loading, or when free memory runs low, whichever comes first ([memory and budget](../concepts/memory-and-budget.md)).
4. The preloader reports that it is ready. Only then does OmsiLaunch start `Omsi.exe`. The session's startup timeout counts from the moment OMSI is started, so the preparation does not consume it.
5. OMSI loads the native component and the managed component. From here on the preloader follows the session ([session program](../concepts/session-program.md)).
6. When OMSI exits, OmsiLaunch asks the preloader to stop; it saves what it learned, releases the memory and exits.

OMSI therefore opens a little later than without the module. That wait is the price of having the files already in memory when OMSI asks for them, and it is repaid during loading ([measuring the gain](measuring-the-gain.md)).

## The first-open window

The first time a map is requested the module knows nothing about it: no list of the files earlier sessions used, no index of which files each tile needs. Unless the setting `first_open_notice` is off, the preloader then shows a small window of its own until OMSI is started. The OmsiLaunch tray indicator is not shown yet at that point, which is why the module uses a window of its own.

| Element | Meaning |
| --- | --- |
| Title bar `Omsi Prefetch`; heading `Preloading is being set up` | The module is preparing this map. |
| Text: the map was requested for the first time; the module is reading the map and bringing as much as it can into memory before OMSI opens; in this first session it also learns what the game uses; the next starts will be faster | This is a first open. Nothing is wrong when the wait is longer than usual. |
| The map name | The friendly name from the map's `global.cfg` (the folder name when the map has none). |
| `Preparing: <size> in memory` and a moving bar | How much the module holds right now. It updates twice a second. |
| `Ready. Opening OMSI...` and a full bar | The preparation is finished and OMSI is being started. The window closes by itself about four seconds later. |

The window is always on top, sits in the bottom-right corner of the primary screen, never takes the keyboard focus, has an entry in the taskbar and can be minimized. It is informational: you do not have to do anything. Its text is Portuguese when the Windows display language is Portuguese (any `pt` variant) and English otherwise.

A map counts as "first open" when the module has neither a learned list of files for it nor a saved tile index that matches the map as it is now. That is the case on the very first session of a map, after `/clear-module-data:omsi-prefetch`, and permanently when both `learn` and `survey` are off (nothing is ever saved). A map whose `global.cfg` or tile files changed since the last session is not a first open as long as its learned list exists: only the tile index is discarded and rebuilt. On later sessions the window does not appear. The preloader reports the fact in the `prepared` event (`first_open`) and in the log line `... ; first open`.

How long the first open takes depends on the size of the map and on the disk. For scale, reading the tile index of Berlin-Spandau of the clean OMSI installation (369 tiles, 4,664 files, 597 MB) takes 14.4 s on a cold disk and 2.3 s on a warm one; holding the files is on top of that.

## What the first session teaches the module

During the session the native component reports every file OMSI uses. When the session ends the preloader writes, for this map only:

- `learned.txt`: the files the game used, in the order it first used them;
- `written.txt`: the files the game wrote, which are never held in memory;
- `tiles.idx`: for each tile, the files it needs (computed in the background while you play and saved when it grew).

It also saves them every two minutes during the session, so a crash of the session loses at most that much.

## The second run

On the next session of the same map the preloader finds the learned list and the tile index. The window does not appear, the files that you really used come first, and the start tiles are known immediately. The expected result is a shorter load. Run the same start two or three times before judging: the first session pays for learning, and the operating system's own file cache makes comparisons between close-together runs optimistic ([measuring the gain](measuring-the-gain.md)).

## Where to look afterwards

| What | Where |
| --- | --- |
| The module's log of the session | `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\logs\preloader-<yyyyMMdd-HHmmss>.log` (the three newest are kept). Read the lines `budget`, `prepared in` and `end:`. |
| What it learned about a map | `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\maps\<folder>-<hash>\` |
| Live view of what is held | Start the session with `/prefetch-monitor`: the monitor window shows the files held, in use and used outside memory. |
| Session events | `OmsiLaunch.exe events read` while the session runs ([events](../reference/events-and-notices.md)). |

If something looks wrong, go to [diagnosing slowness](../guides/diagnosing-slowness.md).

## Next

[Measuring the gain](measuring-the-gain.md) · [Choosing settings by memory size](../guides/choosing-settings-by-memory.md) · [Diagnosing slowness](../guides/diagnosing-slowness.md)
