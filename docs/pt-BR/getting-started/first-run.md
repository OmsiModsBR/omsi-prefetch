# Primeira execução

<!-- l10n: source=getting-started/first-run.md source-sha256=f6d30e3c97bd665403b916679e67709ef0cbf5fd5dc4a77aa3f84e3ff4d542bd -->
> Tradução da [página original em inglês](../../getting-started/first-run.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Esta página acompanha a primeira sessão de um mapa com o Omsi Prefetch instalado: o que o módulo faz antes de o OMSI abrir, o que significa a janela de primeira abertura, o que muda na segunda execução e onde olhar depois. Ela pressupõe que o módulo está instalado e visível para o OmsiLaunch, como descrito em [instalação](installation.md). Você inicia as sessões exatamente como antes (o módulo não precisa de nenhuma flag); nada nesta página altera a linha de comando.

Páginas relacionadas: [instalação](installation.md), [medindo o ganho](measuring-the-gain.md), [como o prefetch funciona](../concepts/how-it-works.md), [programa de sessão](../concepts/session-program.md), [dados aprendidos](../concepts/learned-data.md), [formato do log](../reference/log-format.md).

<a id="what-happens-when-you-start-a-session"></a>
## O que acontece quando você inicia uma sessão

Inicie qualquer sessão como de costume, por exemplo:

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1
```

O módulo recebe o mapa da sessão do OmsiLaunch em qualquer forma de iniciar (um mapa novo, o último estado de um mapa ou uma situação salva: o OmsiLaunch resolve o mapa da situação). Em ordem:

1. O OmsiLaunch planeja a sessão e o módulo entra nela (ativação `default`).
2. O OmsiLaunch inicia o `Prefetch.Preloader.exe` **antes** do OMSI e entrega a ele o contexto da sessão. O arquivo de log do módulo é aberto e as primeiras linhas são escritas (id da sessão, orçamento, memória livre).
3. O preloader lê o `global.cfg` do mapa, carrega o que aprendeu sobre esse mapa em sessões anteriores e começa a manter arquivos na RAM, nesta ordem: o que as sessões anteriores usaram (na ordem do primeiro uso), os arquivos do mapa inteiro, os tiles mais próximos do início, os veículos de IA do mapa e mais tiles ao redor do início. Ele para no orçamento de memória, em cinco minutos de carregamento ou quando a memória livre fica baixa, o que ocorrer primeiro ([memória e orçamento](../concepts/memory-and-budget.md)).
4. O preloader informa que está pronto. Só então o OmsiLaunch inicia o `Omsi.exe`. O tempo limite de inicialização da sessão conta a partir do momento em que o OMSI é iniciado, portanto a preparação não o consome.
5. O OMSI carrega o componente nativo e o componente gerenciado. A partir daí o preloader acompanha a sessão ([programa de sessão](../concepts/session-program.md)).
6. Quando o OMSI termina, o OmsiLaunch pede ao preloader que pare; ele salva o que aprendeu, libera a memória e encerra.

Por isso o OMSI abre um pouco mais tarde do que sem o módulo. Essa espera é o preço de ter os arquivos já na memória quando o OMSI os pede, e ela é compensada durante o carregamento ([medindo o ganho](measuring-the-gain.md)).

<a id="the-first-open-window"></a>
## A janela de primeira abertura

Na primeira vez que um mapa é solicitado, o módulo não sabe nada sobre ele: não há lista dos arquivos que sessões anteriores usaram, nem índice de quais arquivos cada tile precisa. A menos que a configuração `first_open_notice` esteja desligada, o preloader mostra então uma pequena janela própria até o OMSI ser iniciado. O indicador da bandeja do OmsiLaunch ainda não aparece nesse ponto, e é por isso que o módulo usa uma janela própria.

| Elemento | Significado |
| --- | --- |
| Barra de título `Omsi Prefetch`; título `Preloading is being set up` | O módulo está preparando este mapa. |
| Texto: o mapa foi solicitado pela primeira vez; o módulo está lendo o mapa e trazendo para a memória o máximo que puder antes de o OMSI abrir; nesta primeira sessão ele também aprende o que o jogo usa; as próximas inicializações serão mais rápidas | Esta é uma primeira abertura. Não há nada errado se a espera for mais longa que o normal. |
| O nome do mapa | O nome amigável do `global.cfg` do mapa (o nome da pasta, quando o mapa não tem um). |
| `Preparing: <size> in memory` e uma barra em movimento | Quanto o módulo mantém agora. Atualiza duas vezes por segundo. |
| `Ready. Opening OMSI...` e uma barra cheia | A preparação terminou e o OMSI está sendo iniciado. A janela fecha sozinha cerca de quatro segundos depois. |

A janela fica sempre por cima, no canto inferior direito da tela principal, nunca toma o foco do teclado, tem um item na barra de tarefas e pode ser minimizada. Ela é apenas informativa: você não precisa fazer nada. O texto dela é em português quando o idioma de exibição do Windows é o português (qualquer variante `pt`) e em inglês nos demais casos.

Um mapa conta como "primeira abertura" quando o módulo não tem nem uma lista aprendida de arquivos para ele nem um índice de tiles salvo que corresponda ao mapa como ele está agora. É o caso da primeiríssima sessão de um mapa, depois de `/clear-module-data:omsi-prefetch` e, de forma permanente, quando `learn` e `survey` estão ambos desligados (nada nunca é salvo). Um mapa cujo `global.cfg` ou cujos arquivos de tile mudaram desde a última sessão não é uma primeira abertura enquanto a lista aprendida dele existir: apenas o índice de tiles é descartado e refeito. Nas sessões seguintes a janela não aparece. O preloader registra o fato no evento `prepared` (`first_open`) e na linha de log `... ; first open`.

Quanto tempo a primeira abertura leva depende do tamanho do mapa e do disco. Para dar a escala, ler o índice de tiles de Berlin-Spandau da instalação limpa do OMSI (369 tiles, 4.664 arquivos, 597 MB) leva 14,4 s com o disco frio e 2,3 s com o disco quente; manter os arquivos na memória vem além disso.

<a id="what-the-first-session-teaches-the-module"></a>
## O que a primeira sessão ensina ao módulo

Durante a sessão, o componente nativo informa todo arquivo que o OMSI usa. Quando a sessão termina, o preloader grava, somente para este mapa:

- `learned.txt`: os arquivos que o jogo usou, na ordem do primeiro uso;
- `written.txt`: os arquivos que o jogo gravou, que nunca são mantidos em memória;
- `tiles.idx`: para cada tile, os arquivos de que ele precisa (calculado em segundo plano enquanto você joga e salvo quando cresceu).

Ele também os salva a cada dois minutos durante a sessão, de modo que uma falha da sessão perde no máximo esse intervalo.

<a id="the-second-run"></a>
## A segunda execução

Na sessão seguinte do mesmo mapa, o preloader encontra a lista aprendida e o índice de tiles. A janela não aparece, os arquivos que você realmente usou vêm primeiro e os tiles do início já são conhecidos de imediato. O resultado esperado é um carregamento mais curto. Execute o mesmo início duas ou três vezes antes de julgar: a primeira sessão paga pelo aprendizado, e o cache de arquivos do próprio sistema operacional torna otimistas as comparações entre execuções muito próximas ([medindo o ganho](measuring-the-gain.md)).

<a id="where-to-look-afterwards"></a>
## Onde olhar depois

| O quê | Onde |
| --- | --- |
| O log do módulo para a sessão | `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\logs\preloader-<yyyyMMdd-HHmmss>.log` (os três mais recentes são mantidos). Leia as linhas `budget`, `prepared in` e `end:`. |
| O que ele aprendeu sobre um mapa | `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\maps\<folder>-<hash>\` |
| Visão ao vivo do que é mantido | Inicie a sessão com `/prefetch-monitor`: a janela do monitor mostra os arquivos mantidos, em uso e usados fora da memória. |
| Eventos da sessão | `OmsiLaunch.exe events read` enquanto a sessão roda ([eventos](../reference/events-and-notices.md)). |

Se algo parecer errado, vá para [diagnosticando lentidão](../guides/diagnosing-slowness.md).

<a id="next"></a>
## Próximos passos

[Medindo o ganho](measuring-the-gain.md) · [Escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md) · [Diagnosticando lentidão](../guides/diagnosing-slowness.md)
