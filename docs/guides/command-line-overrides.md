# Per-session overrides on the command line

You can change any setting of OMSI Prefetch for one session without touching `modules.ini` or a profile. This page lists the module's own flags, the generic `/module-setting` form that reaches every setting, how values are written and quoted, the precedence rules, how to check the result with `/plan`, and the errors you can meet. Settings themselves are described in the [settings reference](../reference/settings.md).

Related pages: [settings reference](../reference/settings.md), [session profiles](session-profiles.md), [turning it off](turning-off.md), [choosing settings by memory size](choosing-settings-by-memory.md).

## The module's own flags

Seven settings have a flag. A flag is written `/<flag>:<value>`; for a boolean setting the flag alone means `true`.

| Flag | Setting | Example |
| --- | --- | --- |
| `/prefetch-ram:<MB>` | `ram_budget_mb` (also `auto`) | `/prefetch-ram:6144` |
| `/prefetch-reserve:<MB>` | `reserve_mb` | `/prefetch-reserve:6144` |
| `/prefetch-stop-below:<MB>` | `stop_below_free_mb` (also `auto`) | `/prefetch-stop-below:3072` |
| `/prefetch-vehicle-ram:<MB>` | `vehicle_budget_mb` | `/prefetch-vehicle-ram:1024` |
| `/prefetch-monitor` | `monitor` | `/prefetch-monitor` or `/prefetch-monitor:false` |
| `/prefetch-pre-open:<mode>` | `pre_open` (`none`, `learned`, `all`, `list`) | `/prefetch-pre-open:none` |
| `/prefetch-debug` | `debug_log` | `/prefetch-debug` |

A module flag is resolved when OmsiLaunch plans a session, from the flags of the installed modules (`/list:RuntimeModules` shows them). It is the same as `/module-setting:omsi-prefetch.<key>=<value>`. A flag that no installed module declares, or one used outside a launch, is `Unknown argument` (exit `2`); a flag declared by two modules is `OL_E_MODULE_SETTING_INVALID`. A module flag never replaces a built-in OmsiLaunch flag.

## Every other setting

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.tile_radius=8
```

`/module-setting:<module id>.<key>=<value>` can be repeated, once per setting. The form is exact: without the module id and a dot before the key, OmsiLaunch answers `/module-setting requires <module-id>.<key>=<value>` (exit `2`).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-ram:6144 /module-setting:omsi-prefetch.ai_vehicles=load /module-setting:omsi-prefetch.tile_radius=8 /prefetch-monitor
```

## Writing values

- **Booleans**: `true`/`false`, `yes`/`no`, `on`/`off`, `1`/`0`.
- **Integers**: plain digits, within the bounds of the [settings table](../reference/settings.md#summary). `auto` is accepted by `ram_budget_mb` and `stop_below_free_mb`.
- **Choices**: matched without regard to case.
- **Lists** (`user_vehicle_list`, `pre_open_list`, `read_only_folders`): entries separated by `;`, in the order you want them. A semicolon ends a command in PowerShell, so quote the argument:

  ```powershell
  OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 '/module-setting:omsi-prefetch.user_vehicles=list' '/module-setting:omsi-prefetch.user_vehicle_list=Citaro_G;MAN_Lions_City'
  ```

  In `cmd.exe` put double quotes around the whole argument: `"/module-setting:omsi-prefetch.user_vehicle_list=Citaro_G;MAN_Lions_City"`. Do not end a quoted argument with a backslash (Windows turns `\"` into a quote); set `read_only_folders` in `modules.ini` or a preset.

## Precedence

For each setting: the command line (or a preset), then `.omsilaunch\modules.ini`, then the default. Details:

- The command line wins over `modules.ini` and over a `/spec` file.
- A key that the selected session-profile preset sets cannot also be given on the command line: `OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT` (exit `2`). Keys the preset does not set can ([session profiles](session-profiles.md)).
- Settings for a module the session does not load (for example together with `/no-module:omsi-prefetch`) are reported with `OL_W_MODULE_SETTING_IGNORED` and do nothing.

## Checking before you play

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-ram:6144 /plan --json
```

`/plan` starts nothing. In the output, look in `Diagnostics` for `module.planned` for `omsi-prefetch` and for the absence of `OL_E_MODULE_SETTING_INVALID`. The plan lists the effective value of every setting and where it came from (`request`, `settings-file` or `default`).

## Errors

| You see | Cause |
| --- | --- |
| `Unknown argument: /prefetch-...` | The module is not installed, or the flag is misspelled. |
| `/module-setting requires <module-id>.<key>=<value>` | The form is wrong. |
| `OL_E_MODULE_SETTING_INVALID` | A value outside the bounds, a choice that does not exist, or a key the module does not declare. The message names the setting and the reason. |
| `OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT` | The preset already sets that key. |

## Next

[Session profiles](session-profiles.md) · [Turning it off](turning-off.md) · [Settings reference](../reference/settings.md)
