# Installation

This page lists what OMSI Prefetch `1.0.0-beta.1` requires, how the module folder is installed into an OmsiLaunch installation, how to confirm that OmsiLaunch sees the module and will load it, and how to remove or upgrade it. It assumes OmsiLaunch is already installed in the OMSI root as described in the OmsiLaunch installation page. The module is a runtime module in the sense of the OmsiLaunch Runtime Modules reference; the rules there (location, integrity, activation) are the rules of this page.

Related pages: [overview](overview.md), [first run](first-run.md), [turning it off](../guides/turning-off.md), [compatibility](../reference/compatibility.md), [errors and degradation](../reference/errors-and-degradation.md), [files and directories](../reference/files-and-directories.md).

## Requirements

| Requirement | Detail | Failure when missing |
| --- | --- | --- |
| OmsiLaunch `0.1.0-beta4` or later | The module declares `bridge_protocol` `{ "min": 2, "max": 2 }` and the capabilities `bridge.events` and `bridge.hooks.iat`. Any OmsiLaunch that serves bridge protocol 2 can load it. | Not listed as usable: `OL_E_MODULE_PROTOCOL_UNSUPPORTED` (or `OL_E_MODULE_CAPABILITY_UNRESOLVED`). |
| Windows 10 or later, 64-bit | The same platform OmsiLaunch requires. The session program is x64. | OmsiLaunch itself refuses the platform. |
| .NET 6 Desktop Runtime, **x64** | `Prefetch.Preloader.exe` is a framework-dependent `net6.0-windows` program (Windows Forms is used by its two optional windows). OmsiLaunch's controller needs the same runtime, so it is already present when OmsiLaunch runs. | OmsiLaunch starts the program and it exits before it is ready: `OL_E_MODULE_LOAD_FAILED` (see [errors and degradation](../reference/errors-and-degradation.md)). |
| .NET 6 Runtime, **x86** | `Prefetch.Module.dll` (`net6.0`, AnyCPU) is loaded inside the 32-bit `Omsi.exe` by the OmsiLaunch plugin, which needs the x86 runtime anyway. | The OmsiLaunch plugin does not start. |
| OMSI 2 | The native component hooks imports of `Omsi.exe` by name; it carries no OMSI address. See [compatibility](../reference/compatibility.md). | Per hook, see [errors and degradation](../reference/errors-and-degradation.md). |
| Free memory | The module holds files in RAM up to its budget ([memory and budget](../concepts/memory-and-budget.md)). With the automatic budget it holds nothing when less than `reserve_mb` (4096 MB) is free. | The module still runs, without holding anything ([guide by memory size](../guides/choosing-settings-by-memory.md)). |
| Disk space | Its own data is bounded (256 MB by default, see [files and directories](../reference/files-and-directories.md)). | None; the module skips what it cannot write. |
| No `OmsiLoadingPoC.dll` plugin | The proof of concept this module replaces. It is declared as a conflict. | `OL_W_MODULE_NOT_LOADED` / `OL_E_MODULE_CONFLICT`; see [coexisting](../guides/coexisting.md). |

## What the module folder contains

A module is a folder named after its id. For this module it holds exactly these seven files:

| File | Content |
| --- | --- |
| `module.json` | The manifest (`omsilaunch.runtime-module/v1`): id, version, the 29 settings, the conflict, the failure policy and the SHA-256 pin of each component. |
| `Prefetch.Module.dll` | Managed component, loaded inside OMSI. |
| `prefetch-native.dll` | Native component (x86), hooks inside OMSI. |
| `Prefetch.Preloader.exe` | Session program (x64), started before OMSI. |
| `NOTICE.txt` | Copyright and license notice. |
| `GPL-3.0.txt`, `LGPL-3.0.txt` | The license texts. |

## Install

