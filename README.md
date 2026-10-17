# omsi-prefetch
OMSI Prefetch: an OmsiLaunch runtime module that prefetches maps and vehicles into memory and speeds up OMSI 2 loading. Module id `omsi-prefetch`.

## Status

Implemented and tested offline; not yet validated in a real OMSI session. Details, settings and the measured results of the proof it comes from are in [prefetch/README.md](prefetch/README.md).

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
