# Limpando os dados do módulo

<!-- l10n: source=guides/clearing-data.md source-sha256=5cc186995dc190e5b33ee8e976d4ce86e52395976190e2164f0a932d9d9f5bd9 -->
> Tradução da [página original em inglês](../../guides/clearing-data.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

O Omsi Prefetch guarda o que aprendeu sobre cada mapa em `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\`. Você nunca precisa limpá-lo: ele é limitado e os mapas usados há mais tempo são removidos sozinhos. Você pode querer limpá-lo para começar um mapa do zero, depois de uma grande mudança em um mapa, para liberar espaço ou antes de remover o módulo. Esta página mostra como, o que é apagado, qual é o efeito e quando fazer isso.

Páginas relacionadas: [dados aprendidos](../concepts/learned-data.md), [arquivos e diretórios](../reference/files-and-directories.md), [desligando](turning-off.md), [primeira execução](../getting-started/first-run.md).

<a id="everything-at-once"></a>
## Tudo de uma vez

Sem nenhuma sessão rodando:

```text
OmsiLaunch.exe /clear-module-data:omsi-prefetch
```

O OmsiLaunch apaga `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\` e nada mais, e responde com `module-data.clear`: `module` e `cleared` (`false` quando não havia nada para apagar); saída `0`. Ele toma o lease da instalação: enquanto uma sessão é dona da instalação, responde `OL_E_INSTALLATION_BUSY` (saída `7`). Uma pasta de dados alcançada por um link é recusada (`OL_E_MODULE_INTEGRITY`), e um link dentro dela é removido como link, nunca seguido. Funciona com o módulo instalado ou não.

Isso remove as listas aprendidas, as listas de escrita, os índices de tiles e os logs de todos os mapas.

<a id="one-map"></a>
## Um mapa

Apague você mesmo a pasta daquele mapa, sem nenhuma sessão rodando:

```text
<OMSI root>\.omsilaunch\module-data\omsi-prefetch\maps\<folder>-<hash>\
```

`<folder>` é o nome da pasta do mapa e `<hash>` os primeiros oito dígitos hexadecimais do SHA-256 de `maps\<folder>\global.cfg` em minúsculas ([arquivos e diretórios](../reference/files-and-directories.md#folder-names-of-a-map)); o nome da pasta basta para encontrá-la. Para limpar apenas o índice de tiles, apague `tiles.idx`; para esquecer o que foi usado, apague `learned.txt`; para esquecer os arquivos que o jogo gravou, apague `written.txt`. O módulo recria o que precisa.

<a id="what-happens-next"></a>
## O que acontece em seguida

A próxima sessão do mapa é uma primeira abertura ([primeira execução](../getting-started/first-run.md)): a lista aprendida está vazia, o índice de tiles é recalculado e a janela de primeira abertura aparece. A sessão seguinte a ela parte do que essa sessão aprendeu.

<a id="when-to-clear"></a>
## Quando limpar

| Situação | Limpar? |
| --- | --- |
| Você quer comparar "primeira abertura" e "segunda abertura" em um mapa | Sim, aquele mapa ([medindo o ganho](../getting-started/measuring-the-gain.md)). |
| Você refez objetos ou texturas de um mapa sem alterar os arquivos de tile dele | O índice de tiles não percebe isso ([dados aprendidos](../concepts/learned-data.md)); apague o `tiles.idx` daquele mapa. |
| A lista aprendida cresceu por causa de uma rota que você não vai dirigir de novo | Apague o `learned.txt` daquele mapa. |
| Você reinstalou ou moveu a sua pasta do OMSI | Não: os dados ficam abaixo da pasta e mantêm os caminhos relativos. |
| Você atualiza o módulo | Não é necessário. |
| Você remove o módulo | Sim, para liberar o espaço ([desligando](turning-off.md)). |
| Os dados parecem estar danificados | Sim. O módulo ignora um arquivo que não consegue interpretar e o reconstrói. |

<a id="you-do-not-need-to"></a>
## Você não precisa

- Manter a pasta pequena: `data_limit_mb` (padrão 256 MB) remove os mapas usados há mais tempo quando uma sessão começa ([configurações](../reference/settings.md#data_limit_mb)).
- Fazer rodízio de logs: no máximo três, de no máximo 1 MiB, são mantidos ([formato do log](../reference/log-format.md)).

<a id="next"></a>
## Próximos passos

[Diagnosticando lentidão](diagnosing-slowness.md) · [Dados aprendidos](../concepts/learned-data.md) · [Desligando](turning-off.md)
