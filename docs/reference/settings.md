# Settings reference

Omsi Prefetch declares 29 settings in its manifest. This page lists every key with its type, default, bounds and flag, then describes what each does, how it interacts with the others and shows an example. The summary table is generated from `prefetch\module.template.json` (the source of the packaged `module.json`) and checked against it by `tools\Check-Docs.ps1`; the behavior described was verified against the code of the session program (`prefetch\preloader\Protocol.cs` reads and clamps every value) and of the native component (`prefetch\native\Module.cpp`).

Stability: the module is `1.0.0-beta.1`; the runtime-module feature of OmsiLaunch is `EXPERIMENTAL`. Related pages: the OmsiLaunch Runtime Modules reference (`docs\reference\runtime-modules.md` of OmsiLaunch, offline at `<OMSI root>\.omsilaunch\docs\reference\runtime-modules.md`: settings, the settings file, precedence), [choosing settings by memory size](../guides/choosing-settings-by-memory.md), [command-line overrides](../guides/command-line-overrides.md), [session profiles](../guides/session-profiles.md), [concepts: memory and budget](../concepts/memory-and-budget.md), [vehicle scopes](../concepts/vehicle-scopes.md), [pre-open](../concepts/pre-open.md).

## How settings reach the module

The module has no ini file of its own. Every setting reaches it through OmsiLaunch, which validates it against the manifest and refuses a value outside its bounds. In order of precedence, the effective value of a setting is:

1. the request: the module's own flag (`/prefetch-ram:6144`), `/module-setting:omsi-prefetch.<key>=<value>`, `LaunchSpec.Modules.Settings` (`"omsi-prefetch.<key>": "<value>"`) or `modules.omsi-prefetch.settings` of the selected session-profile preset. A key the preset sets cannot also be set on the command line (`OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT`);
2. the `[omsi-prefetch]` section of `<OMSI root>\.omsilaunch\modules.ini` (`key=value`, one per line; comment lines start with `;` or `#`; a comment cannot follow a value);
3. the manifest default in the table below.

Values are normalized by OmsiLaunch: booleans accept `true`/`false`, `yes`/`no`, `on`/`off`, `1`/`0`; choices are matched without regard to case; integers are checked against their bounds; text has no control characters and a length limit. An invalid requested value, or a key the module does not declare, makes the plan not runnable (`OL_E_MODULE_SETTING_INVALID`); an unusable entry in `modules.ini` is ignored with `OL_W_MODULE_SETTING_IGNORED`. `/list:RuntimeModules` shows each setting with its type, default and flag. The module itself clamps numbers a second time to the bounds below (it cannot receive a value outside them from OmsiLaunch).

## Summary

