# Choosing settings by memory size

The defaults are made for a machine with plenty of free memory. This guide gives starting values for machines with 8, 16, 32 and 64 GB of RAM, explains how the automatic budget behaves on each, and shows how to adjust from what the log tells you. The values are starting points derived from how the budget, the floors and the scopes work ([memory and budget](../concepts/memory-and-budget.md), [vehicle scopes](../concepts/vehicle-scopes.md)); the published gain was measured on one machine (n = 1), so adjust on yours ([measuring the gain](../getting-started/measuring-the-gain.md)).

Related pages: [settings reference](../reference/settings.md), [command-line overrides](command-line-overrides.md), [session profiles](session-profiles.md), [diagnosing slowness](diagnosing-slowness.md).

## What decides what you get

1. **The automatic budget** is `0.7 x (free memory - reserve_mb)` at the start, at most 12,288 MB, and zero when less than `reserve_mb` (4,096 MB) is free. It follows the *free* memory, not the installed memory: a browser and a video call open take their share before the module reads it.

   | Free memory at the start | Automatic budget |
   | ---: | ---: |
   | 4,000 MB or less | 0 |
   | 5,000 MB | 632 MB |
   | 8,000 MB | 2,732 MB |
   | 11,000 MB | 4,832 MB |
   | 20,000 MB | 11,132 MB |
   | 28,000 MB or more | 12,288 MB |

