# Documentação do Omsi Prefetch

<!-- l10n: source=README.md source-sha256=f8d4042e52bda0eed6a505a51988b12ce4ddf8041af2bfc63dda0767de88d793 -->
> Tradução da [página original em inglês](../README.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

[English](../README.md) | Português (Brasil)

Esta é a documentação em português do Brasil do Omsi Prefetch `1.0.0-beta.1` (id do módulo `omsi-prefetch`), um Runtime Module do OmsiLaunch que faz prefetch de mapas e veículos para a memória e acelera o carregamento do OMSI 2. Cada página descreve o que o código atual faz; quando uma página e o código divergem, vale o código e a página contém um bug. As páginas espelham a estrutura da documentação do OmsiLaunch: primeiros passos, conceitos, guias, referência e status.

O módulo é um Runtime Module do OmsiLaunch `0.1.0-beta4` ou posterior. A documentação do próprio OmsiLaunch (sua referência de Runtime Modules, a referência da CLI e a referência de perfis de sessão) é a fonte normativa de tudo o que o módulo usa do OmsiLaunch: onde os módulos ficam, integridade, ativação, flags, `modules.ini`, presets. Estas páginas apontam para ela pelo nome e repetem apenas o que um usuário deste módulo precisa.

Vocabulário usado em toda a documentação: o módulo é um beta (`1.0.0-beta.1`); o recurso de Runtime Modules do OmsiLaunch é `EXPERIMENTAL`. Os resultados citados do estudo de otimização de carregamento de 30/09/2026 são os do README do repositório (uma única máquina e um único mapa, n = 1) e são tomados como válidos para o módulo.

<a id="who-reads-what"></a>
## Quem lê o quê

| Público | Comece aqui | Depois |
| --- | --- | --- |
| Jogadores que querem carregamento mais rápido | [Visão geral](getting-started/overview.md), [Instalação](getting-started/installation.md), [Primeira execução](getting-started/first-run.md) | [Escolhendo configurações pelo tamanho da memória](guides/choosing-settings-by-memory.md), [Medindo o ganho](getting-started/measuring-the-gain.md), [FAQ](reference/faq.md) |
| Jogadores que ajustam ou usam perfis | [Configurações](reference/settings.md), [Perfis de sessão](guides/session-profiles.md) | [Sobrescritas pela linha de comando](guides/command-line-overrides.md), [Desligando](guides/turning-off.md), [Limpando dados](guides/clearing-data.md) |
| Jogadores que têm um problema | [Diagnosticando lentidão](guides/diagnosing-slowness.md) | [Formato do log](reference/log-format.md), [Erros e degradação](reference/errors-and-degradation.md), [Convivência](guides/coexisting.md) |
| Autores de mapas e add-ons, integradores | [Como o prefetch funciona](concepts/how-it-works.md), [Arquivos e diretórios](reference/files-and-directories.md) | [Eventos](reference/events-and-notices.md), [Compatibilidade](reference/compatibility.md), [Perfis de sessão](guides/session-profiles.md) |
| Mantenedores | [Segurança](concepts/safety.md), [Limitações conhecidas](reference/known-limitations.md) | [Cobertura de testes](status/test-coverage.md), [Changelog](../../CHANGELOG.md) |

<a id="navigation"></a>
## Navegação

<a id="getting-started"></a>
### Primeiros passos

| Página | Finalidade |
| --- | --- |
| [Visão geral](getting-started/overview.md) | O que o módulo é, o que faz por você, o que não faz, os resultados medidos e as suas três partes. |
| [Instalação](getting-started/installation.md) | Requisitos, instalação da pasta do módulo em uma instalação do OmsiLaunch, verificação de que está ativo, atualização e remoção. |
| [Primeira execução](getting-started/first-run.md) | O que acontece antes de o OMSI abrir, o que significa a janela de primeira abertura, o que a primeira sessão ensina ao módulo, a segunda execução. |
| [Medindo o ganho](getting-started/measuring-the-gain.md) | Como comparar com e sem o módulo de forma justa e quais números o módulo oferece. |

<a id="concepts"></a>
### Conceitos

| Página | Finalidade |
| --- | --- |
| [Como o prefetch funciona](concepts/how-it-works.md) | Os três componentes, como se conectam e uma sessão do início ao fim. |
| [O programa de sessão](concepts/session-program.md) | O que o `Prefetch.Preloader.exe` faz antes e enquanto o OMSI roda: preparação, levantamento, previsão, veículos, pré-abertura, guarda, salvamento, parada. |
| [O shim nativo](concepts/native-shim.md) | Read-ahead, write-behind, abertura somente leitura e os relatórios de uso dentro do OMSI, com suas condições, garantias e limites. |
| [Dados aprendidos](concepts/learned-data.md) | A lista aprendida, a lista de escrita e o índice de tiles: o que contêm, como são construídos, como ficam obsoletos e como são limitados. |
| [Memória, o orçamento e os pisos](concepts/memory-and-budget.md) | O que significa "mantido em memória", o orçamento automático, o piso de carregamento, a guarda e a ordem em que o orçamento é gasto. |
| [Veículos e escopos](concepts/vehicle-scopes.md) | `essential`, `load` e `complete`; os veículos de IA do mapa, os veículos do usuário e a frota. |
| [Pré-abertura](concepts/pre-open.md) | Abrir arquivos de veículos antes do jogo sem mantê-los, de forma planejada e no primeiro uso. |
| [Segurança](concepts/safety.md) | O que o módulo lê e grava, o que nunca mantém, como evita atrapalhar o OMSI e o que acontece quando algo para inesperadamente. |

<a id="guides"></a>
### Guias

| Página | Finalidade |
| --- | --- |
| [Escolhendo configurações pelo tamanho da memória](guides/choosing-settings-by-memory.md) | Valores iniciais para máquinas de 8, 16, 32 e 64 GB e como ajustar a partir do log. |
| [Usando com perfis de sessão predefinidos](guides/session-profiles.md) | Levar as configurações do módulo, ou desligá-lo, em um preset de perfil de sessão do OmsiLaunch. |
| [Sobrescritas por sessão na linha de comando](guides/command-line-overrides.md) | As flags do módulo, `/module-setting`, aspas, precedência e conferência com `/plan`. |
| [Desligando](guides/turning-off.md) | Para uma sessão, para um perfil, para todas as sessões, permanentemente, ou apenas alguns mecanismos. |
| [Limpando os dados do módulo](guides/clearing-data.md) | `/clear-module-data`, limpar um mapa, o que acontece em seguida. |
| [Diagnosticando lentidão](guides/diagnosing-slowness.md) | Plano, log, eventos, log de depuração e monitor: descobrir por que uma sessão está lenta ou por que o módulo não ajudou. |
| [Convivendo com outros módulos e plugins](guides/coexisting.md) | O conflito com o `OmsiLoadingPoC.dll`, outros hooks, memória compartilhada entre módulos, ferramentas que editam arquivos. |

<a id="reference"></a>
### Referência

| Página | Finalidade |
| --- | --- |
| [Configurações](reference/settings.md) | Toda configuração com tipo, padrão, limites, flag, efeito, interações e um exemplo. |
| [Arquivos e diretórios](reference/files-and-directories.md) | O que o módulo lê e grava, os formatos dos seus dados, os limites em disco e em memória. |
| [Formato do log](reference/log-format.md) | O arquivo de log, seus limites e cada mensagem. |
| [Eventos e avisos](reference/events-and-notices.md) | Todo evento `module.omsi-prefetch.*` com seus dados, e o que o módulo não emite. |
| [Erros e degradação](reference/errors-and-degradation.md) | O que pode dar errado, os códigos do OmsiLaunch que aparecem e o que o módulo faz a respeito. |
| [Compatibilidade](reference/compatibility.md) | OmsiLaunch, Windows, .NET, builds do OMSI 2, mapas, outros plugins e idiomas. |
| [Limitações conhecidas](reference/known-limitations.md) | O que o módulo não faz, faz em parte ou faz com um risco aceito, e as diferenças encontradas entre README, manifesto e código. |
| [FAQ](reference/faq.md) | Respostas curtas para perguntas comuns. |

<a id="status"></a>
### Status

| Página | Finalidade |
| --- | --- |
| [Cobertura de testes](status/test-coverage.md) | Os gates offline pelo nome, os cenários nativos, o que não cobrem e a verificação da documentação. |
| [Changelog](../../CHANGELOG.md) | O que o `1.0.0-beta.1` contém. |

<a id="how-this-documentation-is-kept-in-sync"></a>
## Como esta documentação é mantida em sincronia

O `tools\Check-Docs.ps1` roda a partir da raiz do repositório e verifica que todo link relativo e âncora em `docs\**\*.md`, `README.md`, `prefetch\README.md` e `CHANGELOG.md` resolve, que toda configuração e flag de `prefetch\module.template.json` está documentada na [referência de configurações](reference/settings.md) com seu padrão e limites, que toda página está listada na navegação acima e que a árvore `docs\pt-BR\` espelha a árvore em inglês (mesmas páginas, mesmos títulos, mesmos blocos de código). Ele não verifica a prosa, então uma página ainda pode estar errada sobre o comportamento; reporte isso como um bug contra a página.

<a id="license"></a>
## Licença

Copyright © 2026 LMonteiro - github.com/lmonteirotech and OmsiModsBR Team - github.com/OmsiModsBR. O Omsi Prefetch é software livre sob a GNU General Public License, versão 3; usa o OmsiLaunch, que é licenciado sob a GNU Lesser General Public License, somente versão 3. Veja o `LICENSE` do repositório e o `prefetch\NOTICE.txt`.
