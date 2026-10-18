# Clearing the module's data

Omsi Prefetch keeps what it learned about each map in `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\`. You never have to clean it: it is bounded and the least recently used maps are removed by themselves. You may want to clear it to start a map from nothing, after a large change to a map, to free the space, or before removing the module. This page shows how, what is deleted, what the effect is and when to do it.

Related pages: [learned data](../concepts/learned-data.md), [files and directories](../reference/files-and-directories.md), [turning it off](turning-off.md), [first run](../getting-started/first-run.md).

## Everything at once

With no session running:

```text
OmsiLaunch.exe /clear-module-data:omsi-prefetch
```

OmsiLaunch deletes `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\` and nothing else, and answers with `module-data.clear`: `module` and `cleared` (`false` when there was nothing to delete); exit `0`. It takes the installation lease: while a session owns the installation it answers `OL_E_INSTALLATION_BUSY` (exit `7`). A data folder reached through a link is refused (`OL_E_MODULE_INTEGRITY`), and a link inside it is removed as a link, never followed. It works whether or not the module is installed.

This removes the learned lists, the written lists, the tile indexes and the logs of every map.

## One map

Delete that map's folder yourself, with no session running:

```text
<OMSI root>\.omsilaunch\module-data\omsi-prefetch\maps\<folder>-<hash>\
```

`<folder>` is the map's folder name and `<hash>` the first eight hex digits of the SHA-256 of `maps\<folder>\global.cfg` in lower case ([files and directories](../reference/files-and-directories.md#folder-names-of-a-map)); the folder name is enough to find it. To clear only the tile index, delete `tiles.idx`; to forget what was used, delete `learned.txt`; to forget the files the game wrote, delete `written.txt`. The module recreates what it needs.

## What happens next

The next session of the map is a first open ([first run](../getting-started/first-run.md)): the learned list is empty, the tile index is recomputed, and the first-open window appears. The session after it starts from what that session learned.

## When to clear

| Situation | Clear? |
| --- | --- |
| You want to compare "first open" and "second open" on a map | Yes, that map ([measuring the gain](../getting-started/measuring-the-gain.md)). |
| You rebuilt a map's objects or textures without changing its tile files | The tile index does not notice this ([learned data](../concepts/learned-data.md)); delete that map's `tiles.idx`. |
| The learned list grew from a route you will not drive again | Delete that map's `learned.txt`. |
| You reinstalled or moved your OMSI folder | No: the data is below the folder and keeps its relative paths. |
| You upgrade the module | Not needed. |
| You remove the module | Yes, to free the space ([turning it off](turning-off.md)). |
| The data seems to be damaged | Yes. The module ignores a file it cannot parse and rebuilds it. |

## You do not need to

- Keep the folder small: `data_limit_mb` (default 256 MB) removes the maps used least recently when a session starts ([settings](../reference/settings.md#data_limit_mb)).
- Rotate logs: at most three, of at most 1 MiB, are kept ([log format](../reference/log-format.md)).

## Next

[Diagnosing slowness](diagnosing-slowness.md) · [Learned data](../concepts/learned-data.md) · [Turning it off](turning-off.md)
