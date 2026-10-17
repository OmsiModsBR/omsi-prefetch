<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/logo-dark.svg">
    <img src="assets/brand/logo-light.svg" alt="OmsiLaunch Omsi Prefetch" height="96">
  </picture>
</p>

# omsi-prefetch
OMSI Prefetch: an OmsiLaunch runtime module that prefetches maps and vehicles into memory and speeds up OMSI 2 loading. Module id `omsi-prefetch`.

## Results

The module packages the mechanisms of the loading optimization study of 30/09/2026 on the RMG Leste map: the map manifest learned from earlier sessions, tile anticipation, a vehicle cache, read-ahead of OMSI's small reads and write-behind of its terrain-cache writes. The comparison below is the one that isolates them: the same optimized map package, first with the plugin only observing and then acting, the operating system's file cache emptied before the run with the mechanisms on (study runs R2-A and R3-A). These results are taken as valid for the module.

| Indicator | Without | With | Change |
| --- | ---: | ---: | ---: |
| Until traffic is loaded, after "Loading Situation" | 190 s | 51 s | 3.7x faster |
| Until the game is usable (first 60 s without a freeze of 1 s or more) | 265 s | 71 s | 3.7x faster |
| Time frozen (frames of 1 s or more) | 75.0 s | 17.9 s | 4.2x less |
| Longest freeze | 35.4 s | 7.2 s | 4.9x less |
| "Loading Tile" step, average per cycle | 1538 ms | 382 ms | 4.0x faster |
| "Updating Terrain" step, average per cycle | 3744 ms | 549 ms | 6.8x faster |
| Time inside `ReadFile`, all threads | 146.6 s | 0.8 s | 183x less |
| Time inside `WriteFile`, all threads | 22.4 s | 3.8 s | 5.9x less |

The gain comes from the cost of tens of millions of tiny system calls (39.8 to 48.8 million `ReadFile` calls of a few bytes per session), not from the speed of the disk. What does **not** change: the median frame time (about 52 ms, 19 to 20 frames per second) and the parts that are pure game CPU ("Refresh Schedules", about 2.1 to 2.2 s per cycle). The study is a single machine and a single map (n = 1); results on other hardware and maps will differ.

## Status

Implemented, with its offline tests passing. Details, settings and the files the module reads and writes are in [prefetch/README.md](prefetch/README.md).

## Building

The module builds against an OmsiLaunch checkout (bridge protocol 2 or later): `Directory.Build.props` points `OmsiLaunchRoot` at `..\OmsiLaunch-spec-pack\OmsiLaunch` (override with `-p:OmsiLaunchRoot=...`), which must be built first.

```text
powershell -File tools\Build-Modules.ps1
```

The script builds the module, runs its tests and lays it out as it is installed: `artifacts\modules\omsi-prefetch\` with its `module.json`.

## License

Copyright © 2026 Leo Monteiro / Portal OmsiModsBR - https://github.com/OmsiModsBR

omsi-prefetch is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License, version 3, as published by the Free Software Foundation. See [LICENSE](LICENSE).

It uses OmsiLaunch, which is licensed under the GNU Lesser General Public License, version 3 only; see [prefetch/NOTICE.txt](prefetch/NOTICE.txt).
