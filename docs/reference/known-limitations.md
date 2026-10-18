# Known limitations

This page lists, from the code, what OMSI Prefetch `1.0.0-beta.1` does not do, does only partly, or does at an accepted risk, so that you do not build expectations on behavior it does not provide. Each row says what the limitation is, why it exists and what you can do about it. Where the README of the repository and the code were compared, the code wins; the differences found are listed at the end.

Related pages: [safety](../concepts/safety.md), [errors and degradation](errors-and-degradation.md), [compatibility](compatibility.md), [FAQ](faq.md), [test coverage](../status/test-coverage.md).

## Measurement and scope

| Limitation | Detail |
| --- | --- |
| The published results are one machine and one map (n = 1) | The table in the [overview](../getting-started/overview.md#what-you-get) comes from the loading optimization study of 30/09/2026 on the RMG Leste map. Other hardware and maps will differ ([measuring the gain](../getting-started/measuring-the-gain.md)). |
| Frame rate does not change | The median frame time (about 52 ms in the study) and the pure game CPU parts ("Refresh Schedules") are not touched. |
| It works only through OmsiLaunch | The session program is started by OmsiLaunch before OMSI, and the hooks are installed by its bridge. |
| Data is per map | Nothing learned on one map helps another, even if they share content. |

## Memory

| Limitation | Detail |
| --- | --- |
| At most 12 GB is held | The budget is capped at 12,288 MB whatever the RAM. |
| The budget is fixed at the start | The automatic budget reads the free memory once. Later changes of free memory are handled only by the floors and the guard, which release files OMSI has not used; the budget itself is not recomputed. |
| No eviction of tiles behind the player | When the budget is full, the prediction holds no new tile until memory comes back (the guard, a file the game writes, the end of the session). Tiles the player left stay held. A smaller `tile_radius` or a budget that fits the area you drive in avoids this. |
| Held files are file-backed memory of another process | Task Manager shows it as the working set of `Prefetch.Preloader.exe`. It counts against the free memory other programs see. |
| The working-set request is soft | If Windows refuses it (the log says so) or the system is under pressure, held pages can be trimmed. |
| Pre-open is a hint | Opening a file ahead warms the system's file cache, which Windows can drop before OMSI asks. |

## Learning and data

| Limitation | Detail |
| --- | --- |
| The learned list only grows | A file used once stays in `learned.txt` until you clear the data; many routes make a big list, and the files used first are held first. With a small budget the later ones are not held before OMSI starts. |
| The tile index notices only `global.cfg` and the tile files | Its fingerprint covers their names, sizes and write times. A texture, an object or a spline that changed without touching those files is not noticed until the index is cleared. |
| The first session of a map is not faster | The module has nothing learned; it holds what it can derive from the map. It also delays the start of OMSI by the preparation. |
| Start tiles for a new map use both entrypoint orders | The order in which OMSI presents the entrypoints is not known offline, so the tile of the entrypoint of the file order and the one of the name order are both treated as start tiles. One of the two is extra. |
| Learning needs a readable map | With no map in the session, or a map that cannot be read, nothing is learned. |
| Data is not shared between installations | Everything is below the installation's `.omsilaunch\module-data\`. |
| The user's vehicles are held late | They are held while OMSI runs, after the tiles and the AI vehicles have been served; a vehicle you pick in the first moments may not be held yet ([vehicle scopes](../concepts/vehicle-scopes.md)). |

## Safety and accepted risks

| Limitation | Detail |
| --- | --- |
| Terminating OMSI loses buffered `.rdy` bytes | With write-behind, up to 256 KiB per open `.rdy` terrain-cache handle is only in memory until a flush. A normal exit flushes; terminating OMSI (`session stop`, the tray, a crash) loses the rest. The file is a regenerable cache. |
| A failed write-behind flush is silent to OMSI | OMSI has already been told the bytes were written. The failure is counted (`write_behind_failed`) and write-behind turns off for new handles. |
| A held file cannot be truncated or replaced by another program | The module releases a file when OMSI asks (within 1.5 s). It does not release when another program, such as an editor, writes the file in place; that program receives `ERROR_USER_MAPPED_FILE`. Edit your add-ons before a session, not during. |
| A handle reopened for writing is handed back to the game's read-only handle at stop | If OMSI writes to a vehicle asset that was opened read-only and is still open at the moment the module stops, a later write through the game's own handle would be refused. |
| Hooks are import hooks of `Omsi.exe` | A hook is refused when someone outside the bridge changed the import, and it is removed when the module stops. A thread still returning through a detour is kept safe because the DLL stays loaded until OMSI exits. |
| A session ended by terminating OMSI skips the module's cleanup inside OMSI | Module cleanup inside OMSI happens on a normal exit. The session program always gets its stop. |

## Behavior at the edges

| Limitation | Detail |
| --- | --- |
| The report queue is bounded | 400,000 queued lines and 500,000 distinct paths remembered; beyond that, lines are dropped and counted and later first uses are not reported. Handles beyond 4096 at once are served natively. |
| Feed gives up after ten seconds without a session program | The native features keep working; no learning from then on. |
| D3DX hooks depend on `d3dx9.dll` being loaded at start | If it is not, textures and meshes loaded by Direct3D itself are not reported as uses (`-1` in the `hooks` event). |
| References the resolver cannot find are skipped | The module follows references the way the study's tools did (first tab field of a line, quoted strings, a `model\` folder, `texture\` folders up the tree, another image extension). A file named in a way that none of those finds is not held until the learned list has it. |
| Folders that are links are not listed | Junctions and symbolic links are not entered when the module lists a map or vehicle folder. |
| Only two display languages | The module's windows are Portuguese or English. |
| One window may appear before OMSI | The first-open window is always on top; it never takes the focus. |

## Evidence

The module's offline gates and the study that measured the mechanisms are described in [test coverage](../status/test-coverage.md). The offline gates exercise the session program, the native component with a fake bridge and the package with OmsiLaunch's own manifest parser; the measured gain is that of the study. The README states the study's results as valid for the module.

## Differences found between the README, the manifest and the code

When the code and the README, a manifest description or a comment disagreed, these pages follow the code. The differences found while writing them:

- The README gives the first-open window's title as "Pré-carregamento sendo inicializado…" as if it were fixed. The code shows it in Portuguese **only** when the Windows display language is Portuguese; in every other language the English text ("Preloading is being set up") is shown.
- The README says the tile prediction uses "the centroid of the tile files OMSI opened in the last minute". The code uses the tile files opened in the last 60 seconds and, if fewer than 20 were opened, the 20 most recent.
- The manifest description of `read_only_folders` says assets below the folders are opened read-only. The code also requires one of 18 asset extensions ([native shim](../concepts/native-shim.md#read-only-open)).
- The manifest description of `sounds` says it includes "the sound files" that vehicles and objects cite. The code follows only `.wav` citations (and, in the `complete` scope, leaves `.wav` files out of the folder when it is off).
- The manifest description of `survey` says it reads the map's tiles to know what each one needs. With `survey` off the code also stops the tile prediction during the session and the holding of the start tiles before OMSI.
- The manifest description of `learn` says it remembers what each session used. With `learn` off the code still reads and uses an existing learned list; it only stops updating it.
- A comment in `prefetch\preloader\Windows.cs` names the monitor flag `/preload-monitor`; the flag is `/prefetch-monitor` (manifest and README).

## Next

[FAQ](faq.md) · [Errors and degradation](errors-and-degradation.md) · [Test coverage](../status/test-coverage.md)
