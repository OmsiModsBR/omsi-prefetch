# Errors and degradation

OMSI Prefetch is built to fail small: the failure policy in its manifest is `degrade`, and each of its parts keeps working when another one cannot. This page lists what can go wrong, which OmsiLaunch code or module message shows it, what the module does and what you can do. The codes are those of the OmsiLaunch error reference; this page says what they mean for this module.

Related pages: [events](events-and-notices.md), [log format](log-format.md), [safety](../concepts/safety.md#what-happens-when-something-goes-wrong), [diagnosing slowness](../guides/diagnosing-slowness.md), [coexisting](../guides/coexisting.md), [installation](../getting-started/installation.md).

## The policy

`failure_policy` is `degrade`, with `prepare_ms` = 600,000 ms and `host_stop_ms` = 20,000 ms. Under `degrade`, a failure of the module's session program (it cannot start, exits before it is ready, or is not ready in time) **leaves the module in the session**: the diagnostic is recorded, OMSI starts, and the parts that do not need the program keep working. A failure of an in-OMSI phase (the native component's start, arming) releases the module inside OMSI and the session continues; the session never fails because of this module.

Planning is different for a module that joins by itself (activation `default`): if it cannot be loaded at all, the plan reports the warning `OL_W_MODULE_NOT_LOADED` and the session runs without the module. Only when you request the module explicitly (`/module:omsi-prefetch`) does a module that cannot be loaded make the plan not runnable.

## At planning

| Situation | Code | What happens | What to do |
| --- | --- | --- | --- |
| A file of the module folder was changed, is missing or is reached through a link | `OL_E_MODULE_INTEGRITY` (as `OL_W_MODULE_NOT_LOADED` for the default activation) | The module is not loaded. | Reinstall the module folder ([installation](../getting-started/installation.md)). |
| `module.json` is missing or invalid, or the folder is not named `omsi-prefetch` | `OL_E_MODULE_MANIFEST_INVALID` | Not loaded. | Reinstall. |
| OmsiLaunch does not serve bridge protocol 2 | `OL_E_MODULE_PROTOCOL_UNSUPPORTED` | Not loaded. | Use an OmsiLaunch that serves protocol 2 (`0.1.0-beta4` or later). |
| The OMSI build does not resolve a capability the module needs (`bridge.hooks.iat`) | `OL_E_MODULE_CAPABILITY_UNRESOLVED` | Not loaded. | See the OmsiLaunch capabilities and `profiles` reference. |
| The plugin `OmsiLoadingPoC.dll` is switched on in `plugins\*.opl` | `OL_E_MODULE_CONFLICT` (as `OL_W_MODULE_NOT_LOADED` for the default activation) | Not loaded. | Switch that plugin off ([coexisting](../guides/coexisting.md)). |
| A setting or a flag value is invalid, or a key does not exist | `OL_E_MODULE_SETTING_INVALID` | The plan is not runnable. | Correct the value; `/list:RuntimeModules` shows the settings. |
| An entry of `.omsilaunch\modules.ini` cannot be used, or settings are requested for a module that is not loaded | `OL_W_MODULE_SETTING_IGNORED` | The default is used. | Correct or remove the entry. |
| A preset sets a key (or the module on or off) that the command line also sets | `OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT` | The plan is not runnable. | Remove one of the two ([session profiles](../guides/session-profiles.md)). |

## Before OMSI: the session program

