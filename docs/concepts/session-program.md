# The session program

`Prefetch.Preloader.exe` is the module's session program: an x64 program that OmsiLaunch starts before OMSI, that holds files in memory for the session and that follows the session until it ends. This page describes its two phases (before OMSI and while OMSI runs), the activities that run in the second phase, the tile-position prediction, the memory guard, how it stops, and the limits that bound it. The general session-program protocol is specified in the OmsiLaunch Runtime Modules reference ("Session programs"); this page is about what this program does with it.

Related pages: [how prefetching works](how-it-works.md), [memory and budget](memory-and-budget.md), [learned data](learned-data.md), [vehicle scopes](vehicle-scopes.md), [pre-open](pre-open.md), [safety](safety.md), [log format](../reference/log-format.md).

## Start and context

OmsiLaunch starts the program as `Prefetch.Preloader.exe --omsilaunch-session <session id>` from the module folder, inside a job object that ends it with the session, and writes the context as the first line of its standard input. The program refuses to run by itself (started by hand, it shows a message box and exits with code 2) and exits with code 2 when the context is not a valid `omsilaunch.module-session/v1` context for the same session id.

From the context the program uses the installation root, the module's data directory, the session pipe name, the map of the session (`world.map`), the way the session starts (`world.mode`; for a new map also the presented entrypoint index), the effective settings, and the files the session transaction owns (`session_files`), which it never holds ([safety](safety.md)).

## Phase 1: before OMSI (prepare)

The program works in this order and reports `ready` at the end, only after which OmsiLaunch starts OMSI.

