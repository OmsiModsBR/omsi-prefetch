# Log format

The session program writes one log per session. It is made for use, not for debugging: a session writes a few dozen lines that say what was held, how long it took and what went wrong. This page gives the file, its limits, the shape of a line, a sample, and every message the program writes, so that you can read a log (see [diagnosing slowness](../guides/diagnosing-slowness.md)). Messages are taken from the code and are in English whatever the display language.

Related pages: [diagnosing slowness](../guides/diagnosing-slowness.md), [files and directories](files-and-directories.md), [settings: `debug_log`](settings.md#debug_log), [events](events-and-notices.md), [session program](../concepts/session-program.md).

## The file

| | |
| --- | --- |
| Location | `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\logs\` |
| Name | `preloader-<yyyyMMdd-HHmmss>.log`, local time of the start of the program |
| Encoding | UTF-8 without a byte-order mark; one line per entry; flushed after every line |
| Size | At most 1 MiB per file. After that, one marker line (`-- log limit reached (1024 KiB): further lines are dropped`) and no more lines |
| Kept | The three most recent files; older ones are deleted when a new one is opened |
| Verbosity | The normal log, plus per-file and per-vehicle lines with `debug_log` (flag `/prefetch-debug`) |
| Line length | A message longer than 2000 characters is cut and ends with `...` |

If the folder cannot be created or written, the program runs without a log.

## Line shape

```text
HH:mm:ss.fff <message>
```

The time is the local wall-clock time; there is no date in the line (the file name has it). Messages are free text in the forms below; do not parse them with a program that must survive upgrades. Sizes are written as `<number> MB` with one decimal.

## A sample

```text
14:02:11.380 session 8f3c0a52-1d0e-4e1b-9a2f-6a3f1c7d9b10, module 1.0.0-beta.1, OmsiLaunch 0.1.0-beta4
14:02:11.384 budget 4832.0 MB (setting auto), free 11000 MB of 16072 MB
14:02:11.402 map maps\Grundorf\global.cfg (Grundorf): 124 tiles, 6 entrypoints; learned 3120 files; tile index 124 tiles from an earlier session; start 10,12 11,12
14:02:11.931 prepare: learned: 2874 files, 612.3 MB
14:02:12.020 prepare: map: 41 files, 18.7 MB
14:02:12.455 prepare: start tiles (9 tiles): 730 files, 311.5 MB
14:02:12.470 vehicles: map AI 58 citations, 12 folders, 9 objects/people (904.2 MB)
14:02:13.912 prepare: tiles around the start (121 tiles): 1980 files, 903.0 MB
14:02:13.915 prepared in 2.5 s: 6900 files, 2749.5 MB of 4832.0 MB
14:02:20.118 controller: OMSI started, pid 12044
14:02:23.540 feed: OMSI connected
14:08:01.004 controller: stop
14:08:02.250 end: 6977 files held (2755.0 MB), used 3318 of them (1203.4 MB), 46 used outside memory (12.9 MB); feed 4221 lines
14:08:02.260 exit
```

(The figures are illustrative.)

## Messages

### Start and map

| Message | Meaning |
| --- | --- |
| `session <id>, module <version>, OmsiLaunch <version>` | First line of the session. |
| `budget <size> (setting <auto or MB>), free <MB> MB of <MB> MB` | The memory budget and the free and total physical memory at the start. |
| `the working set minimum was not raised: held pages may be trimmed under memory pressure` | Windows refused the request to keep the held pages resident ([memory and budget](../concepts/memory-and-budget.md)). |
| `the session channel cannot be served: <reason>` | The session pipe could not be created. OMSI's native component cannot report; the preloader still holds files. |
| `no map in this session: nothing to prepare` | The session has no map. Nothing is held or learned. |
| `the map <identity> cannot be read: nothing to prepare` | `global.cfg` is missing, not a `global.cfg` or unreadable. |
| `data: <size> of the least recently used maps removed to stay under <MB> MB` | The data of old maps was removed to respect `data_limit_mb`. |
| `map <identity> (<name>): <n> tiles, <n> entrypoints; learned <n> files; tile index <n> tiles from an earlier session \| new; start <x,y ...>[; first open]` | The map, its size, what is known about it, the start tiles. `first open` marks a map the module knows nothing about. |
| `no memory to hold anything: preparation skipped` | The budget is zero. |

### Preparation

| Message | Meaning |
| --- | --- |
| `prepare: <step>: <n> files, <size>` | What a step held. Steps are `learned`, `map`, `start tiles (<n> tiles)` and `tiles around the start (<n> tiles)`. |
| `vehicles: map AI <n> citations, <n> folders, <n> objects/people (<size>)` | The map's AI vehicles found in its lists, with the size of everything they would need. |
| `prepared in <s> s: <n> files, <size> of <size>[ (time limit reached; the rest continues during the session) \| (budget reached) \| (free memory low)]` | The end of the preparation, and why it ended if it stopped early. |
| `prepare failed: <exception>` | The preparation threw. The module sends `OL_E_MODULE_OPERATION_FAILED` to OmsiLaunch and still reports ready. |

### During the session

| Message | Meaning |
| --- | --- |
| `controller: OMSI started, pid <n>` | OmsiLaunch named OMSI's process. |
| `feed: OMSI connected` / `feed: OMSI disconnected` | The native component connected to or left the pipe. |
| `feed: refused a client that is not OMSI's process` | Another process tried to use the pipe. |
| `feed: line not handled: <type>: <message>` | A use report could not be applied. |
| `feed: the pipe cannot be created again: <reason>` | After a disconnect the pipe could not be re-created; the feed ends. |
| `survey: <n> tiles indexed` | The background tile index is complete. |
| `vehicles: <n> user vehicles (<size>), <n> folders without a valid vehicle` | The user's vehicles found for `user_vehicles`. |
| `vehicles: list entry '<name>' is a vehicle of the map's AI \| not a valid vehicle folder` | An entry of `user_vehicle_list` or `pre_open_list` was skipped. |
| `pre-open: <n> files: <w> warm, <c> cold (<size>), <f> failed in <s> s[; cold data limit reached \| ; free memory low]` | The result of the planned pre-open ([pre-open](../concepts/pre-open.md)). |
| `guard: free memory <MB> MB: loading resumes` | Free memory recovered after a low-memory phase. |
| `<activity>: stopped by an unexpected error: <exception>` | A background activity (`survey`, `prediction`, `vehicles`, `pre-open`, `guard`) ended on an error; the others continue. |
| `session failed: <exception>` | The session loop threw. |

### End

| Message | Meaning |
| --- | --- |
| `controller: stop` | OmsiLaunch asked the program to stop. |
| `save: <reason>` | A save of the learned data failed (disk full, no permission); the session continues. |
| `end: <n> files held (<size>), used <n> of them (<size>), <n> used outside memory (<size>); feed <n> lines` | Result of the session: files still held at the end, how many of them OMSI used, how many files OMSI used that memory did not hold, and how many lines the native component sent. |
| `shutdown failed: <exception>` | The shutdown threw. |
| `exit` | The program ended. |

### Only with `debug_log`

| Message | Meaning |
| --- | --- |
| `vehicles: held <folder> (<scope>): <size>` | A vehicle was held. |
| `vehicles: over the budget: <folder> needs <size>` | A vehicle was skipped: it does not fit the budget. |
| `vehicles: over the vehicle budget: <folder> needs <size>` | A vehicle was skipped: it does not fit `vehicle_budget_mb`. |
| `released at OMSI's request: <path>` | OMSI could not replace a held file and the file was released. |
| `guard: free memory <MB> MB: released <size>` | The guard released unused files. |
| `first use of <folder>: <n> files opened ahead: <result>` | A vehicle's folder was opened ahead on its first use. |
| `first use of <folder>: <reason>` | That attempt failed. |

## Reading it

- `end:` is the summary. "Used outside memory" are files OMSI used that the module did not hold when OMSI opened them: they were read from disk. Many of them mean a budget that was too small or a map that has not been learned yet.
- `prepared in ... (budget reached)` means the budget was spent before the list ended; `(free memory low)` means the loading floor stopped it ([memory and budget](../concepts/memory-and-budget.md)); `(time limit reached ...)` means five minutes passed.
- A log that ends without `exit` means the program was ended abruptly (OmsiLaunch killed, the machine lost power).

## Next

[Events](events-and-notices.md) · [Diagnosing slowness](../guides/diagnosing-slowness.md) · [Errors and degradation](errors-and-degradation.md)
