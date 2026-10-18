# Dados aprendidos: a lista aprendida, a lista de escrita e o índice de tiles

<!-- l10n: source=concepts/learned-data.md source-sha256=1e9898b11ae3f41b948a0a729609b4208399d338809e7079b95570ada76b12e3 -->
> Tradução da [página original em inglês](../../concepts/learned-data.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Para cada mapa, o Omsi Prefetch guarda três arquivos pequenos que fazem a próxima sessão do mesmo mapa ser melhor que a primeira. Esta página explica o que eles contêm, como são construídos, como são usados, o que os torna obsoletos, como é uma segunda execução, onde ficam e como são limitados. O formato de cada arquivo está especificado na [referência de arquivos e diretórios](../reference/files-and-directories.md); como apagá-los está em [limpando dados](../guides/clearing-data.md).

Páginas relacionadas: [como o prefetch funciona](how-it-works.md), [programa de sessão](session-program.md), [memória e orçamento](memory-and-budget.md), [primeira execução](../getting-started/first-run.md), [segurança](safety.md).

<a id="where-they-live"></a>
## Onde ficam

```text
<OMSI root>\.omsilaunch\module-data\omsi-prefetch\
  maps\<folder>-<hash>\learned.txt
  maps\<folder>-<hash>\written.txt
  maps\<folder>-<hash>\tiles.idx
  logs\preloader-<date>-<time>.log
```

`<folder>` é o nome da pasta do mapa, com todo caractere que não seja letra, dígito, `-`, `_` ou `.` substituído por `_` e cortado em 40 caracteres. `<hash>` são os primeiros oito dígitos hexadecimais do SHA-256 da identidade do mapa (`maps\<folder>\global.cfg`, em minúsculas), de modo que dois mapas nunca compartilham uma pasta, mesmo quando os nomes colidem. A pasta é criada no primeiro salvamento.

<a id="the-learned-list-learnedtxt"></a>
## A lista aprendida (`learned.txt`)

A lista dos arquivos que o OMSI usou neste mapa, um por linha, relativos à pasta do OMSI, na ordem em que o jogo os usou pela primeira vez. É o arquivo mais importante: na sessão seguinte o módulo mantém esses arquivos primeiro, nessa ordem, antes de qualquer outra coisa.

Como é construída: o componente nativo informa o primeiro uso de cada arquivo (uma linha `U`). O programa de sessão guarda a ordem dos primeiros relatórios. A cada salvamento, a nova lista é a lista antiga seguida dos arquivos usados nesta sessão que não estavam nela, sem duplicatas, sem arquivos que o módulo nunca mantém (veja [segurança](safety.md#what-is-never-held)) e sem arquivos que não existem mais, com no máximo 200.000 entradas.

O que há nela: todo arquivo dentro da pasta do OMSI que o jogo abriu durante a sessão, não só os do mapa: veículos, cenário, texturas, sons, os arquivos de interface do próprio jogo. Arquivos fora da pasta do OMSI são ignorados.

Duas consequências decorrem disso:

- A lista só cresce. Um arquivo que o jogo usou uma vez em alguma sessão permanece nela até você limpar os dados. Se você dirige muitas rotas diferentes em um mapa, a lista cresce até cobrir todas elas.
- A ordem do primeiro uso é a ordem de manutenção. Quando o orçamento é menor que a lista, o que o jogo usou por último é a parte que não é mantida antes de o OMSI iniciar. A previsão de tiles e a [pré-abertura](pre-open.md) trazem parte disso mais tarde.

Com `learn` desligado, a lista ainda é lida e usada, mas não é atualizada.

<a id="the-written-list-writtentxt"></a>
## A lista de escrita (`written.txt`)

A lista dos arquivos que o jogo gravou neste mapa (por exemplo `laststn.osn`, caches `.rdy`, logs, arquivos que um script grava). Eles nunca são mantidos na memória: um arquivo mapeado por outro processo não pode ser substituído nem truncado, e manter um que o jogo reescreve o travaria. A lista é carregada no início de cada sessão, e todo arquivo que o jogo grava durante a sessão é acrescentado a ela (um relatório `W` ou `X`). Independentemente dessa lista, arquivos com as extensões `.osn .rdy .tbmp .odr .tmp .log .sav .bak .owt .prt .lm.bmp` nunca são mantidos, nem os arquivos de que a transação de sessão do OmsiLaunch é dona ([segurança](safety.md#what-is-never-held)).

<a id="the-tile-index-tilesidx"></a>
## O índice de tiles (`tiles.idx`)

Para cada tile do mapa, a lista dos arquivos de que ele precisa: os arquivos próprios do tile, depois todo objeto e spline que ele posiciona, com tudo o que estes citam ([programa de sessão](session-program.md#the-map-wide-files-and-the-tiles)). Sem ele, o módulo teria de ler todo arquivo de tile no início de cada sessão para saber o que manter ao redor do jogador.

Como é construído: um tile é calculado quando é necessário pela primeira vez (um tile inicial antes do OMSI, um tile ao redor do jogador durante a sessão), e a atividade de levantamento (survey) calcula o restante em segundo plano, do mais próximo do início para o mais distante. O índice é salvo a cada dois minutos e no fim, quando cresceu. O levantamento de um mapa grande leva de segundos a dezenas de segundos em um disco frio (Berlin-Spandau da instalação limpa: 369 tiles, 4.664 arquivos, 597 MB; 14,4 s frio, 2,3 s quente).

Formato: uma linha de cabeçalho, uma linha `fingerprint`, depois uma linha `F` por arquivo distinto (um id e o seu caminho relativo) e uma linha `T` por tile (o índice do tile na lista `[map]` do `global.cfg` e os ids dos seus arquivos). Cada arquivo é armazenado uma vez, não importa quantos tiles o usem.

O que o torna obsoleto: o índice carrega a **impressão digital** (fingerprint) do mapa, um hash sobre o nome, o tamanho e a data da última gravação do `global.cfg` e de todo arquivo de tile. Se qualquer um deles mudou, o índice salvo é descartado quando a sessão começa e refeito em segundo plano. Ele não é comparado com os arquivos que os tiles citam: uma textura ou um objeto alterado sem tocar nos arquivos de tile não é notado até você limpar os dados.

Com `survey` desligado, nenhum índice é carregado, calculado nem salvo, e o módulo não mantém os tiles ao redor do início antes do OMSI nem acompanha o jogador durante a sessão.

<a id="what-changes-on-the-second-run"></a>
## O que muda na segunda execução

| | Primeira sessão de um mapa | Sessões seguintes |
| --- | --- | --- |
| Lista aprendida | Vazia. O módulo mantém apenas o que consegue derivar: os arquivos do mapa inteiro, os tiles perto do início (do levantamento), os veículos de IA do mapa. | Mantida primeiro, na ordem do primeiro uso. |
| Índice de tiles | Calculado durante a preparação para os tiles iniciais e em segundo plano para o restante. | Carregado de `tiles.idx`; os tiles iniciais são conhecidos de imediato. |
| Janela | A [janela de primeira abertura](../getting-started/first-run.md#the-first-open-window) é exibida. | Não é exibida. |
| `pre_open` (`learned`) | Nada para abrir antes. | Os arquivos de veículos que o jogo usou antes são abertos antes ([pré-abertura](pre-open.md)). |
| Lista de escrita | Vazia. | Conhecida; os arquivos que o jogo gravou nunca são mantidos. |

<a id="bounds-and-clean-up"></a>
## Limites e limpeza

- Os dados de todos os mapas juntos são mantidos abaixo de `data_limit_mb` (padrão 256 MB, entre 16 e 4096). Quando uma sessão começa e os dados são maiores, os mapas usados há mais tempo (pela data da última gravação dos seus arquivos) são removidos primeiro, nunca o mapa da sessão atual, até caber. Se o mapa da sessão atual sozinho for maior que o limite, ele permanece. O log informa quanto foi removido.
- Os logs são limitados separadamente: no máximo três arquivos de no máximo 1 MiB ([formato do log](../reference/log-format.md)).
- Um salvamento grava um arquivo temporário (`*.tmp`) e substitui o antigo por ele, de modo que uma falha durante um salvamento deixa o arquivo antigo intacto; um `*.tmp` remanescente é removido junto com a pasta do mapa.
- Os tamanhos típicos são alguns megabytes por mapa. `/clear-module-data:omsi-prefetch` remove tudo ([limpando dados](../guides/clearing-data.md)).

<a id="next"></a>
## Próximos passos

[Memória e orçamento](memory-and-budget.md) · [Escopos de veículos](vehicle-scopes.md) · [Arquivos e diretórios](../reference/files-and-directories.md)
