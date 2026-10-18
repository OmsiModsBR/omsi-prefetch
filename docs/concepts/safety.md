# Safety: what the module touches and what happens when something goes wrong

OMSI Prefetch runs next to your simulator and reads a lot of its files, so this page states plainly what it reads, what it writes, what it never holds, how it avoids getting in OMSI's way, and what the outcome is when OMSI, OmsiLaunch or the module itself stops unexpectedly. Everything is taken from the code. The module is not a security product and OmsiLaunch does not vouch for modules; the boundary OmsiLaunch guarantees for any module is in the Runtime Modules reference ("Security boundary").

Related pages: [how prefetching works](how-it-works.md), [native shim](native-shim.md), [memory and budget](memory-and-budget.md), [files and directories](../reference/files-and-directories.md), [errors and degradation](../reference/errors-and-degradation.md), [known limitations](../reference/known-limitations.md).

## What it reads

The session program opens files inside the OMSI folder **read-only**, to hold them or to work out what a map or a vehicle needs: the map's `global.cfg`, its tile files, its lists (`ailists.cfg`, `parklist_p.txt`, `humans.txt`, `drivers.txt`, `unsched_vehgroups.txt`), the scenery objects, splines, models, textures, scripts and sounds they cite, and the vehicles' files. Paths that leave the OMSI folder are never opened. It does not read your documents, the registry, the network or any other program's files. The module has no network code.

