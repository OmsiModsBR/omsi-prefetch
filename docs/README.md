# Omsi Prefetch Documentation

English | [Português (Brasil)](pt-BR/README.md)

This is the English documentation for Omsi Prefetch `1.0.0-beta.1` (module id `omsi-prefetch`), an OmsiLaunch runtime module that prefetches maps and vehicles into memory and speeds up the loading of OMSI 2. Every page describes what the current code does; when a page and the code disagree, the code wins and the page is a bug. The pages mirror the structure of the OmsiLaunch documentation: getting started, concepts, guides, reference and status.

The module is a runtime module of OmsiLaunch `0.1.0-beta4` or later. OmsiLaunch's own documentation (its Runtime Modules reference, CLI reference and session-profile reference) is the normative source for everything the module relies on from OmsiLaunch: where modules live, integrity, activation, flags, `modules.ini`, presets. These pages link to it by name and repeat only what a user of this module needs.

Vocabulary used throughout: the module is a beta (`1.0.0-beta.1`); the runtime-module feature of OmsiLaunch is `EXPERIMENTAL`. Results quoted from the loading optimization study of 30/09/2026 are those of the repository README (a single machine and a single map, n = 1) and are taken as valid for the module.

## Who reads what

| Audience | Start here | Then |
| --- | --- | --- |
| Players who want faster loading | [Overview](getting-started/overview.md), [Installation](getting-started/installation.md), [First run](getting-started/first-run.md) | [Choosing settings by memory size](guides/choosing-settings-by-memory.md), [Measuring the gain](getting-started/measuring-the-gain.md), [FAQ](reference/faq.md) |
| Players who tune or use profiles | [Settings](reference/settings.md), [Session profiles](guides/session-profiles.md) | [Command-line overrides](guides/command-line-overrides.md), [Turning it off](guides/turning-off.md), [Clearing data](guides/clearing-data.md) |
| Players who have a problem | [Diagnosing slowness](guides/diagnosing-slowness.md) | [Log format](reference/log-format.md), [Errors and degradation](reference/errors-and-degradation.md), [Coexisting](guides/coexisting.md) |
| Map and add-on authors, integrators | [How prefetching works](concepts/how-it-works.md), [Files and directories](reference/files-and-directories.md) | [Events](reference/events-and-notices.md), [Compatibility](reference/compatibility.md), [Session profiles](guides/session-profiles.md) |
| Maintainers | [Safety](concepts/safety.md), [Known limitations](reference/known-limitations.md) | [Test coverage](status/test-coverage.md), [Changelog](../CHANGELOG.md) |

## Navigation

### Getting started

| Page | Purpose |
| --- | --- |
| [Overview](getting-started/overview.md) | What the module is, what it does for you, what it does not do, the measured results and its three parts. |
| [Installation](getting-started/installation.md) | Requirements, installing the module folder into an OmsiLaunch installation, verifying that it is active, upgrading and removing. |
| [First run](getting-started/first-run.md) | What happens before OMSI opens, what the first-open window means, what the first session teaches the module, the second run. |
| [Measuring the gain](getting-started/measuring-the-gain.md) | How to compare with and without the module fairly and which numbers the module gives you. |

### Concepts

| Page | Purpose |
| --- | --- |
| [How prefetching works](concepts/how-it-works.md) | The three components, how they connect and a session from start to end. |
| [The session program](concepts/session-program.md) | What `Prefetch.Preloader.exe` does before and while OMSI runs: preparation, survey, prediction, vehicles, pre-open, guard, saving, stopping. |
| [The native shim](concepts/native-shim.md) | Read-ahead, write-behind, read-only open and the use reports inside OMSI, with their conditions, guarantees and limits. |
| [Learned data](concepts/learned-data.md) | The learned list, the written list and the tile index: what they hold, how they are built, how they go stale and how they are bounded. |
| [Memory, the budget and the floors](concepts/memory-and-budget.md) | What "held in memory" means, the automatic budget, the loading floor, the guard and the order in which the budget is spent. |
| [Vehicles and scopes](concepts/vehicle-scopes.md) | `essential`, `load` and `complete`; the map's AI vehicles, the user's vehicles and the fleet. |
| [Pre-open](concepts/pre-open.md) | Opening vehicle files ahead of the game without holding them, planned and on first use. |
| [Safety](concepts/safety.md) | What the module reads and writes, what it never holds, how it stays out of OMSI's way, and what happens when something stops unexpectedly. |

### Guides

| Page | Purpose |
| --- | --- |
| [Choosing settings by memory size](guides/choosing-settings-by-memory.md) | Starting values for 8, 16, 32 and 64 GB machines and how to adjust from the log. |
| [Using it with predefined session profiles](guides/session-profiles.md) | Carrying the module's settings, or switching it off, in an OmsiLaunch session-profile preset. |
| [Per-session overrides on the command line](guides/command-line-overrides.md) | The module's flags, `/module-setting`, quoting, precedence and checking with `/plan`. |
| [Turning it off](guides/turning-off.md) | For one session, for a profile, for every session, permanently, or only some mechanisms. |
| [Clearing the module's data](guides/clearing-data.md) | `/clear-module-data`, clearing one map, what happens next. |
| [Diagnosing slowness](guides/diagnosing-slowness.md) | Plan, log, events, debug log and monitor: finding why a session is slow or why the module did not help. |
| [Coexisting with other modules and plugins](guides/coexisting.md) | The `OmsiLoadingPoC.dll` conflict, other hooks, memory shared between modules, tools that edit files. |

### Reference

| Page | Purpose |
| --- | --- |
| [Settings](reference/settings.md) | Every setting with type, default, bounds, flag, effect, interactions and an example. |
| [Files and directories](reference/files-and-directories.md) | What the module reads and writes, the formats of its data, the bounds on disk and in memory. |
| [Log format](reference/log-format.md) | The log file, its limits and every message. |
| [Events and notices](reference/events-and-notices.md) | Every `module.omsi-prefetch.*` event with its data, and what the module does not emit. |
| [Errors and degradation](reference/errors-and-degradation.md) | What can go wrong, the OmsiLaunch codes it shows, and what the module does about it. |
| [Compatibility](reference/compatibility.md) | OmsiLaunch, Windows, .NET, OMSI 2 builds, maps, other plugins and languages. |
| [Known limitations](reference/known-limitations.md) | What the module does not do, does partly or does at an accepted risk, and the differences found between README, manifest and code. |
| [FAQ](reference/faq.md) | Short answers to common questions. |

### Status

| Page | Purpose |
| --- | --- |
| [Test coverage](status/test-coverage.md) | The offline gates by name, the native scenarios, what they do not cover, and the documentation check. |
| [Changelog](../CHANGELOG.md) | What `1.0.0-beta.1` contains. |

## How this documentation is kept in sync

`tools\Check-Docs.ps1` runs from the repository root and checks that every relative link and anchor in `docs\**\*.md`, `README.md`, `prefetch\README.md` and `CHANGELOG.md` resolves, that every setting and flag of `prefetch\module.template.json` is documented in the [settings reference](reference/settings.md) with its default and bounds, and that every page is listed in the navigation above. It does not check prose, so a page can still be wrong about behavior; report that as a bug against the page.

## License

Copyright © 2026 LMonteiro - github.com/lmonteirotech and OmsiModsBR Team - github.com/OmsiModsBR. Omsi Prefetch is free software under the GNU General Public License, version 3; it uses OmsiLaunch, which is licensed under the GNU Lesser General Public License, version 3 only. See the repository `LICENSE` and `prefetch\NOTICE.txt`.
