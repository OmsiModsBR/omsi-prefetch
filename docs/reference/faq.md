# FAQ

Short answers to the questions users ask most about OMSI Prefetch. Each answer points to the page that has the detail.

Related pages: [overview](../getting-started/overview.md), [known limitations](known-limitations.md), [diagnosing slowness](../guides/diagnosing-slowness.md), [settings](settings.md).

## Does it change my OMSI files?

No. It opens files for reading and writes only below `.omsilaunch\module-data\omsi-prefetch\`. The one thing it does with OMSI's own writes is group the byte-by-byte writes of the terrain cache (`.rdy`) into blocks; the bytes are OMSI's, written later and together ([safety](../concepts/safety.md)).

## Does it raise my frame rate?

No. It shortens loading and the freezes caused by loading. The median frame time and the game's CPU work do not change ([overview](../getting-started/overview.md#what-you-get)).

## Why does OMSI open later than before?

OmsiLaunch starts OMSI only after the module has reported that its preparation is finished. That wait is what lets OMSI find its files in memory. It is longest on the first open of a map and shorter afterwards ([first run](../getting-started/first-run.md)).

## What is the small window that appears before OMSI?

The first-open window. It appears the first time a map is requested (or after its data was cleared) and says that the module is reading the map and learning. It closes by itself. Switch it off with `first_open_notice=false` ([first run](../getting-started/first-run.md#the-first-open-window)).

## Why is the first session not faster?

The module learns what the game uses while you play. The first session of a map pays for that; the next ones start from what you really use ([learned data](../concepts/learned-data.md)).

## Do I need to configure anything?

No. The defaults work on machines with enough free memory. On a small machine, or when you want to tune it, see [choosing settings by memory size](../guides/choosing-settings-by-memory.md).

## How much RAM does it use?

By default 70 % of the free memory at the start after keeping 4 GB, at most 12 GB. On a machine with 4 GB or less free it holds nothing. You can set an explicit amount with `ram_budget_mb` ([memory and budget](../concepts/memory-and-budget.md)).

## Can I use it with 8 GB of RAM?

Yes, with an explicit budget (for example 1,536 MB) and a small footprint. The automatic budget is usually zero on 8 GB because less than 4 GB is free ([profiles by memory size](../guides/choosing-settings-by-memory.md#8-gb)).

## Why does Task Manager show a large `Prefetch.Preloader.exe`?

That is the held memory: the files are mapped in that process. It is released when the session ends, and Windows can take it back if the system needs it ([memory and budget](../concepts/memory-and-budget.md)).

## Does it send anything over the Internet?

No. The module has no network code and sends nothing outside the machine.

## What happens if OMSI crashes or I end the session from the tray?

Nothing happens to your files. The session program saves what it learned and exits. The only loss is the part of the terrain cache (`.rdy`) that was still in a write-behind buffer; OMSI rebuilds it ([safety](../concepts/safety.md#what-happens-when-something-goes-wrong)). To avoid even that, leave OMSI through its own exit.

## Can I turn it off?

For one session: `/no-module:omsi-prefetch`. Permanently: `enabled=false` in `.omsilaunch\modules.ini`, or remove the module folder ([turning it off](../guides/turning-off.md)).

## Can I set it per map or per way of playing?

Yes, with an OmsiLaunch session-profile preset that carries `modules.omsi-prefetch.settings` ([session profiles](../guides/session-profiles.md)).

## Where are its settings?

There is no file of the module's own. Settings go through OmsiLaunch: its flags (`/prefetch-ram:6144`), `/module-setting:omsi-prefetch.<key>=<value>`, `.omsilaunch\modules.ini` or a preset ([settings](settings.md)).

## It says the module is not loaded. Why?

Read the plan: `OmsiLaunch.exe ... /plan --json`. The usual reasons are an altered module folder (`OL_E_MODULE_INTEGRITY`), the plugin `OmsiLoadingPoC.dll` being switched on (`OL_E_MODULE_CONFLICT`), or the module being switched off ([errors and degradation](errors-and-degradation.md)).

## Does it work without OmsiLaunch?

No. It is a runtime module of OmsiLaunch: OmsiLaunch starts its session program before OMSI and its bridge loads the in-game components.

## How do I see what it holds?

Start the session with `/prefetch-monitor` for a live window, or read the log ([log format](log-format.md)).

## How do I reset what it learned?

`OmsiLaunch.exe /clear-module-data:omsi-prefetch` while no session is running ([clearing data](../guides/clearing-data.md)).

## Why is a window in Portuguese?

The module's two windows use Portuguese when the Windows display language is Portuguese and English otherwise.

## Which license is it under?

GNU General Public License, version 3. It uses OmsiLaunch, which is under the GNU Lesser General Public License, version 3 only. See `NOTICE.txt` in the module folder.

## Next

[Known limitations](known-limitations.md) · [Diagnosing slowness](../guides/diagnosing-slowness.md) · [Overview](../getting-started/overview.md)
