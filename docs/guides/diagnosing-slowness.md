# Diagnosing slowness

This guide helps you find out why a session is slow or why Omsi Prefetch did not help, using what the module itself reports: the plan, the events, the log and the monitor window. It starts with the cheapest checks. Where the log is read, the message formats are in [log format](../reference/log-format.md).

Related pages: [log format](../reference/log-format.md), [events](../reference/events-and-notices.md), [errors and degradation](../reference/errors-and-degradation.md), [choosing settings by memory size](choosing-settings-by-memory.md), [measuring the gain](../getting-started/measuring-the-gain.md).

## 1. Is the module in the session?

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /plan --json
```

Look in `Diagnostics`:

| You see | Meaning |
| --- | --- |
| `module.planned` for `omsi-prefetch` | It will take part. |
| `module.disabled` | You, a preset or `modules.ini` switched it off ([turning it off](turning-off.md)). |
| `OL_W_MODULE_NOT_LOADED` | It cannot be loaded; its `Data["code"]` says why (`OL_E_MODULE_INTEGRITY`, `OL_E_MODULE_CONFLICT`, ...). |
| Nothing about `omsi-prefetch` | It is not installed where OmsiLaunch looks: `/list:RuntimeModules` ([installation](../getting-started/installation.md)). |

## 2. Read the log

Open the newest `preloader-*.log` in `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\logs\`. Five lines tell most of the story:

| Line | What to check |
| --- | --- |
| `budget <size> (setting auto), free <MB> MB of <MB> MB` | Is the budget what you expected? `0.0 MB` means the automatic budget is zero: less than 4,096 MB was free. Set `ram_budget_mb` ([choosing settings by memory size](choosing-settings-by-memory.md)). |
| `map ...; learned <n> files; tile index ... ; first open` | `first open` means nothing is learned yet: this session teaches the module, the next one is the one to measure. `learned 0 files` without `first open` means the tile index exists but nothing was learned (`learn` off, or the map has not been used long enough). |
| `prepare: ...` steps | Which step held how much. A step with `0 files` is suspicious: `learned: 0 files` on a known map, or `start tiles (0 tiles)`, means the map's tiles or start were not found. |
| `prepared in <s> s: ... [reason]` | How long the preparation took and why it ended. `(budget reached)`, `(free memory low)` and `(time limit reached ...)` are explained in [log format](../reference/log-format.md). |
| `end: <n> files held (...), used <n> of them (...), <n> used outside memory (...)` | The hit result. Many "used outside memory" means the files OMSI needed were not in memory. |

If the log has no `prepare` lines or says `no map in this session` or `the map ... cannot be read`, the module had nothing to prepare: check the map identity of the session.

## 3. Common causes

| Symptom | Likely cause | What to do |
| --- | --- | --- |
| No gain on the first session of a map | The module learns during that session. | Run the map again and compare the second session ([first run](../getting-started/first-run.md)). |
| No gain, `budget 0.0 MB` | Less than `reserve_mb` free at the start (8 GB machines). | Set an explicit `ram_budget_mb`; close programs before the session. |
| No gain, many files `used outside memory`, `(budget reached)` | The budget is smaller than what you use. | Raise `ram_budget_mb`; reduce `ai_vehicles` or `tile_radius` to spend it better. |
| No gain, most held files unused | The budget is spent on files you do not use. | Lower `tile_radius`, `ai_vehicles`, the vehicle scopes. |
| OMSI opens very late | A large first open, a slow disk, or a `complete` AI scope on a huge map. | Look at the `prepared in` time; lower `ai_vehicles` to `load` or `essential`; the limit for loading is five minutes. |
| Stutters while loading tiles, but the log is clean | The tile prediction does not hold the tiles you drive into: the budget was full when you moved, or `survey` is off. | Check `survey`; raise the budget; lower `tile_radius`. |
| Windows pages or the system is sluggish | Too much is held for the machine. | Lower `ram_budget_mb`; raise `stop_below_free_mb`; close other programs. The guard releases unused files below 1,024 MB free ([memory and budget](../concepts/memory-and-budget.md)). |
| The log says `the working set minimum was not raised` | Windows refused the request to keep pages resident. | Not an error; held pages may be trimmed under pressure. Lower the budget if you see paging. |
| `feed: OMSI connected` never appears | The native component did not connect (the module is not armed in OMSI, a hook was refused, or the program had no pipe). | Read the `hooks` event (`OmsiLaunch.exe events read`): a value other than `0` for `create`, `read` or `close` means the native phase failed ([errors and degradation](../reference/errors-and-degradation.md)). |
| `host.failed` / `OL_E_MODULE_LOAD_FAILED` | The session program did not start or exited before it was ready. | Check that the .NET 6 Desktop Runtime (x64) is installed; reinstall the module folder. |
| `OL_W_MODULE_NOT_LOADED` | A conflict, integrity or protocol problem. | See [coexisting](coexisting.md) and [errors and degradation](../reference/errors-and-degradation.md). |
| The first-open window appears every time | `learn` and `survey` are both off, or the data is cleared on every start. | Turn one back on; check what clears `.omsilaunch\module-data`. |

## 4. Turn on the detail

For one session, add `/prefetch-debug`:

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-debug
```

The log then also says, per vehicle, `vehicles: held <folder> (<scope>): <size>`, `vehicles: over the budget: <folder> needs <size>` (a vehicle that did not fit), `released at OMSI's request: <path>`, `guard: ... released ...` and the result of each first-use pre-open. The detail counts against the same 1 MiB limit of the file; if the log says `-- log limit reached`, later lines are lost.

## 5. Watch it live

Add `/prefetch-monitor` to see the monitor window: the memory held against the budget, the phase (reading the map, loading before OMSI, following the session), the memory of the system, the held bytes by source (learned, map, tiles, AI, user), the estimated position, the number of tiles indexed, whether OMSI is connected, and the list of files. The filter "Show: used outside memory" lists the files that OMSI used and the module did not hold (orange rows): they are what to look at when the log's `used outside memory` is large. "Show: in memory, unused" lists what the budget holds that OMSI has not used. Do not use the monitor for timed runs.

## 6. Rule out a mechanism

If the session misbehaves, switch off one native mechanism at a time with `/module-setting:omsi-prefetch.read_ahead=false`, `write_behind=false` or `read_only_open=false`, or the whole module with `/no-module:omsi-prefetch`, and compare ([turning it off](turning-off.md)). If a problem disappears only without the module, keep the log and the `hooks` and `stats` events and report it with the OmsiLaunch version, the module version (`1.0.0-beta.1`) and the map.

## Next

[Coexisting with other modules and plugins](coexisting.md) · [Errors and degradation](../reference/errors-and-degradation.md) · [Log format](../reference/log-format.md)