2. **An explicit `ram_budget_mb`** (256 to 12,288) ignores the free memory for its size and lowers the loading floor to 1,024 MB. It is what you want on a small machine, where the automatic budget is zero or tiny, and when you want the same amount every time.
3. **What you ask to be held** (the scope of the AI vehicles, the user's vehicles, `tile_radius`) decides how the budget is spent. A budget that is too small for the scopes is spent in order, so the vehicles at the end of the order (the user's) are the ones that miss ([memory and budget](../concepts/memory-and-budget.md#where-the-budget-goes-first)).

OMSI is a 32-bit program and uses a few gigabytes at most; the module keeps `reserve_mb` free for it and for Windows. On any machine, close memory-hungry programs before starting a session and leave at least 4 GB of free memory after the budget.

## Starting values

| | 8 GB | 16 GB | 32 GB | 64 GB |
| --- | --- | --- | --- | --- |
| `ram_budget_mb` | `1536` | `4096` | `8192` | `12288` |
| `stop_below_free_mb` | `1536` | `2048` | `2048` | `2048` |
| `tile_radius` | `4` | `8` | `12` | `16` |
| `ai_vehicles` | `essential` | `load` | `complete` | `complete` |
| `vehicle_budget_mb` | `512` | `1536` | `0` | `0` |
| `user_vehicles` | `none` | `list` | `list` | `all` |
| `user_vehicle_scope` | | `essential` | `load` | `load` |
| `fleet_scope` | | `load` | `complete` | `complete` |
| `pre_open` | `learned` | `learned` | `learned` | `all` |
| `pre_open_max_mb` | `512` | `1024` | `2048` | `4096` |
| `pre_open_threads` | `1` | `2` | `2` | `4` |

Where a cell is blank the setting does not matter for that profile. Unlisted settings keep their defaults.

Put the values where they suit you: the section `[omsi-prefetch]` of `<OMSI root>\.omsilaunch\modules.ini` for every session, a session-profile preset for one map or way of playing ([session profiles](session-profiles.md)), or the command line for one session ([command-line overrides](command-line-overrides.md)).

## 8 GB

On 8 GB the free memory at the start is usually 4 to 5 GB, so the automatic budget is zero or a few hundred MB. Use an explicit, modest budget and ask for little:

```ini
[omsi-prefetch]
ram_budget_mb=1536
stop_below_free_mb=1536
tile_radius=4
ai_vehicles=essential
vehicle_budget_mb=512
user_vehicles=none
pre_open=learned
pre_open_max_mb=512
pre_open_threads=1
```

Why: the budget leaves more than 3 GB for Windows and OMSI; a floor of 1,536 MB stops loading before the system is short; `essential` holds only the first-needed part of each AI vehicle; the user's vehicles are not held (the pre-open only warms the files you used before in the system's file cache, without reserving memory). The read-ahead and write-behind work whatever the budget, so a small machine keeps part of the gain even with a budget of zero ([memory and budget](../concepts/memory-and-budget.md#when-there-is-no-budget)). If the system starts paging during loading, lower `ram_budget_mb` first.

## 16 GB

About 10 to 12 GB is free when nothing else is running, which gives an automatic budget around 4.8 to 5.5 GB. An explicit 4,096 MB makes the amount predictable:

```ini
[omsi-prefetch]
ram_budget_mb=4096
stop_below_free_mb=2048
tile_radius=8
ai_vehicles=load
vehicle_budget_mb=1536
user_vehicles=list
user_vehicle_list=Bus_You_Drive_1;Bus_You_Drive_2
user_vehicle_scope=essential
pre_open=learned
pre_open_max_mb=1024
```

Replace the list by the folders of the vehicles you really drive (folder names under `Vehicles\`). Leave `ram_budget_mb` out if you prefer the automatic budget and keep the machine free of other large programs.

## 32 GB

The automatic budget reaches the 12,288 MB cap when most memory is free. Ask for less to leave room for the rest of your system:

```ini
[omsi-prefetch]
ram_budget_mb=8192
tile_radius=12
ai_vehicles=complete
user_vehicles=list
user_vehicle_list=Bus_You_Drive_1;Bus_You_Drive_2;Bus_You_Drive_3
user_vehicle_scope=load
fleet_scope=complete
pre_open=learned
pre_open_max_mb=2048
```

`complete` holds each AI vehicle's whole folder, so it is the largest AI footprint; if the log shows `(budget reached)` while many held files were never used, lower it to `load`.

## 64 GB

The budget cap of 12 GB is reached on any machine of this size. Use it, widen the area and hold more vehicles:

```ini
[omsi-prefetch]
ram_budget_mb=12288
tile_radius=16
ai_vehicles=complete
user_vehicles=all
user_vehicle_scope=load
fleet_scope=complete
pre_open=all
pre_open_scope=load
pre_open_max_mb=4096
pre_open_threads=4
```

`user_vehicles=all` is smallest-first after the map's own fleet; the budget is spent on the tiles and the AI first, so a collection too big for 12 GB is simply cut short at its end. Memory beyond 12 GB is left to Windows's own file cache, which also helps OMSI.

## Adjusting from the log

After a session read the lines `prepared in` and `end:` ([log format](../reference/log-format.md)):

| You see | Meaning | Try |
| --- | --- | --- |
| `prepared in ... (budget reached)` and many files `used outside memory` | The budget is too small for what you use. | Raise `ram_budget_mb`; lower the scopes of what you do not use. |
| `prepared in ... (budget reached)` and `used` is a small part of `files held` | The budget is spent on files you do not use. | Lower `tile_radius`, `ai_vehicles` or the vehicle scopes. |
| `prepared in ... (free memory low)` | The loading floor stopped the loading. | Close other programs, lower `ram_budget_mb`, or lower `stop_below_free_mb` if you accept less headroom. |
| `prepared in ... (time limit reached ...)` | Five minutes of loading passed. | Lower what you hold, or accept that the rest is brought in during the session. |
| `no memory to hold anything` | The automatic budget is zero. | Set an explicit `ram_budget_mb` ([8 GB](#8-gb)). |
| `used outside memory` large on a second run | Your route differs from what was learned, or the learned list is thin. | Run the map again; consider a larger `tile_radius`. |
| Windows pages or stutters while loading | Too much is held for the machine. | Lower `ram_budget_mb` and raise `stop_below_free_mb`. |

Change one setting at a time and compare runs the way [measuring the gain](../getting-started/measuring-the-gain.md) describes.

## Next

[Session profiles](session-profiles.md) · [Command-line overrides](command-line-overrides.md) · [Diagnosing slowness](diagnosing-slowness.md)
