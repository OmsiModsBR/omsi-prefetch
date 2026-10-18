# Medindo o ganho

<!-- l10n: source=getting-started/measuring-the-gain.md source-sha256=2e2d1eb7de0debf4c434ee62c15301ed10080a984e694520b4ab61054a2d77a8 -->
> Tradução da [página original em inglês](../../getting-started/measuring-the-gain.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Esta página mostra como comparar o carregamento do OMSI com e sem o Omsi Prefetch na sua própria máquina, quais números o módulo oferece e quais armadilhas tornam uma comparação errada. Os resultados publicados vêm de uma máquina e um mapa (n = 1) e estão listados na [visão geral](overview.md#what-you-get); os seus vão diferir conforme o hardware, o mapa e a memória que você dá ao módulo.

Páginas relacionadas: [visão geral](overview.md), [primeira execução](first-run.md), [diagnosticando lentidão](../guides/diagnosing-slowness.md), [formato do log](../reference/log-format.md), [eventos](../reference/events-and-notices.md), [desligando](../guides/turning-off.md).

<a id="what-to-compare"></a>
## O que comparar

Compare o mesmo início (mesmo mapa, mesmo ponto de entrada ou situação salva, mesmo veículo) em duas condições:

| Condição | Como iniciar |
| --- | --- |
| Sem o módulo | Acrescente `/no-module:omsi-prefetch` à linha de comando. |
| Com o módulo | A mesma linha de comando sem essa flag (o módulo vem ligado por padrão). |

Meça do momento em que você inicia o comando até o momento em que o jogo está utilizável. Tome dois tempos, porque o módulo move parte do trabalho para antes do OMSI:

| Intervalo | Onde termina |
| --- | --- |
| Início do comando até a janela do OMSI aparecer | A preparação do módulo está neste intervalo. Ele é mais longo com o módulo. |
| Janela do OMSI até o jogo estar utilizável | O carregamento propriamente dito. É onde o módulo mais economiza. Uma definição simples: os primeiros 60 segundos sem um congelamento de um segundo ou mais, que é a usada pelo estudo. |

O total dos dois é o que você espera. O primeiro indicador da tabela do estudo (o mesmo mapa otimizado, plugin observando versus agindo) é contado a partir de "Loading Situation"; guarde os dois intervalos nas suas anotações para comparar coisas equivalentes.

<a id="rules-for-a-fair-comparison"></a>
## Regras para uma comparação justa

1. **Aprenda primeiro.** A primeira sessão de um mapa é uma sessão de aprendizado ([primeira execução](first-run.md)). Descarte-a e compare as sessões seguintes.
2. **Esvazie o cache de arquivos nas execuções que você compara.** Depois de uma execução, o Windows mantém os arquivos no seu próprio cache, e a execução seguinte é rápida mesmo sem o módulo. O estudo esvaziou o cache de arquivos do sistema operacional antes da execução com os mecanismos ligados. Reiniciar o Windows faz isso; esvaziar a standby list com uma ferramenta como o RAMMap da Sysinternals também. Use o mesmo método antes de cada execução cronometrada.
3. **Repita.** Faça pelo menos três execuções por condição e compare as medianas.
4. **Mude uma coisa de cada vez.** Não altere configurações entre as duas condições; mudar uma configuração entre execuções da mesma condição é outra comparação ([guia por tamanho de memória](../guides/choosing-settings-by-memory.md)).
5. **Feche outros programas** que disputam memória. O orçamento é calculado a partir da memória livre no início ([memória e orçamento](../concepts/memory-and-budget.md)); uma quantidade diferente de memória livre é um orçamento diferente. A linha de log `budget ... free ...` registra isso.

<a id="what-the-module-tells-you"></a>
## O que o módulo informa

<a id="the-log"></a>
### O log

O log de cada sessão ([formato do log](../reference/log-format.md)) traz os números que explicam um resultado:

| Linha | O que ela informa |
| --- | --- |
| `budget <size> (setting auto, ...), free <n> MB of <n> MB` | A memória que o módulo podia manter e quanto estava livre. |
| `prepare: <step>: <n> files, <size>` | Quanto cada etapa manteve (`learned`, `map`, `start tiles`, `tiles around the start`). |
| `prepared in <s> s: <n> files, <size> of <size>` | Quanto tempo a preparação levou e por que terminou (`budget reached`, `free memory low`, `time limit reached`, ou nada de especial quando tudo coube). |
| `end: <n> files held (<size>), used <n> of them (<size>), <n> used outside memory (<size>); feed <n> lines` | O resultado da sessão: quantos arquivos mantidos o jogo realmente usou e quantos arquivos ele usou que a memória não mantinha. |

Uma boa sessão tem muitos arquivos em "used" e poucos em "used outside memory". Muitos arquivos mantidos que nunca foram usados significam que o orçamento está sendo gasto com as coisas erradas (diminua `tile_radius`, use um escopo de veículos menor). Muitos arquivos usados fora da memória significam que o orçamento foi pequeno demais ou que a lista aprendida ainda é rala (execute o mapa de novo ou aumente o orçamento).

<a id="the-events"></a>
### Os eventos

Enquanto a sessão roda, `OmsiLaunch.exe events read` (ou `events watch`) mostra os eventos do módulo. Dois trazem números ([eventos](../reference/events-and-notices.md)):

- `module.omsi-prefetch.prepared`: `held_mb`, `budget_mb`, `files`, `ms` (o tempo de preparação) e `first_open`;
- `module.omsi-prefetch.stats`, emitido quando o módulo para com uma saída normal do OMSI: `app_reads` (as chamadas `ReadFile` que o OMSI fez por meio do módulo) contra `real_reads` (as que chegaram ao sistema operacional), `served_from_buffer` e os contadores de write-behind. A razão `app_reads` / `real_reads` é a redução de chamadas de sistema. Para capturar o evento, execute `events watch` em um segundo console antes de sair do OMSI. Uma sessão encerrada com `session stop` ou pela bandeja termina o OMSI sem uma saída normal, e esse evento não é emitido ([segurança](../concepts/safety.md#what-happens-when-something-goes-wrong)).

<a id="the-monitor-window"></a>
### A janela do monitor

Inicie a sessão com `/prefetch-monitor` para acompanhar o módulo ao vivo: quanto está mantido em relação ao orçamento, quais arquivos o jogo usa e quais arquivos ele usou que a memória não mantinha. A janela custa um pouco de CPU; não a use em execuções cronometradas.

<a id="estimating-a-map-before-you-play-it"></a>
### Estimando um mapa antes de jogar

O programa de testes do repositório consegue ler uma instalação e informar o que o módulo veria em um mapa, sem manter nem gravar nada: `Prefetch.Tests.exe --survey <OMSI folder> <maps\<map>\global.cfg>` mostra a contagem de tiles, o tamanho do índice de tiles, os veículos de IA e os veículos do usuário, com tempos. Ele é compilado por `tools\Build-Modules.ps1` e é uma ferramenta de desenvolvedor; não está na pasta do módulo. Berlin-Spandau da instalação limpa resulta em 369 tiles, 4.664 arquivos e 597 MB.

<a id="reading-the-result"></a>
## Lendo o resultado

Espere os maiores ganhos na fase de carregamento depois que o mundo começa e nos congelamentos durante o carregamento de tiles (o estudo: tempo de `ReadFile` e as etapas "Loading Tile" e "Updating Terrain"), e nenhuma mudança na taxa de quadros. Se o tempo de carregamento depois que o OMSI abre não melhorou, leia [diagnosticando lentidão](../guides/diagnosing-slowness.md): as causas usuais são um orçamento pequeno demais para a memória livre da máquina, um mapa cuja lista aprendida ainda não existe e outro plugin em conflito.

<a id="next"></a>
## Próximos passos

[Escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md) · [Diagnosticando lentidão](../guides/diagnosing-slowness.md) · [Formato do log](../reference/log-format.md)