| Situation | Code or message | What happens |
| --- | --- | --- |
| The program cannot be started, or exits before `ready` | `OL_E_MODULE_LOAD_FAILED` (session diagnostic), event `host.failed` | The module stays; OMSI starts. The native component finds no pipe, gives up after ten seconds and sends no reports; read-ahead, write-behind and read-only open keep working. Nothing is held or learned for this session. A cause is a missing .NET 6 Desktop Runtime (x64), which the program needs. |
| The program is not ready within `prepare_ms` | `OL_E_MODULE_PHASE_TIMEOUT`, event `host.failed` | The module stays and OMSI starts. The program's own limit for loading is five minutes, so reaching this takes a stuck or extremely slow read of the map. |
| The preparation throws | `OL_E_MODULE_OPERATION_FAILED`, detail `prefetch: the preparation failed: <message>`; log line `prepare failed:` | The program still reports ready; the module continues with what it had already held. |
| No map in the session, or a map that cannot be read | Log: `no map in this session...` / `the map ... cannot be read...` | Nothing is held or learned; the native features work. |
| The budget is zero (the automatic budget with less than `reserve_mb` free) | Log: `no memory to hold anything: preparation skipped` | Nothing is held; the native features and the learning work. |
| Free memory falls below the loading floor during the preparation | Log: `prepared in ... (free memory low)` | The preparation ends early; the prediction waits and retries during the session. |
| The session pipe cannot be created | Log: `the session channel cannot be served: ...` | The preloader holds files, but OMSI's native component cannot report to it; nothing is learned and no tile is predicted. |
| The working-set minimum cannot be raised | Log: `the working set minimum was not raised...` | Held pages may be trimmed by Windows under memory pressure. |

## Inside OMSI: the native component

| Situation | Where you see it | What happens |
| --- | --- | --- |
| `CreateFileW`, `ReadFile` or `CloseHandle` cannot be hooked | The `hooks` event (a value other than `0`); `OL_E_MODULE_PHASE_FAILED` ("the native component's start returned 2") | The phase fails and the bridge removes whatever was installed; the module is released inside OMSI and the session continues. |
| The bridge is protocol 1, or its table has no `get_value` | Start returns `1` | The component refuses to start. Not reachable with a bridge that serves protocol 2. |
| Another plugin already changed the same import | The `hooks` event shows `4` for that hook | That hook is refused, never overwritten. For one of the three required hooks, see above; for the others the dependent features stay off. |
| A position hook (`SetFilePointer`, `GetFileSize`, `SetEndOfFile`) or `WriteFile` is refused | The `hooks` event | Read-ahead, write-behind and read-only open all stay off; the use reports still work. |
| A D3DX function is not hooked | The `hooks` event shows `-1` or a code | Textures and meshes loaded by Direct3D itself are not reported as uses; nothing else changes. |
| More than 4096 handles are open at once | `track_full` in `stats` | The extra handles are served natively. |
| A write-behind flush fails twice | `write_behind_failed` in `stats` | Write-behind turns off for new handles. The bytes of that block are lost; OMSI had already been told they were written. The file is a regenerable `.rdy` cache. |
| The session program cannot be reached for ten seconds | `feed_dropped` in `stats` | The native component stops reporting. The three features keep working. |
| OMSI cannot replace a file that is held | `release_requests` in `stats` | A release is requested and the call retried for up to 1.5 s; with no program connected the call fails as it would without the module. |

## During the session

| Situation | What happens |
| --- | --- |
| Free memory falls below 1,024 MB | The guard releases held files OMSI has not used, until about 1,536 MB would be free, and stops loading until more than 2,560 MB is free again. |
| The pipe's queue is full | `U` lines (hints) are dropped and counted; `T` and `W` lines wait up to 50 ms; `X` lines are never queued. |
| A client that is not OMSI connects | It is disconnected and counted (`feed_refused`); log line `feed: refused a client that is not OMSI's process`. |
| A background activity throws | The log says `<activity>: stopped by an unexpected error`; the other activities continue. |
| A save of the learned data fails | Log line `save: ...`; the session continues; the previous data stays. |
| The log cannot be written | The program runs without a log. |

## Stopping

| Situation | What happens |
| --- | --- |
| OmsiLaunch asks the program to stop | It stops its activities (waiting up to six seconds), saves, emits `session` and exits. OmsiLaunch waits `host_stop_ms` (20 s) and then ends it. |
| The program does not stop in time | OmsiLaunch ends it (event `host.killed`); held memory is released with the process; the last periodic save is what remains. |
| OMSI is terminated | See [safety](../concepts/safety.md#what-happens-when-something-goes-wrong): the buffered `.rdy` bytes are lost; nothing else. |

## Next

[Events](events-and-notices.md) · [Known limitations](known-limitations.md) · [Diagnosing slowness](../guides/diagnosing-slowness.md)
