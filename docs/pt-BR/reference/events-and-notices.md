# Eventos e avisos

<!-- l10n: source=reference/events-and-notices.md source-sha256=e96a316529d88397ce66ed8ae7d216829ef6bb1c98ea970ac785ebc5c02bf3ff -->
> Tradução da [página original em inglês](../../reference/events-and-notices.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

O Omsi Prefetch informa o seu progresso ao OmsiLaunch como eventos de runtime chamados `module.omsi-prefetch.<name>`. Esta página lista todo evento que o módulo emite (e os que o OmsiLaunch emite sobre o programa de sessão dele), com as chaves dos seus dados e quando aparecem, e diz o que o módulo não emite (avisos, operações). Os eventos são lidos enquanto a sessão roda com `OmsiLaunch.exe events read` ou `events watch` e por `session status`; todos os valores são strings.

Páginas relacionadas: [formato do log](log-format.md), [erros e degradação](errors-and-degradation.md), [medindo o ganho](../getting-started/measuring-the-gain.md), [programa de sessão](../concepts/session-program.md), [shim nativo](../concepts/native-shim.md).

<a id="events-at-a-glance"></a>
## Eventos em resumo

| Evento | Emitido por | Quando |
| --- | --- | --- |
| `module.omsi-prefetch.host.started` | OmsiLaunch | O programa de sessão foi iniciado. Dados: `pid`. |
| `module.omsi-prefetch.prepared` | Programa de sessão | A preparação terminou, logo antes de informar que está pronto. Não é emitido quando a preparação é pulada (sem mapa, mapa ilegível, orçamento zero). |
| `module.omsi-prefetch.host.ready` | OmsiLaunch | O programa informou que está pronto. Dados: `prepare_ms`. |
| `module.omsi-prefetch.hooks` | Componente nativo | Os hooks foram solicitados (quando o OMSI carrega o componente). |
| `module.omsi-prefetch.armed` | Componente gerenciado | O módulo foi armado dentro do OMSI. |
| `module.omsi-prefetch.gameplay-ready` | Componente gerenciado | O gameplay foi iniciado. |
| `module.omsi-prefetch.stopping` | Componente gerenciado | O módulo está parando (uma saída normal do OMSI). |
| `module.omsi-prefetch.stats` | Componente nativo | O componente nativo parou. |
| `module.omsi-prefetch.session` | Programa de sessão | No fim da sessão, depois de o programa ser mandado parar. |
| `module.omsi-prefetch.host.failed`, `host.exited`, `host.killed` | OmsiLaunch | O programa de sessão falhou ao preparar, terminou ou foi encerrado ([erros e degradação](errors-and-degradation.md)). |

O OmsiLaunch também informa os seus próprios eventos de módulo (`module.session-failure`, `module.failed`, `module.degraded`, ...) como descrito na sua referência de Runtime Modules.

<a id="moduleomsi-prefetchprepared"></a>
## `module.omsi-prefetch.prepared`

| Chave | Significado |
| --- | --- |
| `held_mb` | MB mantidos no fim da preparação. |
| `budget_mb` | O orçamento, em MB. |
| `files` | Número de arquivos mantidos. |
| `ms` | Duração das etapas de carregamento, em milissegundos (exclui o tempo de ler o mapa e o índice de tiles antes). |
| `first_open` | `true` quando o módulo não sabia nada sobre o mapa ([primeira execução](../getting-started/first-run.md#the-first-open-window)), `false` caso contrário. |

<a id="the-hooks-event"></a>
## O evento hooks

`module.omsi-prefetch.hooks` traz o resultado de cada solicitação de hook, como os números que a bridge retorna. `0` é sucesso; `-1` significa que o hook não foi solicitado ou não estava disponível. Os outros valores são os códigos de recusa da bridge (por exemplo `3` o ponto não contém o que deveria, `4` o ponto tem hook de alguém fora da bridge, `10` sem dispositivo).

| Chave | Hook |
| --- | --- |
| `create`, `read`, `close` | `CreateFileW`, `ReadFile`, `CloseHandle`. Obrigatórios: se algum não for `0`, a fase nativa do módulo falha. |
| `write` | `WriteFile`. |
| `seek`, `size`, `truncate` | `SetFilePointer`, `GetFileSize`, `SetEndOfFile`. `-1` quando read-ahead, write-behind e abertura somente leitura estão todos desligados. |
| `tex_ex_w`, `tex_ex_a`, `tex_w`, `mesh` | `D3DXCreateTextureFromFileExW`, `...ExA`, `D3DXCreateTextureFromFileW`, `D3DXLoadMeshFromXA`. `-1` quando o `d3dx9.dll` não estava carregado ou não exporta a função quando o componente iniciou. |
| `feed` | `1` quando o pipe da sessão está configurado, `0` quando o OMSI não tem programa de sessão (então nenhum relatório de uso é enviado). |

<a id="moduleomsi-prefetcharmed"></a>
## `module.omsi-prefetch.armed`

| Chave | Significado |
| --- | --- |
| `channel` | `session-program` quando o módulo tem um canal de sessão, `none` caso contrário. |
| `read_ahead`, `write_behind`, `read_only_open` | As configurações efetivas como `true` ou `false`. |

<a id="moduleomsi-prefetchstats"></a>
## `module.omsi-prefetch.stats`

Emitido quando o componente nativo para, depois de ele ter descarregado e devolvido todo handle. Os contadores valem para a sessão inteira.

| Chave | Significado |
| --- | --- |
| `app_reads` | Chamadas `ReadFile` que o OMSI fez por meio do módulo. |
| `real_reads` | Chamadas `ReadFile` que chegaram ao sistema operacional. `app_reads` dividido por `real_reads` é a redução. |
| `served_from_buffer` | Leituras atendidas (ao menos em parte) a partir de um bloco de read-ahead. |
| `shim_stopped` | Handles em que uma gravação ou truncamento encerrou o shim. |
| `write_behind_app_writes` | Gravações que o OMSI fez em handles `.rdy` com buffer. |
| `write_behind_real_writes` | Gravações que chegaram ao sistema operacional para eles. |
| `write_behind_failed` | Descarregamentos que falharam duas vezes (o write-behind então é desligado para novos handles). |
| `read_only_opens` | Aberturas rebaixadas para somente leitura. |
| `read_only_reopened` | Handles somente leitura em que o OMSI gravou, reabertos com acesso de escrita. |
| `read_only_reopen_failed` | Reaberturas que falharam. |
| `track_full` | Handles que não puderam ser rastreados (mais de 4096 ao mesmo tempo). |
| `release_requests` | Vezes em que o OMSI não conseguiu substituir um arquivo mantido e uma liberação foi solicitada. |
| `feed_dropped` | Linhas de relatório de uso descartadas (fila cheia ou nenhum programa de sessão alcançável). |

<a id="moduleomsi-prefetchsession"></a>
## `module.omsi-prefetch.session`

| Chave | Significado |
| --- | --- |
| `held_mb` | MB mantidos no fim. |
| `used_held` | Arquivos mantidos que o OMSI usou. |
| `used_outside` | Arquivos que o OMSI usou e que não estavam mantidos. |
| `feed_lines` | Linhas recebidas do componente nativo. |
| `feed_refused` | Clientes recusados por não serem o processo do OMSI. |

<a id="notices-and-operations"></a>
## Avisos e operações

- **Avisos.** O módulo nunca emite um evento `notice`, de modo que a bandeja do OmsiLaunch não mostra balão para ele. As únicas mensagens visíveis ao usuário são as duas janelas do próprio módulo: a janela de primeira abertura e o monitor opcional ([programa de sessão](../concepts/session-program.md#windows)).
- **Erros.** Se a preparação falhar, o programa de sessão escreve uma linha `error` que o OmsiLaunch registra como diagnóstico de sessão com `OL_E_MODULE_OPERATION_FAILED` e o detalhe `prefetch: the preparation failed: <message>`.
- **Operações.** O módulo não declara operações. Uma chamada a `module.omsi-prefetch.<operation>` responde `OL_E_MODULE_OPERATION_UNKNOWN`.

<a id="next"></a>
## Próximos passos

[Erros e degradação](errors-and-degradation.md) · [Formato do log](log-format.md) · [Compatibilidade](compatibility.md)