The size limits that bound a read are in [files and directories](../reference/files-and-directories.md#what-it-reads).

## What it writes

| Where | What | Who |
| --- | --- | --- |
| `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\maps\<folder>-<hash>\` | `learned.txt`, `written.txt`, `tiles.idx` (and a transient `.tmp` during a save) | The session program |
| `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\logs\` | `preloader-<date>-<time>.log` | The session program |
| OMSI's own files | The bytes **OMSI itself** writes, exactly as it writes them: with write-behind the write of a terrain cache (`.rdy`) can be delayed and grouped, never changed | The native component, on behalf of OMSI |

Nothing else. The module never creates, edits, renames or deletes a file of your OMSI installation, of your vehicles, maps or add-ons. It does not write `options.cfg`, the splash bitmaps or any other file that the OmsiLaunch session transaction owns, and it never holds them. Its data directory is outside the session transaction by design, so it stays after the session. The data directory is bounded ([learned data](learned-data.md#bounds-and-clean-up)).

## What is never held

A file is never held (mapped) when any of these is true:

- its extension is one of `.osn .rdy .tbmp .odr .tmp .log .sav .bak .owt .prt .lm.bmp` (files the game rewrites or streams: it truncates `laststn.osn` when it closes the map and streams `tile_X_Y.map.prt` and `.LM.bmp` while it loads tiles; a mapped file cannot be truncated, the game would stall or its tile loader thread would die and half the map would not load);
- the game wrote it in an earlier session on this map (`written.txt`) or in this session;
- it is one of the files the OmsiLaunch session transaction owns (the context's `session_files`, among them `options.cfg` when it is overlaid and the splash bitmaps);
- it is empty, cannot be opened, or would not fit the budget.

The map-wide files, the tile files and the vehicle scopes also leave out work files and archives (`.psd .rar .zip .7z .blend .max .3ds .pdf .doc .xls .db .exe .dll .fbx .obj .svg` and the like).

## Not getting in OMSI's way

| Mechanism | What it guarantees |
| --- | --- |
| Handles are closed at once | The preloader maps a file and closes its handle. OMSI's own opens of the same file never meet a sharing conflict. |
| Full sharing | Every open by the module allows read, write and delete sharing. |
| Release at OMSI's request | If OMSI tries to replace or truncate a file that is held (Windows answers `ERROR_USER_MAPPED_FILE`), the native component asks the session program to release it (an `X` line) and retries for up to 1.5 s. The program answers on the thread that reads the pipe, ahead of every other line, so a burst of reports cannot delay it. The file is released and never held again. If no session program is connected, the call fails as it would without the module. |
| Exact semantics | Read-ahead, write-behind and read-only open keep reads, positions, sizes and end-of-file behavior exactly what an unhooked call returns ([native shim](native-shim.md)). |
| Features need every hook | If a position hook is not installed, none of the three features starts. |
| Read-only open reverts | If OMSI writes to a handle opened read-only, the file is reopened with the access OMSI asked for and the handle is served from it. |
| Low priority | The survey, the user-vehicle holding and the pre-open run below normal priority, and the pre-open with background I/O priority. |
| Memory floors and guard | The module stops loading below a free-memory floor and gives back files OMSI has not used when memory runs low ([memory and budget](memory-and-budget.md)). |

## Process and channel boundary

- The session program is a separate process started by OmsiLaunch inside a job object: it ends with the session, cannot start other programs and cannot change system or display settings.
- Its pipe is restricted to the current user and accepts one client, the process OmsiLaunch names as OMSI; any other client is refused and counted.
- Lines on the pipe are bounded (4096 bytes), accept only four kinds, must be valid UTF-8 without control characters, and name paths that are resolved inside the OMSI folder; a path outside it is ignored. A path is data: it is only ever used to look up a file to hold, never run or followed.
- The managed and native components run in OMSI with OMSI's rights. They hold no installation files.
- Nothing leaves the machine.

## What happens when something goes wrong

| Event | Effect on OMSI | Effect on your files | What the module keeps |
| --- | --- | --- | --- |
| OMSI exits normally | None | None. The shim flushes every buffer before the hooks go; the session program saves and exits. | Learned data is saved at the end. |
| OMSI crashes or is killed | OMSI is gone | The `.rdy` bytes still buffered by write-behind (up to 256 KiB per open `.rdy`) are lost; `.rdy` is a regenerable cache, which OMSI rebuilds. No other file is affected. | The session program is stopped by OmsiLaunch after OMSI exits; it saves what it learned. |
| The session is ended with the tray or `session stop` | OMSI is terminated by OmsiLaunch | Same as a killed OMSI. A session stop terminates OMSI, so module cleanup inside OMSI does not run. | The session program is told to stop and saves. |
| OmsiLaunch is killed | OMSI keeps running | None | The job object ends the session program. The last periodic save (at most two minutes old) is what remains; held memory is released with the process. |
| The session program crashes or is killed | OMSI keeps running | None | Held memory is released. The native component stops reporting (it gives up after ten seconds without a pipe); read-ahead, write-behind and read-only open keep working. Nothing is learned from then on; what was saved earlier stays. |
| The machine loses power or Windows crashes | | The `.rdy` caveat above. A save in progress leaves the old data file intact (new data is written to a `.tmp` and then replaces it). | Last completed save. |
| The data directory is full or not writable | None | None | Saves fail and are logged (`save: ...`); the session continues. |
| A native hook is refused | OMSI runs as without that hook | None | Per [errors and degradation](../reference/errors-and-degradation.md). |

Because the module never modifies a file of its own accord, the recovery of an OmsiLaunch session after a crash (`/recovery-status`) never involves it. After a crash there is nothing for the module to restore.

## Accepted risks

These are the risks the module accepts, each stated in full in [known limitations](../reference/known-limitations.md):

- Terminating OMSI while it writes a `.rdy` loses the bytes still buffered; the file is a regenerable cache.
- A write to a vehicle asset that is still open at the very moment the module stops can be refused, because the game's own read-only handle is used again.
- A file held in memory cannot be truncated or replaced by another program while it is held; the module releases a file when OMSI asks, not when an editor does.

## Next

[Files and directories](../reference/files-and-directories.md) · [Errors and degradation](../reference/errors-and-degradation.md) · [Known limitations](../reference/known-limitations.md)
