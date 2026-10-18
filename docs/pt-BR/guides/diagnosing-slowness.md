# Diagnosticando lentidão

<!-- l10n: source=guides/diagnosing-slowness.md source-sha256=b0462f72452d08aa8464a0307e00fc328f8a5358b0de82dff5a7fa5638304fd5 -->
> Tradução da [página original em inglês](../../guides/diagnosing-slowness.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Este guia ajuda a descobrir por que uma sessão está lenta ou por que o Omsi Prefetch não ajudou, usando o que o próprio módulo informa: o plano, os eventos, o log e a janela do monitor. Ele começa pelas verificações mais baratas. Onde o log é lido, os formatos das mensagens estão em [formato do log](../reference/log-format.md).

Páginas relacionadas: [formato do log](../reference/log-format.md), [eventos](../reference/events-and-notices.md), [erros e degradação](../reference/errors-and-degradation.md), [escolhendo configurações pelo tamanho da memória](choosing-settings-by-memory.md), [medindo o ganho](../getting-started/measuring-the-gain.md).

<a id="1-is-the-module-in-the-session"></a>
## 1. O módulo está na sessão?

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /plan --json
```

Procure em `Diagnostics`:

| Você vê | Significado |
| --- | --- |
| `module.planned` para `omsi-prefetch` | Ele vai participar. |
| `module.disabled` | Você, um preset ou o `modules.ini` o desligou ([desligando](turning-off.md)). |
| `OL_W_MODULE_NOT_LOADED` | Ele não pode ser carregado; o `Data["code"]` dele diz o motivo (`OL_E_MODULE_INTEGRITY`, `OL_E_MODULE_CONFLICT`, ...). |
| Nada sobre `omsi-prefetch` | Ele não está instalado onde o OmsiLaunch procura: `/list:RuntimeModules` ([instalação](../getting-started/installation.md)). |

<a id="2-read-the-log"></a>
## 2. Leia o log

Abra o `preloader-*.log` mais recente em `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\logs\`. Cinco linhas contam a maior parte da história:

| Linha | O que verificar |
| --- | --- |
| `budget <size> (setting auto), free <MB> MB of <MB> MB` | O orçamento é o que você esperava? `0.0 MB` significa que o orçamento automático é zero: havia menos de 4.096 MB livres. Defina `ram_budget_mb` ([escolhendo configurações pelo tamanho da memória](choosing-settings-by-memory.md)). |
| `map ...; learned <n> files; tile index ... ; first open` | `first open` significa que ainda não há nada aprendido: esta sessão ensina o módulo, a próxima é a que se deve medir. `learned 0 files` sem `first open` significa que o índice de tiles existe mas nada foi aprendido (`learn` desligado, ou o mapa não foi usado por tempo suficiente). |
| Etapas `prepare: ...` | Qual etapa manteve quanto. Uma etapa com `0 files` é suspeita: `learned: 0 files` em um mapa conhecido, ou `start tiles (0 tiles)`, significa que os tiles ou o início do mapa não foram encontrados. |
| `prepared in <s> s: ... [reason]` | Quanto tempo a preparação levou e por que terminou. `(budget reached)`, `(free memory low)` e `(time limit reached ...)` estão explicados em [formato do log](../reference/log-format.md). |
| `end: <n> files held (...), used <n> of them (...), <n> used outside memory (...)` | O resultado dos acertos. Muitos "used outside memory" significam que os arquivos de que o OMSI precisou não estavam na memória. |

Se o log não tem linhas `prepare` ou diz `no map in this session` ou `the map ... cannot be read`, o módulo não tinha nada a preparar: verifique a identidade do mapa da sessão.

<a id="3-common-causes"></a>
## 3. Causas comuns

| Sintoma | Causa provável | O que fazer |
| --- | --- | --- |
| Nenhum ganho na primeira sessão de um mapa | O módulo aprende durante essa sessão. | Execute o mapa de novo e compare a segunda sessão ([primeira execução](../getting-started/first-run.md)). |
| Nenhum ganho, `budget 0.0 MB` | Menos de `reserve_mb` livres no início (máquinas de 8 GB). | Defina um `ram_budget_mb` explícito; feche programas antes da sessão. |
| Nenhum ganho, muitos arquivos `used outside memory`, `(budget reached)` | O orçamento é menor do que o que você usa. | Aumente `ram_budget_mb`; reduza `ai_vehicles` ou `tile_radius` para gastá-lo melhor. |
| Nenhum ganho, a maioria dos arquivos mantidos sem uso | O orçamento está sendo gasto com arquivos que você não usa. | Diminua `tile_radius`, `ai_vehicles`, os escopos de veículos. |
| O OMSI abre muito tarde | Uma primeira abertura grande, um disco lento ou um escopo de IA `complete` em um mapa enorme. | Olhe o tempo de `prepared in`; reduza `ai_vehicles` para `load` ou `essential`; o limite de carregamento é de cinco minutos. |
| Engasgos ao carregar tiles, mas o log está limpo | A previsão de tiles não mantém os tiles em que você está entrando: o orçamento estava cheio quando você se moveu, ou `survey` está desligado. | Verifique `survey`; aumente o orçamento; diminua `tile_radius`. |
| O Windows pagina ou o sistema está lento | Há coisa demais mantida para a máquina. | Diminua `ram_budget_mb`; aumente `stop_below_free_mb`; feche outros programas. A guarda libera arquivos não usados abaixo de 1.024 MB livres ([memória e orçamento](../concepts/memory-and-budget.md)). |
| O log diz `the working set minimum was not raised` | O Windows recusou o pedido para manter as páginas residentes. | Não é um erro; as páginas mantidas podem ser reduzidas sob pressão. Diminua o orçamento se você vir paginação. |
| `feed: OMSI connected` nunca aparece | O componente nativo não se conectou (o módulo não está armado no OMSI, um hook foi recusado ou o programa não tinha pipe). | Leia o evento `hooks` (`OmsiLaunch.exe events read`): um valor diferente de `0` para `create`, `read` ou `close` significa que a fase nativa falhou ([erros e degradação](../reference/errors-and-degradation.md)). |
| `host.failed` / `OL_E_MODULE_LOAD_FAILED` | O programa de sessão não iniciou ou terminou antes de ficar pronto. | Confira se o .NET 6 Desktop Runtime (x64) está instalado; reinstale a pasta do módulo. |
| `OL_W_MODULE_NOT_LOADED` | Um problema de conflito, de integridade ou de protocolo. | Veja [convivência](coexisting.md) e [erros e degradação](../reference/errors-and-degradation.md). |
| A janela de primeira abertura aparece toda vez | `learn` e `survey` estão ambos desligados, ou os dados são limpos a cada início. | Ligue um dos dois de novo; verifique o que limpa `.omsilaunch\module-data`. |

<a id="4-turn-on-the-detail"></a>
## 4. Ligue o detalhe

Para uma sessão, acrescente `/prefetch-debug`:

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-debug
```

O log passa então a dizer também, por veículo, `vehicles: held <folder> (<scope>): <size>`, `vehicles: over the budget: <folder> needs <size>` (um veículo que não coube), `released at OMSI's request: <path>`, `guard: ... released ...` e o resultado de cada pré-abertura no primeiro uso. O detalhe conta contra o mesmo limite de 1 MiB do arquivo; se o log disser `-- log limit reached`, as linhas seguintes são perdidas.

<a id="5-watch-it-live"></a>
## 5. Acompanhe ao vivo

Acrescente `/prefetch-monitor` para ver a janela do monitor: a memória mantida em relação ao orçamento, a fase (lendo o mapa, carregando antes do OMSI, acompanhando a sessão), a memória do sistema, os bytes mantidos por origem (aprendida, mapa, tiles, IA, usuário), a posição estimada, o número de tiles indexados, se o OMSI está conectado e a lista de arquivos. O filtro "Show: used outside memory" lista os arquivos que o OMSI usou e o módulo não manteve (linhas laranja): são o que se deve examinar quando o `used outside memory` do log é grande. "Show: in memory, unused" lista o que o orçamento mantém e o OMSI ainda não usou. Não use o monitor em execuções cronometradas.

<a id="6-rule-out-a-mechanism"></a>
## 6. Descarte um mecanismo

Se a sessão se comportar mal, desligue um mecanismo nativo de cada vez com `/module-setting:omsi-prefetch.read_ahead=false`, `write_behind=false` ou `read_only_open=false`, ou o módulo inteiro com `/no-module:omsi-prefetch`, e compare ([desligando](turning-off.md)). Se um problema desaparece apenas sem o módulo, guarde o log e os eventos `hooks` e `stats` e reporte-o com a versão do OmsiLaunch, a versão do módulo (`1.0.0-beta.1`) e o mapa.

<a id="next"></a>
## Próximos passos

[Convivendo com outros módulos e plugins](coexisting.md) · [Erros e degradação](../reference/errors-and-degradation.md) · [Formato do log](../reference/log-format.md)
