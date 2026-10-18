# Using it with predefined session profiles

An OmsiLaunch session profile is a YAML package that fixes a map, an entrypoint and up to 99 presets of settings. A preset can carry the settings of Omsi Prefetch, so that each map or way of playing starts with the values that suit it: a small budget for a light session, a large one with your own vehicles for a long one, or the module switched off. This page shows how to write them, how they combine with the command line and `modules.ini`, and which mistakes OmsiLaunch rejects. The profile format itself is in the OmsiLaunch session-profile reference (`docs\reference\session-profiles.md` of OmsiLaunch).

Related pages: [settings reference](../reference/settings.md), [command-line overrides](command-line-overrides.md), [turning it off](turning-off.md), [choosing settings by memory size](choosing-settings-by-memory.md).

## The `modules` block of a preset

A preset has an optional `modules` mapping, one entry per module id. For this module the entry is `omsi-prefetch`, with two keys:

| Key | Meaning |
| --- | --- |
| `enabled` | `true` requests the module for the session; `false` keeps it out (also when it would join by default). Not set: the module follows its activation (`default`: it joins). |
| `settings` | The module's settings by key, with the values of the [settings reference](../reference/settings.md). Checked against the manifest when the session is planned. |

```yaml
modules:
  omsi-prefetch:
    settings:
      ram_budget_mb: 8192
      user_vehicles: list
      user_vehicle_list: "Bus1;Bus2"
      user_vehicle_scope: load
      monitor: false
```

Values may be written as YAML numbers, booleans or strings; OmsiLaunch converts them to the module's settings the same way as for the command line, and refuses a value outside the manifest's bounds when the session is planned (`OL_E_MODULE_SETTING_INVALID`). A setting key must match `[a-z][a-z0-9_]{0,31}` and the module id `[a-z0-9][a-z0-9-]{1,47}`, or the profile is invalid (`OL_E_SESSION_PROFILE_INVALID`). If the module is not installed, `enabled: true` is reported when the session is planned (`OL_E_MODULE_NOT_FOUND`) and settings alone are reported with `OL_W_MODULE_SETTING_IGNORED`.

## A complete example

`<OMSI root>\.omsilaunch\session-profiles\grundorf-prefetch\profile.yaml`: two presets of the same map, a light one and a full one. The map and the entrypoint are fixed by the profile; each preset carries its own module settings.

```yaml
schema: omsilaunch.session-profile/v1
id: grundorf-prefetch
name: Grundorf with Omsi Prefetch
author: You
version: "1.0"
compatibility:
  maps:
    - maps\Grundorf\global.cfg
new:
  map: maps\Grundorf\global.cfg
  entrypoint-index: 1
presets:
  - index: 1
    id: light
    name: Light (8 GB)
    modules:
      omsi-prefetch:
        settings:
          ram_budget_mb: 1536
          stop_below_free_mb: 1536
          tile_radius: 4
          ai_vehicles: essential
  - index: 2
    id: full
    name: Full (32 GB)
    modules:
      omsi-prefetch:
        settings:
          ram_budget_mb: 8192
          user_vehicles: list
          user_vehicle_list: "Citaro_G;MAN_Lions_City"
          user_vehicle_scope: load
          monitor: false
```

Start one with:

```text
OmsiLaunch.exe /predefined-profile:grundorf-prefetch /predefined-profile-index:2 /new
```

The profile keys outside the `modules` block (`compatibility`, `new`, `settings`, ...) are defined by the OmsiLaunch session-profile reference; the example only shows where the block goes. The `id` must equal the directory name.

## Switching the module off in a preset

```yaml
    modules:
      omsi-prefetch:
        enabled: false
```

This keeps the module out of sessions started with that preset, including its default activation. See [turning it off](turning-off.md).

## Precedence and conflicts

For each setting the effective value is, in order: the request (a preset **or** the command line, see below), the `[omsi-prefetch]` section of `.omsilaunch\modules.ini`, the default.

- A preset's `modules.omsi-prefetch.settings` count as part of the request, so they win over `modules.ini`.
- The command line cannot also set a key the preset sets: `/module-setting:omsi-prefetch.ram_budget_mb=...` or `/prefetch-ram:...` with a preset that sets `ram_budget_mb` is rejected with `OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT`. A key the preset does not set can still be given on the command line.
- `/module:omsi-prefetch` against a preset with `enabled: false`, and `/no-module:omsi-prefetch` against a preset with `enabled: true`, are rejected the same way.
- A `/spec` file's module settings are joined with the preset's.

## Tips

- Keep one preset per hardware class or way of playing rather than editing values by hand.
- Put values that rarely change (the monitor off, the floors) in `modules.ini` and the ones that depend on the map (budget, vehicles) in the preset.
- Run `OmsiLaunch.exe /predefined-profile:<id> /predefined-profile-index:<n> /new /plan --json` to check a preset: the `module.planned` diagnostic and the absence of `OL_E_MODULE_SETTING_INVALID` confirm it.

## Next

[Command-line overrides](command-line-overrides.md) · [Turning it off](turning-off.md) · [Settings reference](../reference/settings.md)
