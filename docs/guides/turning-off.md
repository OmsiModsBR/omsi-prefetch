# Turning it off

You can keep OMSI Prefetch out of one session, out of a profile, out of every session, or switch off only some of its mechanisms. This page lists each way, in order from the lightest to the most permanent, with the effect on the plan so that you can check it. Which way wins when they disagree is stated at the end.

Related pages: [command-line overrides](command-line-overrides.md), [session profiles](session-profiles.md), [clearing data](clearing-data.md), [installation](../getting-started/installation.md).

## For one session

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /no-module:omsi-prefetch
```

`/no-module:omsi-prefetch` keeps the module out of that session, including the module loaded by default. The plan shows the diagnostic `module.disabled` for `omsi-prefetch` (check with `/plan --json`). Use it for the "without the module" half of a comparison ([measuring the gain](../getting-started/measuring-the-gain.md)). Nothing is started, held or written by the module in that session; its data from earlier sessions stays.

## For a profile or a preset

In the preset of an OmsiLaunch session profile:

```yaml
    modules:
      omsi-prefetch:
        enabled: false
```

Sessions started with that preset do not load the module ([session profiles](session-profiles.md)). The command line cannot contradict it: `/module:omsi-prefetch` with that preset is `OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT`.

## For every session

Edit `<OMSI root>\.omsilaunch\modules.ini` and add:

```ini
[omsi-prefetch]
enabled=false
```

(`true`/`false`, `yes`/`no`, `on`/`off`, `1`/`0` are accepted for `enabled`.) OmsiLaunch only reads this file. To bring the module back, set `enabled=true` or delete the line.

## Permanently

Remove the module folder `<OMSI root>\plugins\OmsiLaunch.Modules\omsi-prefetch\`. Nothing else has to change: the module never modified an OMSI file. Delete what it learned with `OmsiLaunch.exe /clear-module-data:omsi-prefetch` ([clearing data](clearing-data.md)).

Renaming the folder does not work: the folder name must equal the module id, so a renamed folder is reported as an invalid module (`OL_E_MODULE_MANIFEST_INVALID`). Move the folder out of `plugins\OmsiLaunch.Modules\` instead.

## Only some mechanisms

The module can stay installed while one of its mechanisms is off. Each of these is a setting ([settings reference](../reference/settings.md)):

| To switch off | Setting |
| --- | --- |
| The holding of files in memory | Not switchable on its own: the smallest budget is `ram_budget_mb=256`. Combine it with `ai_vehicles=off`, `survey=false`, `user_vehicles=none` and `pre_open=none` to hold almost nothing (an existing learned list is still brought in until the budget is spent), or use `/no-module:omsi-prefetch`. |
| Read-ahead of OMSI's small reads | `read_ahead=false` |
| Write-behind of the terrain cache | `write_behind=false` |
| Read-only open of assets | `read_only_open=false` |
| Tile anticipation (tile index, start tiles, prediction) | `survey=false` |
| Learning | `learn=false` (an existing learned list is still used; to ignore it, clear the data) |
| Pre-open of vehicle files | `pre_open=none` and `pre_open_on_first_use=false` |
| The user's vehicles | `user_vehicles=none` |
| The AI vehicles | `ai_vehicles=off` |
| The first-open window | `first_open_notice=false` |

With `read_ahead`, `write_behind` and `read_only_open` all off, the native component only sends its use reports ([native shim](../concepts/native-shim.md)).

## When they disagree

Highest priority first:

1. An explicit request on the command line or in the selected preset: `/module:omsi-prefetch` or `modules.omsi-prefetch.enabled: true` request the module, `/no-module:omsi-prefetch` or `enabled: false` keep it out. A conflict between the command line and a preset is rejected (`OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT`); enabling and disabling it in one request is `OL_E_MODULE_SETTING_INVALID`.
2. `enabled=` in `.omsilaunch\modules.ini`. An explicit request wins over the file.
3. The module's activation: `default`, so it joins every session.

## Next

[Clearing data](clearing-data.md) · [Command-line overrides](command-line-overrides.md) · [Session profiles](session-profiles.md)
