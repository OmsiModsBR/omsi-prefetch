# Memory, the budget and the floors

This page explains what "held in memory" means for OMSI Prefetch, why the module holds files in RAM at all, how big the budget is, which floors stop it from taking memory your system needs, which files are spent first, and how it gives memory back. Every number is taken from the code; the settings that move them are in the [settings reference](../reference/settings.md#memory).

Related pages: [how prefetching works](how-it-works.md), [session program](session-program.md), [vehicle scopes](vehicle-scopes.md), [choosing settings by memory size](../guides/choosing-settings-by-memory.md), [diagnosing slowness](../guides/diagnosing-slowness.md).

## What "held" means

The preloader holds a file by mapping it read-only into its own address space and touching every page. One request asks Windows to bring the whole range in (`PrefetchVirtualMemory`), then one read per 4 KiB page makes sure each page is present. The file handle is closed immediately; the mapping keeps the data.

This has four effects that explain the design:

- **The amount is exact.** The pages belong to the preloader process, so the amount held is a number the module knows and can compare with the budget. It is not an estimate of what the system might cache.
- **Windows does not hand the pages to something else** while the game reads the same files through its own opens, as it can with ordinary file cache.
- **OMSI never meets a sharing conflict.** The preloader opens files with read, write and delete sharing and does not keep the handle, so OMSI can open the same file for reading or writing as it would without the module.
- **A mapped file cannot be truncated or replaced.** That is why the module never holds a file the game writes and why it releases a held file the moment OMSI cannot replace it ([safety](safety.md)).

The module also asks Windows to raise the preloader's working-set minimum to the budget plus 256 MB, so that held pages stay resident. The request is soft: under real memory pressure Windows can still take pages back. If the request is refused, the log says so. The held memory shows in Task Manager as the working set of `Prefetch.Preloader.exe`; it is file-backed memory, not allocated memory, and Windows can reuse it for other programs if it is released.

## Why hold files in RAM

The study this module comes from measured the cost of OMSI's file calls, not of the disk ([overview](../getting-started/overview.md)). With the file already in memory, the remaining cost of a call is tiny; without it, the first read of a cold file is a read from disk and every later small read is a system call that may have to wait. The preloader pays the cold-read cost before OMSI starts (and in the background while OMSI runs), where it does not freeze the game.

## The budget

The budget is the amount of memory the preloader may hold. It is fixed at the start of the preparation.

| Setting | Effect |
| --- | --- |
| `ram_budget_mb` = `auto` (default) | 70 % of the physical memory free at that moment, after keeping `reserve_mb` (default 4096 MB) for OMSI and the system, at most 12,288 MB. If less than `reserve_mb` is free, the budget is zero. |
| `ram_budget_mb` = a number | That many MB, between 256 and 12,288. The free memory is not consulted for the size, only for the floors below. |

The automatic formula is `0.7 x (free MB - reserve_mb)`, never negative, never more than 12,288. Examples:

| Free memory at the start | `reserve_mb` | Automatic budget |
| ---: | ---: | ---: |
| 3,000 MB | 4096 | 0 (nothing is held; the log says `no memory to hold anything`) |
| 6,000 MB | 4096 | 1,332 MB |
| 11,000 MB | 4096 | 4,832 MB |
| 20,000 MB | 4096 | 11,132 MB |
| 40,000 MB | 4096 | 12,288 MB (the maximum) |

The 12 GB ceiling applies to the automatic and to the explicit budget alike. A budget of zero does not turn the module off: read-ahead, write-behind and learning still work ([degradation](#when-there-is-no-budget)).

Files are held whole. A file that does not fit in what is left of the budget is skipped, and smaller files after it can still fit. The preparation and the prediction treat the budget as full when less than 1 MiB is left.

### The vehicle cap

`vehicle_budget_mb` (default 0, meaning only `ram_budget_mb` limits) caps the memory the map's AI vehicles may hold and, separately, the memory the user's vehicles may hold. A vehicle is held only if everything it still needs fits under the cap; a vehicle that does not fit is skipped whole.

## The floors

A floor is the amount of free memory below which the module stops loading, so that holding files never starves the system or OMSI.

| Floor | Value | What it does |
| --- | --- | --- |
| Loading floor (`stop_below_free_mb`) | `auto`: 2,048 MB with the automatic budget, 1,024 MB with an explicit `ram_budget_mb`. Or a number from 256 to 65,536. | When free physical memory is below it, no file is held and the pre-open stops. The preparation ends (`free memory low`); the prediction waits and retries. |
| Guard release threshold | 1,024 MB (fixed) | When free memory falls below it, the guard releases held files that OMSI has not used, until about 1,536 MB would be free. |
| Guard resume threshold | 2,560 MB (fixed) | Once the guard has marked memory as low, loading resumes only after free memory is above this. |

An explicit budget is your choice, so the module lets loading go further with it (1,024 MB) than with the automatic one (2,048 MB). Raise `stop_below_free_mb` on a machine that runs other programs next to OMSI.

## Where the budget goes first

The preparation spends the budget in this order, and the tiers keep that order afterwards:

| Order | Tier | What |
| ---: | --- | --- |
| 1 | learned | Files earlier sessions of the map used, in first-use order. |
| 2 | map | The files cited by `global.cfg` and the map-wide files. |
| 3 | tiles | The tiles nearest the start, then more tiles up to `tile_radius`; during the session, the tiles around the estimated position. |
| 4 | AI | The map's AI vehicles and the objects and people they place, according to `ai_vehicles`. |
| 5 | user | The user's vehicles chosen by `user_vehicles`; held during the session. |

The same file is held once, in the tier that got it first. Within a tier the order is the order of the list.

## The memory guard

Every two seconds the guard reads the free memory. Below 1,024 MB it marks memory as low (which stops every loading path) and releases held files, **only files OMSI has not used**, starting with the least valuable tier (user, then AI, then tiles, then map, then learned) and the largest files within a tier, until about 1,536 MB would be free. Files that OMSI has used are never released by the guard. When free memory is later above 2,560 MB the guard clears the mark and logs `loading resumes`.

Nothing else frees memory during a session except the release of a file the game writes or cannot replace, and the end of the session. In particular the module does not evict tiles behind the player when the budget is full ([known limitations](../reference/known-limitations.md)).

## When there is no budget

If the automatic budget is zero, the preparation is skipped and the log says so. OMSI starts as soon as the program is ready, the native component still reads ahead and writes behind, the session program still learns what the game uses, and nothing is held. A machine with little memory can therefore keep part of the gain; see [choosing settings by memory size](../guides/choosing-settings-by-memory.md) for an explicit budget on small machines.

## Next

[Vehicle scopes](vehicle-scopes.md) · [Pre-open](pre-open.md) · [Choosing settings by memory size](../guides/choosing-settings-by-memory.md)