| Step | What it does |
| --- | --- |
| Budget | Reads the free physical memory, computes the budget ([memory and budget](memory-and-budget.md)) and tries to raise its working-set minimum to the budget plus 256 MB so that Windows keeps the held pages resident. If that is refused, the log says so and the pages may be trimmed under memory pressure. |
| Monitor | Opens the monitor window only when `monitor` is true. |
| Pipe | Creates the session pipe, so that OMSI's native component can connect as soon as it loads. |
| Map | Reads `global.cfg`. With no map in the session, or a map that cannot be read, it logs that and skips the rest (OMSI still starts). |
| Data | Opens the map's data folder, prunes old maps' data when the data exceeds `data_limit_mb`, loads the written list, the learned list and (with `survey`) the saved tile index. |
| Start tiles | Works out the tiles where the session starts: for a new map, the tile of the presented entrypoint (OMSI's presented order is not known offline, so the entrypoint of the file order and the one of the name order are both used); for a saved situation or the map's last state, the tiles of every entrypoint; with no entrypoint, the tile nearest the middle of the map. |
| First-open window | Shows it when this is a first open and `first_open_notice` is on ([first run](../getting-started/first-run.md)). |
| Hold | Holds files in this order: `learned`, `map`, `start tiles` (radius 2, or `tile_radius` when smaller; only with `survey`), the map's AI vehicles (unless `ai_vehicles` is `off`), `tiles around the start` (up to `tile_radius`; only with `survey`). Each step stops as soon as the budget is full, five minutes have passed since the first step started, or free memory fell below the floor. |
| Report | Logs `prepared in ...`, emits the `prepared` event and writes `ready`. |

If the preparation throws, the program logs the failure, sends one `error` line to OmsiLaunch (`OL_E_MODULE_OPERATION_FAILED`) and still writes `ready`: OMSI starts and the module degrades to whatever was already held. The time the controller allows for `ready` is the manifest's `prepare_ms`, 600,000 ms (ten minutes); the program's own loading limit is five minutes, so under normal conditions it never reaches that.

What each step holds is described in [learned data](learned-data.md) (the first), [vehicle scopes](vehicle-scopes.md) (the AI vehicles) and below (the map-wide files and the tiles).

### The map-wide files and the tiles

- **Map-wide files**: everything cited by `global.cfg` (ground textures, money system, ticket pack) and every file of the map's folder, recursively, except the tiles' own files (`tile_*`), the `chrono\` folder, `texture\map\`, work files and archives, and the files the game rewrites ([safety](safety.md#what-is-never-held)).
- **A tile**: its own files (`tile_X_Y.map` first, then its companions such as `.terrain`, `.water`, in the map folder and in `texture\map\`, never the `.rdy` cache and never the streamed files), then every object (`.sco`) and spline (`.sli`) the tile file places, each with everything it cites: models, textures (a texture's `.cfg` goes with it), scripts, sub-objects and, with `sounds`, the `.wav` files.
- **Distance**: a tile counts as "around" a point when it is within `tile_radius` tiles on both axes (a square, not a circle); they are held nearest first.

## Phase 2: while OMSI runs

When the controller reports OMSI's process id, the program starts these activities. They run on their own threads, below the priority of the game's own work where it matters.

| Activity | Priority | What it does | Needs |
| --- | --- | --- | --- |
| Survey | below normal | Computes the tile index of every tile not computed yet, nearest to the start first, and keeps it for saving. | `survey` and a readable map |
| Prediction | normal | Estimates where the player is and holds the tiles around that point ([below](#tile-prediction)). | `survey` and a readable map |
| User vehicles | below normal | Holds the user's vehicles chosen by `user_vehicles` ([vehicle scopes](vehicle-scopes.md#the-users-vehicles)). | `user_vehicles` other than `none` |
| Pre-open | below normal | Opens vehicle files ahead of the game without holding them ([pre-open](pre-open.md)). | `pre_open` other than `none` |
| Guard | normal | Gives memory back when the system runs low ([memory and budget](memory-and-budget.md#the-memory-guard)). | always |
| Feed | n/a | Serves the session pipe: receives the use reports and applies them. | the native component connected |
| First-use pre-open | below normal | One short thread per vehicle folder, started by the first use of its `.bus` or `.ovh` ([pre-open](pre-open.md#on-first-use-pre_open_on_first_use)). | `pre_open_on_first_use` |

Every `U` line marks a file as used for the first time and feeds the learned list. A `T` line (an open of a tile file) also feeds the prediction. A `W` line marks a file as written: it is released at once if held and is never held again. An `X` line does the same and is answered immediately, on the thread that reads the pipe, so that a burst of other lines can never delay it (OMSI is waiting while an `X` is pending, up to 1.5 s).

### Tile prediction

OMSI loads a window of tiles around the player, and new tiles appear at its leading edge. The program takes the tile files OMSI opened in the last minute (and at least the 20 most recent, if fewer than 20 were opened in that minute), averages their coordinates, and takes that centroid as the estimated position. It then holds the tiles around the centroid within `tile_radius`, nearest first. If the player moves while it is working, the next pass starts at once; if memory or the budget blocks it, it retries every five seconds; otherwise it waits for movement. The estimated position is shown in the monitor window.

The prediction never releases tiles it has already held because the player moved away; memory comes back only through the guard, the release of a file the game writes, and the end of the session ([known limitations](../reference/known-limitations.md)).

## The feed

The pipe accepts one client at a time and refuses a client that is not OMSI's process (the controller names the process; a connection that arrives before the name waits up to ten seconds for it). Lines are at most 4096 bytes, a tab separates the kind from the path, and only the kinds `U`, `T`, `W` and `X` are accepted; paths with control characters or outside the installation are ignored. The work queue holds 4096 lines; when it is full a `U` line (a hint) is dropped and counted, while `T` and `W` wait up to 50 ms. After OMSI disconnects, the pipe is created again.

## Saving

Every two minutes during the session, and once more at the end, the program writes the learned list and the written list (with `learn`) and the tile index (with `survey`, when it has grown since the last save) into the map's data folder, through a temporary file that replaces the old one ([learned data](learned-data.md)).

## Stopping

When the controller writes `stop` (or the standard input ends), the program cancels its activities, waits up to six seconds for them, saves, emits the `session` event, logs the `end:` line, closes the pipe and releases every held file, then exits. The controller allows `host_stop_ms` (20,000 ms in this module) for that and ends the program afterwards. If OmsiLaunch itself is killed, the job object ends the program; it does not get to save, and the last periodic save (at most two minutes old) is what remains.

## Windows

The program shows at most two windows, both on its own thread: the [first-open window](../getting-started/first-run.md#the-first-open-window) and the monitor window. The monitor opens only when OmsiLaunch's settings ask for it (`monitor`; flag `/prefetch-monitor`). It shows how much is held against the budget, the phase, the system memory, the held bytes by source (learned, map, tiles, AI, user), the estimated position, the number of tiles indexed, whether OMSI is connected, and a list of files with a filter (all, in use, in memory but unused, used outside memory) and an "Always on top" box. Green rows are files in use, orange rows were used outside memory, grey rows are held and unused. Closing it only hides it until the session ends. Both windows use Portuguese when the Windows display language is Portuguese and English otherwise.

## Limits that bound the program

| Limit | Value |
| --- | --- |
| Loading before OMSI | 5 minutes |
| Controller's wait for `ready` | `prepare_ms`, 600,000 ms |
| Controller's wait for the stop | `host_stop_ms`, 20,000 ms |
| Files in one closure (a vehicle, a scenery object) | 60,000 |
| Entries in the learned list | 200,000 |
| Entries listed in a map folder (map-wide) | 50,000 |
| Context line | 1 MiB; later lines at most 64 KiB |

## Next

[Native shim](native-shim.md) · [Memory and budget](memory-and-budget.md) · [Vehicle scopes](vehicle-scopes.md)
