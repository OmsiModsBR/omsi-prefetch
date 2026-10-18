# Pre-open: opening vehicle files ahead without holding them

Holding a file keeps it in memory. Pre-open does something cheaper: it opens the file and closes it again, on a background thread, so that Windows reads it from disk into its own file cache before OMSI asks for it. The memory is not reserved, the cache is the system's, and the file may be dropped from it later. Pre-open exists for vehicle files because the first open of a file costs a full read of it on a cold disk, and vehicles are the files that are not predictable from the map. This page describes the two forms: the planned pre-open (`pre_open`) and the one triggered by the first use of a vehicle (`pre_open_on_first_use`).

Related pages: [vehicle scopes](vehicle-scopes.md), [memory and budget](memory-and-budget.md), [session program](session-program.md), [settings reference](../reference/settings.md#pre-open), [diagnosing slowness](../guides/diagnosing-slowness.md).

## How a file is opened ahead

The module opens each file with read access and full sharing (read, write and delete), measures how long the open took, and closes it. An open slower than 1.5 ms counts as cold; its file size is added to a total of cold data. When the total reaches the cap, the pre-open stops. The work is done by a small number of threads that run with background I/O priority, so that the game's own reads go first. Every 16 files the threads check the free memory and stop when it is below the loading floor ([memory and budget](memory-and-budget.md)).

At the end the log has one line with the counts: `pre-open: <n> files: <w> warm, <c> cold (<size>), <f> failed in <s> s`, ending with `; cold data limit reached` or `; free memory low` when one of those stopped it.

## Planned pre-open (`pre_open`)

It starts after the user's vehicles have been handled (waiting up to three minutes for them) and after the tile prediction has been idle (up to one minute). It never opens files that are already held or that the module never holds.

| `pre_open` | Files opened ahead |
| --- | --- |
| `none` | Nothing. |
| `learned` (default) | The files below `Vehicles\` that earlier sessions of this map used (the learned list) and the ones the game has used so far in this session, without duplicates. |
| `all` | Every file of the vehicles the module would consider with `user_vehicles=all`, with the scope `pre_open_scope` (default `load`). |
| `list` | The files of the folders in `pre_open_list` (separated by `;`), with the scope `pre_open_scope`. A folder that is a vehicle of the map's AI is skipped. |

Bounds: `pre_open_max_mb` (default 2048) is the cold data after which it stops, and `pre_open_threads` (default 2) is the number of threads. On the first open of a map there is no learned list, so `learned` has nothing to do except the files the game opens meanwhile.

## On first use (`pre_open_on_first_use`)

When OMSI first uses a vehicle file (a `.bus` or `.ovh` below `Vehicles\<folder>\`), typically when a vehicle preview starts, the module opens the rest of that vehicle's folder in parallel, ahead of the game. It happens once per folder and session, never for the folders of the map's AI (those are held), and it is skipped when at least 90 % of the folder's files are already held.

The order of the files is scripts and other text files first, then models (`.o3d`), then textures, and the textures of repaints last (folders named `skins`, `pinturas`, `paint` or `lackierung`). It skips sounds (`.wav .ogg .mp3 .flac`), `.ini` files, work files and archives.

Bounds: `pre_open_first_use_max_mb` (default 400) is the cold data after which it stops and `pre_open_first_use_threads` (default 4) is the number of threads. The thread runs below normal priority and ends when it is done.

## Cost and safety

- Opening a file for reading does not change it. Nothing is written.
- The cost is disk reads at background priority and a little CPU. With a fast disk and a vehicle already in the file cache the opens are warm (under 1.5 ms) and cost almost nothing.
- Pre-open is a hint: the file cache may drop the data before OMSI asks, and the setting does not guarantee a hit.

## Next

[Safety](safety.md) · [Choosing settings by memory size](../guides/choosing-settings-by-memory.md) · [Settings reference](../reference/settings.md)
