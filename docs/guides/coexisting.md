# Coexisting with other modules and plugins

Omsi Prefetch hooks file functions of `Omsi.exe`, holds memory and reads many files. This page says what that means next to OMSI plugins, other OmsiLaunch modules, other programs and tools that touch your OMSI files, and how to resolve the one declared conflict. The rules of the hook registry, conflicts and activation are those of the OmsiLaunch Runtime Modules reference.

Related pages: [compatibility](../reference/compatibility.md), [errors and degradation](../reference/errors-and-degradation.md), [safety](../concepts/safety.md), [turning it off](turning-off.md).

## The declared conflict: `OmsiLoadingPoC.dll`

The manifest declares `conflicts.plugins` = `OmsiLoadingPoC.dll`, the proof-of-concept plugin this module replaces. It installs hooks on the same file functions of `Omsi.exe`, so the two must not run together.

- At planning, OmsiLaunch reads the `[dll]` section of every `plugins\*.opl`. If one loads `OmsiLoadingPoC.dll`, the module is refused: `OL_E_MODULE_CONFLICT` when you asked for it explicitly (`/module:omsi-prefetch`), and the warning `OL_W_MODULE_NOT_LOADED` otherwise (the session runs without the module). The reason names the `.opl`.
- Inside OMSI, a module whose conflicting DLL is loaded anyway is not loaded.
- OmsiLaunch never changes another plugin's files. To use the module, switch the other plugin off yourself: remove its entry from the `.opl` or move the `.opl` out of `plugins\`.

## Other plugins that hook `Omsi.exe` imports

If another plugin has already patched the same import (`CreateFileW`, `ReadFile`, ...), the bridge refuses the module's hook over it (result `4` in the `hooks` event), because a site changed outside the bridge is never overwritten.

- If it is one of the three required hooks (`CreateFileW`, `ReadFile`, `CloseHandle`), the module's native phase fails and the module is released inside OMSI; the session continues.
- If it is another hook, read-ahead, write-behind and read-only open stay off, and the module keeps learning.

Plugins that only read or write OMSI's files normally are unaffected: they see the same files, and the module never holds a file the game writes.

## Other OmsiLaunch modules

- **Hooks** are recorded in one registry with their owner. Hooks of different modules on the same import stack and are removed last-in first-out. A module that hooks the Direct3D device (for example a HUD) hooks other targets and does not interact with this module's file hooks.
- **Memory.** Every module that holds memory counts against the same RAM. Two modules that each hold gigabytes can together starve the system: the module's loading floor and guard react to the system's free memory, not to another module's share. If you run one, reduce `ram_budget_mb` ([choosing settings by memory size](choosing-settings-by-memory.md)).
- **Session programs.** Each module with a session program runs in its own process and job; OmsiLaunch starts them all before OMSI and waits for all of them to report ready. This module's preparation therefore adds to the others'.
- **Settings.** Settings are per module; another module's settings and flags do not affect this one.

## Programs that touch your OMSI files during a session

A held file is mapped, and Windows does not let a program truncate or replace a mapped file. OMSI is handled (the module releases a file when OMSI cannot replace it). Another program is not:

- an editor that saves a map, a script or a model in place while a session runs may fail with an error about a mapped file;
- a tool that rewrites textures or models in place has the same problem;
- a backup or sync program that only reads is fine.

Close the session before editing, or switch the module off for editing sessions (`/no-module:omsi-prefetch`).

## Antivirus, file-system filters and cloud folders

The module opens and maps many files. Programs that inspect every open apply to it as to any program; whether that makes the preparation slower is a property of those programs. If you see a much longer preparation, check `prepared in` in the log. A cloud-sync folder that places OMSI's files on demand can make the first read of each file slow; keep OMSI on a local disk.

## Windows file cache and RAM disks

The module holds files in its own mapped memory; the Windows file cache is separate. A RAM disk holds the same files again: it does not combine with the module and uses memory the module could use. Prefer giving that memory to `ram_budget_mb`.

## When something conflicts

1. Run the plan with `--json` and read the `Diagnostics` for `omsi-prefetch` ([diagnosing slowness](diagnosing-slowness.md)).
2. Read the `hooks` event: a hook other than `0` shows which import is taken.
3. Switch the module off for a session (`/no-module:omsi-prefetch`) to confirm that it is the cause.
4. Switch off the other plugin or module, or the module's mechanism that conflicts (`read_ahead`, `write_behind`, `read_only_open`).

## Next

[Compatibility](../reference/compatibility.md) · [Errors and degradation](../reference/errors-and-degradation.md) · [Diagnosing slowness](diagnosing-slowness.md)
