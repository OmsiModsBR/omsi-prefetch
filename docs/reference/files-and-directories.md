# Files and directories

This page lists everything OMSI Prefetch reads and writes: the module's own files, the files it reads from the OMSI installation, the data it keeps and its formats, the other resources it uses (a named pipe, memory, threads), and the bounds that apply to each. Everything is taken from the code of the session program (`prefetch\preloader\`) and the native component (`prefetch\native\`).

Related pages: [safety](../concepts/safety.md), [learned data](../concepts/learned-data.md), [log format](log-format.md), [clearing data](../guides/clearing-data.md), [settings](settings.md#data).

## The module's files

```text
<OMSI root>\plugins\OmsiLaunch.Modules\omsi-prefetch\
  module.json              manifest, with the SHA-256 of each component
  Prefetch.Module.dll      managed component (inside OMSI)
  prefetch-native.dll      native component, x86 (inside OMSI)
  Prefetch.Preloader.exe   session program, x64 (before and next to OMSI)
  NOTICE.txt  GPL-3.0.txt  LGPL-3.0.txt
```

The module reads nothing from this folder except through OmsiLaunch's loader, and writes nothing into it.

## What it reads

Only inside the OMSI folder; a path that leaves it is never opened. All opens are for reading with read, write and delete sharing, and for the files that are held the handle is closed after mapping.

| Files | Why | Read limit |
| --- | --- | --- |
| `maps\<map>\global.cfg` | Tiles, entrypoints, the files the map cites (ground textures, money system, ticket pack), the friendly name. | 64 MiB |
| `maps\<map>\tile_X_Y.map` | Which objects and splines each tile places (line by line; `.sco` and `.sli` references). | 64 MiB per tile file |
| `maps\<map>\ailists.cfg`, `parklist_p.txt`, `humans.txt`, `drivers.txt`, `unsched_vehgroups.txt` | The map's AI vehicles, people and objects. | 8 MiB each |
| The map's folder, recursively | The map-wide files to hold. | at most 50,000 files listed |
| Scenery objects, splines, vehicle files (`.sco`, `.sli`, `.bus`, `.ovh`, `.hum`, `.cfg`, `.cti`, `.zug`) | Followed for the files they cite. | 8 MiB each |
| `.o3d` models | Searched for the names of the textures they hold. | 256 MiB each |
| Models, textures, scripts, sounds, `.hof` and the rest that the closure reaches | Held in memory, or opened ahead. | Held whole; a file that does not fit the budget is skipped. |
| Folders under `Vehicles\` | To find valid vehicles (`.bus`/`.ovh` with a `[model]` section) for `user_vehicles` and `pre_open`. | 200,000 files listed per folder at most (20,000 for the first-use pre-open) |
| Its own data files (below) | Learned and written lists, tile index. | 64 MiB (lists), 512 MiB (tile index) |

Text files are read as OMSI writes them: UTF-16 with a byte-order mark, UTF-8 with a byte-order mark or Windows-1252. Reparse points (junctions, symbolic links) are not followed when a folder is listed. A reference longer than 1,024 characters, or with invalid path characters, is ignored. An entry count of more than 4,096 entrypoints in `global.cfg` is ignored.

It does not read `options.cfg`, your saved situations, your documents or anything outside the OMSI folder. The files that the OmsiLaunch session transaction owns are never held ([safety](../concepts/safety.md#what-is-never-held)).

## What it writes

Only below `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\` (the module's data directory, created by OmsiLaunch):

```text
.omsilaunch\module-data\omsi-prefetch\
  maps\<folder>-<hash>\
    learned.txt    files the game used on the map, first-use order
    written.txt    files the game wrote: never held
    tiles.idx      tile index, with the map's fingerprint
  logs\
    preloader-<yyyyMMdd-HHmmss>.log   the three most recent session logs
```

It writes no CSV, no sample traces and no copies of OMSI's files. It is not part of the session transaction: the data stays after the session. A save writes `<file>.tmp` and then replaces the old file with it.

### `learned.txt` and `written.txt`

UTF-8 without a byte-order mark, LF line endings. The first line is a comment (`# omsilaunch prefetch: files the game used on this map, in first-use order` and `# omsilaunch prefetch: files the game wrote: never held in memory`); each other line is a path relative to the OMSI folder. Lines starting with `#` and empty lines are ignored when reading; a path that resolves outside the OMSI folder is ignored.

`learned.txt` is capped at 200,000 entries when written. `written.txt` has no cap of its own but only holds files the game wrote.

### `tiles.idx`

UTF-8 without a byte-order mark, LF line endings:

```text
# omsilaunch prefetch tile index v1
fingerprint <64 hex digits>
F<TAB><id><TAB><path relative to the OMSI folder>
T<TAB><tile index><TAB><id>,<id>,...
```

`F` lines come first (ids are 0, 1, 2, ... in order, one per distinct file), then one `T` line per tile (the tile's position in the `[map]` list of `global.cfg`, and the ids of the files it needs in loading order). The fingerprint is the SHA-256 of the name, size and last-write time of `global.cfg` and of every tile file. The file is used only when its header and its fingerprint match.

### Folder names of a map

`maps\<folder>-<hash>`: `<folder>` is the map's folder name with every character other than a letter, digit, `-`, `_` or `.` replaced by `_`, cut to 40 characters; `<hash>` is the first eight hex digits of the SHA-256 of `maps\<folder>\global.cfg` in lower case.

### Logs

`logs\preloader-<yyyyMMdd-HHmmss>.log`, UTF-8 without a byte-order mark. See [log format](log-format.md).

## Bounds on disk

| What | Bound |
| --- | --- |
| Maps' data (all maps together) | `data_limit_mb`, default 256 MB (16 to 4096). Checked when a session starts; the maps used least recently are removed first, never the current map. |
| One log file | 1 MiB (1,048,576 bytes); after it, one marker line and the rest is dropped. |
| Log files kept | 3 (when a new one is opened, all but the two newest older ones are deleted; names sort by date). |
| `learned.txt` | 200,000 entries |
| Typical size | A few megabytes per map |

Everything is removed with `OmsiLaunch.exe /clear-module-data:omsi-prefetch`.

## Other resources

| Resource | Detail |
| --- | --- |
| Named pipe | `\\.\pipe\OmsiLaunch.Module.<session id without dashes>.omsi-prefetch`, created by the session program, current user only, one client, in-bound only (OMSI to the program). Exists only while the session runs. |
| Memory | The held files, mapped read-only in `Prefetch.Preloader.exe`; the native component's buffers in OMSI (64 KiB per read-ahead handle, 256 KiB per write-behind handle; up to 4096 handles tracked) and its feed queue (up to 400,000 lines and a set of up to 500,000 paths). |
| Threads | In the session program: the session thread, the controller reader, the pipe reader and its worker, and during the session the survey, the prediction, the user vehicles, the pre-open, the guard, plus up to 16 pre-open threads and up to 16 first-use threads per vehicle folder. In OMSI: one feed thread. |
| Hooks | `iat:kernel32.dll!CreateFileW`, `ReadFile`, `CloseHandle`, `WriteFile`, `SetFilePointer`, `GetFileSize`, `SetEndOfFile`; `iat:d3dx9.dll!D3DXCreateTextureFromFileExW`, `D3DXCreateTextureFromFileExA`, `D3DXCreateTextureFromFileW`, `D3DXLoadMeshFromXA`. Owned by `module:omsi-prefetch` in the bridge's registry and removed when the module stops. |
| Network, registry, other processes | None. |

## Next

[Log format](log-format.md) · [Clearing data](../guides/clearing-data.md) · [Learned data](../concepts/learned-data.md)