| Key | Type | Default | Range or choices | Flag |
| --- | --- | --- | --- | --- |
| `ram_budget_mb` | integer | `auto` | 256 to 12288 or `auto` | `/prefetch-ram` |
| `monitor` | boolean | `false` | `true` or `false` | `/prefetch-monitor` |
| `reserve_mb` | integer | `4096` | 0 to 65536 | `/prefetch-reserve` |
| `stop_below_free_mb` | integer | `auto` | 256 to 65536 or `auto` | `/prefetch-stop-below` |
| `vehicle_budget_mb` | integer | `0` | 0 to 65536 | `/prefetch-vehicle-ram` |
| `read_ahead` | boolean | `true` | `true` or `false` |  |
| `write_behind` | boolean | `true` | `true` or `false` |  |
| `read_only_open` | boolean | `true` | `true` or `false` |  |
| `read_only_folders` | text | `vehicles\` | up to 512 characters |  |
| `tile_radius` | integer | `12` | 0 to 64 |  |
| `ai_vehicles` | choice | `complete` | `off`, `essential`, `load`, `complete` |  |
| `user_vehicles` | choice | `none` | `none`, `list`, `all` |  |
| `user_vehicle_list` | text | (empty) | up to 4096 characters |  |
| `user_vehicle_scope` | choice | `essential` | `essential`, `load`, `complete` |  |
| `fleet_scope` | choice | `load` | `essential`, `load`, `complete` |  |
| `sounds` | boolean | `true` | `true` or `false` |  |
| `pre_open` | choice | `learned` | `none`, `learned`, `all`, `list` | `/prefetch-pre-open` |
| `pre_open_scope` | choice | `load` | `essential`, `load`, `complete` |  |
| `pre_open_list` | text | (empty) | up to 4096 characters |  |
| `pre_open_max_mb` | integer | `2048` | 16 to 65536 |  |
| `pre_open_threads` | integer | `2` | 1 to 16 |  |
| `pre_open_first_use_max_mb` | integer | `400` | 16 to 65536 |  |
| `pre_open_first_use_threads` | integer | `4` | 1 to 16 |  |
| `pre_open_on_first_use` | boolean | `true` | `true` or `false` |  |
| `learn` | boolean | `true` | `true` or `false` |  |
| `survey` | boolean | `true` | `true` or `false` |  |
| `first_open_notice` | boolean | `true` | `true` or `false` |  |
| `debug_log` | boolean | `false` | `true` or `false` | `/prefetch-debug` |
| `data_limit_mb` | integer | `256` | 16 to 4096 |  |

A flag is written `/<flag>:<value>`; a boolean's flag alone (`/prefetch-monitor`) means `true`. `enabled` is not a setting of the module: OmsiLaunch switches a module on or off for every module the same way ([turning it off](../guides/turning-off.md)).

In the examples, the command-line form is for one session; the same key and value work in `modules.ini` (`key=value` under `[omsi-prefetch]`) and in a preset (`modules.omsi-prefetch.settings`). Examples with single quotes are for PowerShell, where an argument that contains `;` must be quoted; in `cmd.exe` use double quotes around the whole argument instead.

## Memory

### `ram_budget_mb`

The memory the preloader may hold, in MB. `auto` (default) is 70 % of the physical memory free at the start, after keeping `reserve_mb` for OMSI, at most 12,288 MB; a number from 256 to 12,288 is used as it is. The 12 GB ceiling is the same for both. Interactions: `reserve_mb` is used only with `auto`; with a number, `stop_below_free_mb` defaults to 1,024 MB instead of 2,048 MB. The explicit budget is not reduced when little memory is free; the loading floor stops loading instead ([memory and budget](../concepts/memory-and-budget.md)). If less than `reserve_mb` is free with `auto`, the budget is zero and nothing is held.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-ram:6144
```

### `reserve_mb`

Memory kept for OMSI and the system when `ram_budget_mb` is `auto`, in MB (0 to 65,536, default 4,096). The automatic budget is `0.7 x (free - reserve_mb)`. Ignored when `ram_budget_mb` is a number. Raise it on a machine that runs other programs next to OMSI; lowering it below the default gives the module more of a small machine's memory and leaves OMSI less.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-reserve:6144
```

### `stop_below_free_mb`

Loading stops when the free physical memory is below this value, in MB (256 to 65,536). `auto` (default) is 2,048 with the automatic budget and 1,024 with an explicit `ram_budget_mb`. The floor stops the preparation, the tile prediction, the user vehicles and the pre-open; it does not release what is already held (the guard does that, at a fixed 1,024 MB: [memory and budget](../concepts/memory-and-budget.md#the-memory-guard)).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-stop-below:3072
```

### `vehicle_budget_mb`

Cap, in MB, of what the map's AI vehicles may hold and, separately, of what the user's vehicles may hold (0 to 65,536, default 0). 0 means only `ram_budget_mb` limits them. A vehicle that does not fit the cap is skipped whole. It does not limit the tiles, the map-wide files, the learned files or the pre-open.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-vehicle-ram:1024
```

## The native features

These three switches act inside OMSI. They start only when all the hooks they need are installed ([native shim](../concepts/native-shim.md)). Turning all three off leaves only the use reports.

### `read_ahead`

Default `true`. Serves OMSI's byte-by-byte reads from 64 KiB blocks. Switch off to rule it out when diagnosing; there is no reason to switch it off otherwise.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.read_ahead=false
```

### `write_behind`

