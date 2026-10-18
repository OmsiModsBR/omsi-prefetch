# Vehicles and scopes: essential, load and complete

A vehicle is many files: scripts, models, textures, sounds, repaints, timetables. Holding all of every vehicle you own would use more memory than most machines have, so OMSI Prefetch has three scopes that say how much of a vehicle is held, and separate settings for the vehicles the map itself uses (its AI) and the vehicles you drive. This page defines the scopes, shows how the module decides which vehicles to consider, and says when each set is held.

Related pages: [how prefetching works](how-it-works.md), [memory and budget](memory-and-budget.md), [pre-open](pre-open.md), [settings reference](../reference/settings.md#vehicles), [choosing settings by memory size](../guides/choosing-settings-by-memory.md).

## The three scopes

| Scope | What is held | When to choose it |
| --- | --- | --- |
| `essential` | What a vehicle preview needs first: the vehicle file (`.bus` or `.ovh`), its model `.cfg`, the largest mesh (`.o3d`) with the textures it names and the base textures of that mesh's configuration, and the `.hof` files in the vehicle's folder. | Many vehicles on a small budget; the smallest footprint. |
| `load` | The whole loading closure: everything the vehicle file cites, followed transitively (models, textures with their `.cfg`, scripts, sub-objects, the repaint list and the textures of the repaints it lists, with `sounds` the cited `.wav` files), plus the `.hof` files in the vehicle's folder. | What OMSI reads when it loads the vehicle. A balanced default for vehicles you use. |
| `complete` | The `load` closure first, then every file of the vehicle's folder, recursively, except work files and archives (`.psd .rar .zip .7z .blend .max .3ds .pdf .doc .xls .db .bak .tmp .exe .dll .fbx .obj .svg .log .osn .rdy .sav` and the like). With `sounds` off, `.wav` files are left out. | Vehicles you use every session and have memory to spare. |

Order matters inside a vehicle: the files are held in loading order, so a vehicle cut short by the budget keeps the part OMSI reads first. A vehicle is held whole only when everything it still needs fits the budget and the vehicle cap `vehicle_budget_mb`; otherwise it is skipped (smaller vehicles after it can still fit).

## The map's vehicles (AI)

The module finds the vehicles of the map by reading the map's own lists: `ailists.cfg`, `parklist_p.txt`, `humans.txt`, `drivers.txt` and `unsched_vehgroups.txt`. Every reference to a `.bus`, `.ovh`, `.sco` or `.hum` file that exists becomes part of the map's AI:

- A reference below `Vehicles\<folder>\` makes that folder an AI vehicle, held with the scope of `ai_vehicles`.
- A reference elsewhere (people, objects the AI places) goes into one extra entry, "objects and people of the map", which is followed like scenery whatever the scope.
- AI vehicles are held smallest first, after the objects and people.

`ai_vehicles` takes `off`, `essential`, `load` or `complete` (default `complete`). `off` holds none of the AI vehicles **and** none of the objects and people. The AI vehicles are held in the preparation, before OMSI starts, after the map-wide files and the start tiles and before the wider ring of tiles.

## The user's vehicles

`user_vehicles` chooses which of the other vehicles are held:

| Value | Vehicles considered |
| --- | --- |
| `none` (default) | None. |
| `list` | The folders named in `user_vehicle_list` (folder names under `Vehicles\`, separated by `;`, in the order given, matched without regard to case). A name that is not a valid vehicle folder, or that is a vehicle of the map's AI, is skipped and the log says so. |
| `all` | Every folder of `Vehicles\` that has a valid vehicle and is not used by the map's AI, the map's own fleet first, then smallest first. |

A folder is a valid vehicle when it holds a `.bus` or `.ovh` file with a `[model]` section. Folders whose name starts with `.` and folders without a valid vehicle are ignored. With `ai_vehicles` set to `off` no folder is excluded as AI, so `all` then includes the vehicles the map would have used.

The scope is `user_vehicle_scope` (default `essential`). A vehicle that carries the map's timetable, a `.hof` file named after the map's folder in the vehicle's own folder, is the map's own fleet and uses `fleet_scope` instead (default `load`).

The user's vehicles are held **while OMSI runs**, not before it starts: a background activity waits for the tile prediction to be idle (up to two minutes at first, then up to 30 seconds before each vehicle) and holds the vehicles one by one. Choose a scope and a list that fit the budget: they are held after the budget has gone to the tiles and the AI.

## Sounds

`sounds` (default true) controls the `.wav` files that vehicles and objects cite and, in `complete`, the `.wav` files of a folder. It does not change other audio formats: `.ogg` and similar files are held only by `complete`, as part of the whole folder, whatever `sounds` says.

## Sizes, ordering and caps

- `vehicle_budget_mb` caps the AI tier and the user tier separately (default 0: only `ram_budget_mb` limits).
- Everything is bounded by the budget and the floors ([memory and budget](memory-and-budget.md)).
- Under memory pressure the guard releases unused vehicle files first ([memory and budget](memory-and-budget.md#the-memory-guard)).
- A vehicle's files are resolved when needed; the AI set is computed once at the preparation and the user set once during the session.

## Next

[Pre-open](pre-open.md) · [Choosing settings by memory size](../guides/choosing-settings-by-memory.md) · [Settings reference](../reference/settings.md)