1. Close OMSI and make sure no OmsiLaunch session is running (`OmsiLaunch.exe /recovery-status` must not report a pending recovery).
2. Copy the whole `omsi-prefetch` folder into the module directory of the installation, so that the manifest ends up at:

   ```text
   <OMSI root>\plugins\OmsiLaunch.Modules\omsi-prefetch\module.json
   ```

   Create `plugins\OmsiLaunch.Modules\` if it does not exist. The folder name must equal the module id, `omsi-prefetch`.
3. Do not rename, move or edit any file of the folder, do not replace one of its files by a link or a junction, and do not copy the files one by one from different builds. Every component is pinned by its SHA-256 in `module.json`, and OmsiLaunch refuses a module whose files do not match (`OL_E_MODULE_INTEGRITY`). A file reached through a junction or symbolic link is also refused.

OmsiLaunch never copies a module for you, and a session never writes to `plugins\OmsiLaunch.Modules\`.

### Building the folder from source

The repository builds against an OmsiLaunch checkout (bridge protocol 2 or later) that must be built first. `Directory.Build.props` points `OmsiLaunchRoot` at `..\OmsiLaunch-spec-pack\OmsiLaunch`; override it with `-p:OmsiLaunchRoot=...` or the `-OmsiLaunchRoot` parameter of the script.

```text
powershell -File tools\Build-Modules.ps1
```

The script builds the three components (the native one with MSBuild and the Visual Studio C++ build tools, x86), publishes the preloader as one framework-dependent file, assembles `artifacts\modules\omsi-prefetch\` with `module.json` pinned, and runs the offline tests (see [test coverage](../status/test-coverage.md)); `-SkipTests` skips the tests. It never copies anything into an OMSI installation: copying the assembled folder is step 2 above.

## Verify

Run from the OMSI root.

```text
OmsiLaunch.exe /list:RuntimeModules
```

Expected: an entry with id `omsi-prefetch`, version `1.0.0-beta.1`, activation `default`, valid, and its settings with type, default and flag (for example `ram_budget_mb` with the flag `prefetch-ram`). An invalid module is listed with its error code instead (`OL_E_MODULE_INTEGRITY`, `OL_E_MODULE_MANIFEST_INVALID`).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /plan --json
```

Expected: in `Diagnostics`, an entry `module.planned` whose data names `module` `omsi-prefetch` and `activation` `default`, and `IsRunnable` true. Use any map of your installation (`/list:Maps`). Planning never starts OMSI and never writes to the installation. These diagnostics tell you the module will take part:

| Plan diagnostic | Meaning |
| --- | --- |
| `module.planned` | The module joins the session. |
| `module.disabled` | The module is kept out by `/no-module:omsi-prefetch`, a preset or `enabled=false` in `.omsilaunch\modules.ini`. |
| `OL_W_MODULE_NOT_LOADED` | The module would join by default but cannot be loaded (its `Data["code"]` names why). The session still runs, without the module. |
| `OL_W_MODULE_SETTING_IGNORED` | An entry of `.omsilaunch\modules.ini` for this module is unusable and was ignored. |
| `OL_E_MODULE_SETTING_INVALID` | A setting or flag you requested is not valid for the module (the plan is not runnable). |

To confirm that the module is active in a real session, start one and look for any of these:

- `OmsiLaunch.exe events read` (from a second console) lists `module.omsi-prefetch.host.started`, `module.omsi-prefetch.host.ready`, `module.omsi-prefetch.prepared`, `module.omsi-prefetch.armed` and `module.omsi-prefetch.hooks` ([events](../reference/events-and-notices.md)).
- A log file `preloader-<date>-<time>.log` appears in `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\logs\` with the first line `session <id>, module 1.0.0-beta.1, OmsiLaunch <version>` ([log format](../reference/log-format.md)).
- With `/prefetch-monitor` the monitor window opens ([first run](first-run.md)).

## Upgrade

Replace the whole `omsi-prefetch` folder with the new one while no session is running. Mixing files of two versions fails the integrity check. The data the module keeps for your maps ([learned data](../concepts/learned-data.md)) stays valid across upgrades as long as its formats are unchanged; delete it with `/clear-module-data:omsi-prefetch` if you want to start from nothing.

## Remove

Delete `<OMSI root>\plugins\OmsiLaunch.Modules\omsi-prefetch\`. The data the module wrote stays in `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\` until you run `OmsiLaunch.exe /clear-module-data:omsi-prefetch` (before or after removing the folder) or delete that folder. Removing the module changes nothing else: it never modified an OMSI file.

## Next

[First run](first-run.md) · [Measuring the gain](measuring-the-gain.md) · [Choosing settings by memory size](../guides/choosing-settings-by-memory.md)
