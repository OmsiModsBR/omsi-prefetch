# Events and notices

OMSI Prefetch reports its progress to OmsiLaunch as runtime events named `module.omsi-prefetch.<name>`. This page lists every event the module emits (and the ones OmsiLaunch emits about its session program), with the keys of their data and when they appear, and says what the module does not emit (notices, operations). Events are read while the session runs with `OmsiLaunch.exe events read` or `events watch` and through `session status`; all values are strings.

Related pages: [log format](log-format.md), [errors and degradation](errors-and-degradation.md), [measuring the gain](../getting-started/measuring-the-gain.md), [session program](../concepts/session-program.md), [native shim](../concepts/native-shim.md).

## Events at a glance

| Event | Emitted by | When |
| --- | --- | --- |
| `module.omsi-prefetch.host.started` | OmsiLaunch | The session program was started. Data: `pid`. |
| `module.omsi-prefetch.prepared` | Session program | The preparation finished, just before it reports ready. Not emitted when the preparation is skipped (no map, unreadable map, zero budget). |
| `module.omsi-prefetch.host.ready` | OmsiLaunch | The program reported ready. Data: `prepare_ms`. |
| `module.omsi-prefetch.hooks` | Native component | The hooks were requested (when OMSI loads the component). |
| `module.omsi-prefetch.armed` | Managed component | The module was armed inside OMSI. |
| `module.omsi-prefetch.gameplay-ready` | Managed component | Gameplay was entered. |
| `module.omsi-prefetch.stopping` | Managed component | The module is stopping (a normal exit of OMSI). |
| `module.omsi-prefetch.stats` | Native component | The native component stopped. |
| `module.omsi-prefetch.session` | Session program | At the end of the session, after the program was told to stop. |
| `module.omsi-prefetch.host.failed`, `host.exited`, `host.killed` | OmsiLaunch | The session program failed to prepare, ended or was ended ([errors and degradation](errors-and-degradation.md)). |

OmsiLaunch also reports its own module events (`module.session-failure`, `module.failed`, `module.degraded`, ...) as described in its Runtime Modules reference.

## `module.omsi-prefetch.prepared`

| Key | Meaning |
| --- | --- |
| `held_mb` | MB held at the end of the preparation. |
| `budget_mb` | The budget, in MB. |
| `files` | Number of files held. |
| `ms` | Duration of the loading steps, in milliseconds (excludes the time to read the map and the tile index beforehand). |
| `first_open` | `true` when the module knew nothing about the map ([first run](../getting-started/first-run.md#the-first-open-window)), `false` otherwise. |

## The hooks event

`module.omsi-prefetch.hooks` carries the result of every hook request, as the numbers the bridge returns. `0` is success; `-1` means the hook was not requested or not available. Other values are the bridge's refusal codes (for example `3` the site does not hold what it should, `4` the site is hooked by someone outside the bridge, `10` no device).

| Key | Hook |
| --- | --- |
| `create`, `read`, `close` | `CreateFileW`, `ReadFile`, `CloseHandle`. Required: if any is not `0` the module's native phase fails. |
| `write` | `WriteFile`. |
| `seek`, `size`, `truncate` | `SetFilePointer`, `GetFileSize`, `SetEndOfFile`. `-1` when read-ahead, write-behind and read-only open are all off. |
| `tex_ex_w`, `tex_ex_a`, `tex_w`, `mesh` | `D3DXCreateTextureFromFileExW`, `...ExA`, `D3DXCreateTextureFromFileW`, `D3DXLoadMeshFromXA`. `-1` when `d3dx9.dll` was not loaded or does not export the function when the component started. |
| `feed` | `1` when the session pipe is configured, `0` when OMSI has no session program (then no use reports are sent). |

## `module.omsi-prefetch.armed`

| Key | Meaning |
| --- | --- |
| `channel` | `session-program` when the module has a session channel, `none` otherwise. |
| `read_ahead`, `write_behind`, `read_only_open` | The effective settings as `true` or `false`. |

## `module.omsi-prefetch.stats`

Emitted when the native component stops, after it has flushed and handed back every handle. The counters are for the whole session.

| Key | Meaning |
| --- | --- |
| `app_reads` | `ReadFile` calls OMSI made through the module. |
| `real_reads` | `ReadFile` calls that reached the operating system. `app_reads` divided by `real_reads` is the reduction. |
| `served_from_buffer` | Reads served (at least in part) from a read-ahead block. |
| `shim_stopped` | Handles on which a write or truncate ended the shim. |
| `write_behind_app_writes` | Writes OMSI made to buffered `.rdy` handles. |
| `write_behind_real_writes` | Writes that reached the operating system for them. |
| `write_behind_failed` | Flushes that failed twice (write-behind then turns off for new handles). |
| `read_only_opens` | Opens downgraded to read-only. |
| `read_only_reopened` | Read-only handles OMSI wrote to, reopened with write access. |
| `read_only_reopen_failed` | Reopens that failed. |
| `track_full` | Handles that could not be tracked (more than 4096 at once). |
| `release_requests` | Times OMSI could not replace a held file and a release was requested. |
| `feed_dropped` | Use-report lines dropped (queue full or no session program reachable). |

## `module.omsi-prefetch.session`

| Key | Meaning |
| --- | --- |
| `held_mb` | MB held at the end. |
| `used_held` | Files held that OMSI used. |
| `used_outside` | Files OMSI used that were not held. |
| `feed_lines` | Lines received from the native component. |
| `feed_refused` | Clients refused because they were not OMSI's process. |

## Notices and operations

- **Notices.** The module never emits a `notice` event, so OmsiLaunch's tray shows no balloon for it. The only user-visible messages are the module's own two windows: the first-open window and the optional monitor ([session program](../concepts/session-program.md#windows)).
- **Errors.** If the preparation fails, the session program writes one `error` line that OmsiLaunch records as a session diagnostic with `OL_E_MODULE_OPERATION_FAILED` and the detail `prefetch: the preparation failed: <message>`.
- **Operations.** The module declares no operations. A call to `module.omsi-prefetch.<operation>` answers `OL_E_MODULE_OPERATION_UNKNOWN`.

## Next

[Errors and degradation](errors-and-degradation.md) · [Log format](log-format.md) · [Compatibility](compatibility.md)