Default `true`. Collects the byte-by-byte writes of the terrain cache (`.rdy`) in 256 KiB blocks. With it on, terminating OMSI loses the bytes still buffered; the file is a regenerable cache ([safety](../concepts/safety.md)). Switch it off if you routinely end sessions by terminating OMSI and prefer to keep every `.rdy` byte (you give up the gain in `WriteFile` time).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.write_behind=false
```

### `read_only_open`

Default `true`. Opens assets below `read_only_folders` without write access, which spares a synchronous metadata update when the handle is closed. If OMSI writes to such a handle after all, the file is reopened with the access it asked for.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.read_only_open=false
```

### `read_only_folders`

Folders, relative to the OMSI folder and separated by `;`, whose assets are opened read-only (text, up to 512 characters, default `vehicles\`). Entries are trimmed, matched without regard to case, `/` is read as `\` and a trailing `\` is added when missing. A path matches when it starts with the folder or contains it as a segment (so absolute paths match too). Only the asset extensions listed in [native shim](../concepts/native-shim.md#read-only-open) are downgraded. An empty value is read as the default. Only effective with `read_only_open` on.

```ini
[omsi-prefetch]
read_only_folders=vehicles\;sceneryobjects\
```

Do not end a quoted command-line value with `\` (Windows argument parsing turns `\"` into a quote); set this key in `modules.ini` or a preset.

## Tiles and learning

### `tile_radius`

How far around the start and around the player's estimated position tiles are held, in tiles (0 to 64, default 12). It is a square: a tile is included when it is within the radius on both axes, so 12 means a 25 by 25 block. Before OMSI, the nearest ring (radius 2, or `tile_radius` if smaller) is held first, then the rest up to the radius, after the AI vehicles. During the session the prediction holds the tiles around the estimated position, nearest first, up to the radius. 0 holds only the tile itself. Needs `survey`.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.tile_radius=6
```

### `learn`

Default `true`. Remembers what each session of a map used (the learned list) and what it wrote (the written list), to bring the first in before the next session. With `false` the lists are still read and used, but not updated and not saved. Interaction: with `learn` and `survey` both off, the module never saves anything about a map, every session counts as a first open, and the first-open window shows every time (unless `first_open_notice` is off).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.learn=false
```

### `survey`

Default `true`. Reads the map's tiles to know what each one needs (the tile index): it lets the module hold the tiles near the start before OMSI and the tiles around the player while it runs. With `false` no tile index is loaded, computed or saved, and the module holds no tile files (neither before OMSI nor during the session); `tile_radius` has no effect. The learned list still brings in the tiles you used before.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.survey=false
```

## Vehicles

### `ai_vehicles`

What of the map's AI vehicles is held (default `complete`): `off`, `essential`, `load` or `complete` ([vehicle scopes](../concepts/vehicle-scopes.md)). `off` also leaves out the objects and people the map's AI places. Held in the preparation, before OMSI starts.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.ai_vehicles=load
```

### `user_vehicles`

Which of the user's vehicles are held while OMSI runs (default `none`): `none`, `list` (the folders of `user_vehicle_list`) or `all` (every valid vehicle folder the map's AI does not use). With `ai_vehicles` set to `off`, `all` includes the vehicles the map would have used.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.user_vehicles=all
```

### `user_vehicle_list`

Vehicle folders held with `user_vehicles=list`, separated by `;`, in order (text, up to 4096 characters, default empty). Names are folder names directly under `Vehicles\`, matched without regard to case. A name that is not a valid vehicle folder, or that is a vehicle of the map's AI, is skipped and the log says so. Only used with `user_vehicles=list`.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.user_vehicles=list '/module-setting:omsi-prefetch.user_vehicle_list=MAN_Lions_City;Citaro_G'
```

### `user_vehicle_scope`

What of each user vehicle is held (default `essential`): `essential`, `load` or `complete`. Used for the vehicles chosen by `user_vehicles`, except those that carry the map's timetable (see `fleet_scope`).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.user_vehicle_scope=load
```

### `fleet_scope`

What of each user vehicle that carries the map's timetable (a `.hof` file named after the map's folder, in the vehicle's own folder) is held (default `load`). Those vehicles are the map's own fleet and are held first among the user's vehicles. Only matters with `user_vehicles` other than `none`.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.fleet_scope=complete
```

