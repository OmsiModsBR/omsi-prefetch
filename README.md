<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/brand/logo-dark.svg">
    <img src="assets/brand/logo-light.svg" alt="OmsiLaunch Omsi Prefetch" height="96">
  </picture>
</p>

<div align="center">

[![License: GPL v3](https://img.shields.io/badge/license-GPL--3.0-blue)](LICENSE)
[![Release](https://img.shields.io/github/v/release/OmsiModsBR/omsi-prefetch?include_prereleases&label=release&sort=semver)](https://github.com/OmsiModsBR/omsi-prefetch/releases)
[![Last commit](https://img.shields.io/github/last-commit/OmsiModsBR/omsi-prefetch)](https://github.com/OmsiModsBR/omsi-prefetch/commits/main)
[![Issues](https://img.shields.io/github/issues/OmsiModsBR/omsi-prefetch)](https://github.com/OmsiModsBR/omsi-prefetch/issues)
![Platform: Windows](https://img.shields.io/badge/platform-Windows%2010%2B-0078d6)
![Game: OMSI 2](https://img.shields.io/badge/game-OMSI%202-f78034)
[![Requires OmsiLaunch](https://img.shields.io/badge/requires-OmsiLaunch%200.1.0--beta4%2B-ff9918)](https://github.com/OmsiModsBR/OmsiLaunch)
![.NET 6](https://img.shields.io/badge/.NET-6-512bd4)
![Docs: EN / PT-BR](https://img.shields.io/badge/docs-EN%20%7C%20PT--BR-00c94a)

</div>

# Omsi Prefetch

Omsi Prefetch: an OmsiLaunch runtime module that prefetches maps and vehicles into memory and speeds up OMSI 2 loading. Module id `omsi-prefetch`.

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

## Documentation

Complete documentation, with examples, in English and Brazilian Portuguese: [English index](docs/README.md) · [Índice em português](docs/pt-BR/README.md).

#### English

- **Getting started:** [Overview](docs/getting-started/overview.md) · [Installation](docs/getting-started/installation.md) · [First run](docs/getting-started/first-run.md) · [Measuring the gain](docs/getting-started/measuring-the-gain.md)
- **Concepts:** [How prefetching works](docs/concepts/how-it-works.md) · [The session program](docs/concepts/session-program.md) · [The native shim](docs/concepts/native-shim.md) · [Learned data](docs/concepts/learned-data.md) · [Memory, the budget and the floors](docs/concepts/memory-and-budget.md) · [Vehicles and scopes](docs/concepts/vehicle-scopes.md) · [Pre-open](docs/concepts/pre-open.md) · [Safety](docs/concepts/safety.md)
- **Guides:** [Choosing settings by memory size](docs/guides/choosing-settings-by-memory.md) · [Using it with predefined session profiles](docs/guides/session-profiles.md) · [Per-session overrides on the command line](docs/guides/command-line-overrides.md) · [Turning it off](docs/guides/turning-off.md) · [Clearing the module's data](docs/guides/clearing-data.md) · [Diagnosing slowness](docs/guides/diagnosing-slowness.md) · [Coexisting with other modules and plugins](docs/guides/coexisting.md)
- **Reference:** [Settings](docs/reference/settings.md) · [Files and directories](docs/reference/files-and-directories.md) · [Log format](docs/reference/log-format.md) · [Events and notices](docs/reference/events-and-notices.md) · [Errors and degradation](docs/reference/errors-and-degradation.md) · [Compatibility](docs/reference/compatibility.md) · [Known limitations](docs/reference/known-limitations.md) · [FAQ](docs/reference/faq.md)
- **Status:** [Test coverage](docs/status/test-coverage.md) · [Changelog](CHANGELOG.md)

#### Português (Brasil)

- **Primeiros passos:** [Visão geral](docs/pt-BR/getting-started/overview.md) · [Instalação](docs/pt-BR/getting-started/installation.md) · [Primeira execução](docs/pt-BR/getting-started/first-run.md) · [Medindo o ganho](docs/pt-BR/getting-started/measuring-the-gain.md)
- **Conceitos:** [Como o prefetch funciona](docs/pt-BR/concepts/how-it-works.md) · [O programa de sessão](docs/pt-BR/concepts/session-program.md) · [O shim nativo](docs/pt-BR/concepts/native-shim.md) · [Dados aprendidos](docs/pt-BR/concepts/learned-data.md) · [Memória, o orçamento e os pisos](docs/pt-BR/concepts/memory-and-budget.md) · [Veículos e escopos](docs/pt-BR/concepts/vehicle-scopes.md) · [Pré-abertura](docs/pt-BR/concepts/pre-open.md) · [Segurança](docs/pt-BR/concepts/safety.md)
- **Guias:** [Escolhendo configurações pelo tamanho da memória](docs/pt-BR/guides/choosing-settings-by-memory.md) · [Usando com perfis de sessão predefinidos](docs/pt-BR/guides/session-profiles.md) · [Sobrescritas por sessão na linha de comando](docs/pt-BR/guides/command-line-overrides.md) · [Desligando](docs/pt-BR/guides/turning-off.md) · [Limpando os dados do módulo](docs/pt-BR/guides/clearing-data.md) · [Diagnosticando lentidão](docs/pt-BR/guides/diagnosing-slowness.md) · [Convivendo com outros módulos e plugins](docs/pt-BR/guides/coexisting.md)
- **Referência:** [Configurações](docs/pt-BR/reference/settings.md) · [Arquivos e diretórios](docs/pt-BR/reference/files-and-directories.md) · [Formato do log](docs/pt-BR/reference/log-format.md) · [Eventos e avisos](docs/pt-BR/reference/events-and-notices.md) · [Erros e degradação](docs/pt-BR/reference/errors-and-degradation.md) · [Compatibilidade](docs/pt-BR/reference/compatibility.md) · [Limitações conhecidas](docs/pt-BR/reference/known-limitations.md) · [FAQ](docs/pt-BR/reference/faq.md)
- **Status:** [Cobertura de testes](docs/pt-BR/status/test-coverage.md) · [Changelog](CHANGELOG.md)

## Building

The module builds against an OmsiLaunch checkout (bridge protocol 2 or later): `Directory.Build.props` points `OmsiLaunchRoot` at `..\OmsiLaunch-spec-pack\OmsiLaunch` (override with `-p:OmsiLaunchRoot=...`), which must be built first.

```text
powershell -File tools\Build-Modules.ps1
```

The script builds the module, runs its tests and lays it out as it is installed: `artifacts\modules\omsi-prefetch\` with its `module.json`.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for building, testing, the documentation rules and the commit policy, and [SECURITY.md](SECURITY.md) to report a vulnerability.

## License

Copyright © 2026 LMonteiro - github.com/lmonteirotech and OmsiModsBR Team - github.com/OmsiModsBR

omsi-prefetch is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License, version 3, as published by the Free Software Foundation. See [LICENSE](LICENSE).

It uses OmsiLaunch, which is licensed under the GNU Lesser General Public License, version 3 only; see [prefetch/NOTICE.txt](prefetch/NOTICE.txt).