### `sounds`

Default `true`. Includes the `.wav` files that vehicles and objects cite (and, with the `complete` scope, the `.wav` files of a folder). `.ogg` and other audio files are not followed from citations; the `complete` scope holds them as part of the folder whatever `sounds` says. Switch off to save memory when sounds load fast enough from disk.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.sounds=false
```

## Pre-open

### `pre_open`

Vehicle files opened ahead of the game without being held (default `learned`): `none`, `learned` (the vehicle files earlier sessions used), `all` (every valid user vehicle, with `pre_open_scope`) or `list` (the folders of `pre_open_list`). Flag `/prefetch-pre-open:<mode>`. See [pre-open](../concepts/pre-open.md).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-pre-open:none
```

### `pre_open_scope`

What of each vehicle is opened ahead with `pre_open` `all` or `list` (default `load`): `essential`, `load` or `complete`. Ignored with `none` and `learned`.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-pre-open:all /module-setting:omsi-prefetch.pre_open_scope=essential
```

### `pre_open_list`

Vehicle folders opened ahead with `pre_open=list`, separated by `;` (text, up to 4096 characters, default empty). A folder that is a vehicle of the map's AI is skipped.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-pre-open:list '/module-setting:omsi-prefetch.pre_open_list=MAN_Lions_City;Citaro_G'
```

### `pre_open_max_mb`

Cold data, in MB, after which the planned pre-open stops (16 to 65,536, default 2048). Cold data is the size of the files whose open took 1.5 ms or more.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.pre_open_max_mb=1024
```

### `pre_open_threads`

Threads of the planned pre-open (1 to 16, default 2). They run with background I/O priority.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.pre_open_threads=4
```

### `pre_open_on_first_use`

Default `true`. When a vehicle's preview starts (the first use of its `.bus` or `.ovh`), opens its folder ahead of the game, once per folder and session. Skipped for the map's AI vehicles and when 90 % of the folder is already held.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.pre_open_on_first_use=false
```

### `pre_open_first_use_max_mb`

Cold data, in MB, after which the opening of a vehicle's folder on its first use stops (16 to 65,536, default 400). Applied per folder.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.pre_open_first_use_max_mb=800
```

### `pre_open_first_use_threads`

Threads that open a vehicle's folder on its first use (1 to 16, default 4).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.pre_open_first_use_threads=2
```

## Windows and diagnostics

### `monitor`

Default `false`. Shows the window that follows what is held in memory. It opens only when OmsiLaunch asks for it: the flag `/prefetch-monitor`, `/module-setting:omsi-prefetch.monitor=true`, `monitor=true` in `modules.ini` or a preset. Closing the window hides it until the session ends ([session program](../concepts/session-program.md#windows)).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-monitor
```

### `first_open_notice`

Default `true`. Shows the small window that explains the wait on the first open of a map ([first run](../getting-started/first-run.md#the-first-open-window)). With `false` there is no window; the wait is the same.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.first_open_notice=false
```

### `debug_log`

Default `false`. Writes the per-file and per-vehicle lines in the log: vehicles held or skipped, files released at OMSI's request, guard releases, first-use pre-opens ([log format](log-format.md)). The normal log is a few dozen lines per session, at most 1 MiB per file, three files kept; the extra lines count against the same 1 MiB limit. Flag `/prefetch-debug`.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-debug
```

## Data

### `data_limit_mb`

Limit, in MB, of the module's data, meaning what each map taught it (16 to 4096, default 256). When a session starts and the maps' data is larger, the maps used least recently are removed first, never the current one. The logs are bounded separately and are not counted ([learned data](../concepts/learned-data.md#bounds-and-clean-up)).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.data_limit_mb=64
```

## Related

[Command-line overrides](../guides/command-line-overrides.md) · [Session profiles](../guides/session-profiles.md) · [Choosing settings by memory size](../guides/choosing-settings-by-memory.md) · [Files and directories](files-and-directories.md)
