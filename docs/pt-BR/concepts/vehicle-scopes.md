# Veículos e escopos: essential, load e complete

<!-- l10n: source=concepts/vehicle-scopes.md source-sha256=cb4c0c5d4a3743c88f6886e6bb0f54a9df63c2c2fea83588161a9e2f27dab01d -->
> Tradução da [página original em inglês](../../concepts/vehicle-scopes.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Um veículo são muitos arquivos: scripts, modelos, texturas, sons, repinturas, horários. Manter tudo de todos os veículos que você possui usaria mais memória do que a maioria das máquinas tem, por isso o Omsi Prefetch tem três escopos que dizem quanto de um veículo é mantido, e configurações separadas para os veículos que o próprio mapa usa (a sua IA) e para os veículos que você dirige. Esta página define os escopos, mostra como o módulo decide quais veículos considerar e diz quando cada conjunto é mantido.

Páginas relacionadas: [como o prefetch funciona](how-it-works.md), [memória e orçamento](memory-and-budget.md), [pré-abertura](pre-open.md), [referência de configurações](../reference/settings.md#vehicles), [escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md).

<a id="the-three-scopes"></a>
## Os três escopos

| Escopo | O que é mantido | Quando escolhê-lo |
| --- | --- | --- |
| `essential` | O que a pré-visualização de um veículo precisa primeiro: o arquivo do veículo (`.bus` ou `.ovh`), o `.cfg` do seu modelo, a maior malha (`.o3d`) com as texturas que ela nomeia e as texturas base da configuração dessa malha, e os arquivos `.hof` na pasta do veículo. | Muitos veículos com um orçamento pequeno; a menor pegada. |
| `load` | O fechamento de carregamento inteiro: tudo o que o arquivo do veículo cita, seguido transitivamente (modelos, texturas com o seu `.cfg`, scripts, subobjetos, a lista de repinturas e as texturas das repinturas que ela lista, com `sounds` os arquivos `.wav` citados), mais os arquivos `.hof` na pasta do veículo. | O que o OMSI lê quando carrega o veículo. Um padrão equilibrado para os veículos que você usa. |
| `complete` | Primeiro o fechamento `load`, depois todo arquivo da pasta do veículo, recursivamente, exceto arquivos de trabalho e arquivos compactados (`.psd .rar .zip .7z .blend .max .3ds .pdf .doc .xls .db .bak .tmp .exe .dll .fbx .obj .svg .log .osn .rdy .sav` e semelhantes). Com `sounds` desligado, os arquivos `.wav` ficam de fora. | Veículos que você usa em toda sessão e para os quais há memória de sobra. |

A ordem importa dentro de um veículo: os arquivos são mantidos na ordem de carregamento, de modo que um veículo interrompido pelo orçamento fica com a parte que o OMSI lê primeiro. Um veículo só é mantido inteiro quando tudo o que ele ainda precisa cabe no orçamento e no teto de veículos `vehicle_budget_mb`; caso contrário, é pulado (veículos menores depois dele ainda podem caber).

<a id="the-maps-vehicles-ai"></a>
## Os veículos do mapa (IA)

O módulo encontra os veículos do mapa lendo as listas do próprio mapa: `ailists.cfg`, `parklist_p.txt`, `humans.txt`, `drivers.txt` e `unsched_vehgroups.txt`. Toda referência a um arquivo `.bus`, `.ovh`, `.sco` ou `.hum` que exista passa a fazer parte da IA do mapa:

- Uma referência abaixo de `Vehicles\<folder>\` faz dessa pasta um veículo de IA, mantido com o escopo de `ai_vehicles`.
- Uma referência em outro lugar (pessoas, objetos que a IA posiciona) vai para uma entrada extra, "objetos e pessoas do mapa", que é seguida como cenário, qualquer que seja o escopo.
- Os veículos de IA são mantidos do menor para o maior, depois dos objetos e das pessoas.

`ai_vehicles` aceita `off`, `essential`, `load` ou `complete` (padrão `complete`). `off` não mantém nenhum dos veículos de IA **nem** nenhum dos objetos e pessoas. Os veículos de IA são mantidos na preparação, antes de o OMSI iniciar, depois dos arquivos do mapa inteiro e dos tiles iniciais e antes do anel mais amplo de tiles.

<a id="the-users-vehicles"></a>
## Os veículos do usuário

`user_vehicles` escolhe quais dos outros veículos são mantidos:

| Valor | Veículos considerados |
| --- | --- |
| `none` (padrão) | Nenhum. |
| `list` | As pastas nomeadas em `user_vehicle_list` (nomes de pastas sob `Vehicles\`, separados por `;`, na ordem dada, comparados sem diferenciar maiúsculas de minúsculas). Um nome que não é uma pasta de veículo válida, ou que é um veículo da IA do mapa, é pulado e o log informa. |
| `all` | Toda pasta de `Vehicles\` que tem um veículo válido e não é usada pela IA do mapa, primeiro a frota do próprio mapa e depois do menor para o maior. |

Uma pasta é um veículo válido quando contém um arquivo `.bus` ou `.ovh` com uma seção `[model]`. Pastas cujo nome começa com `.` e pastas sem um veículo válido são ignoradas. Com `ai_vehicles` em `off`, nenhuma pasta é excluída como IA, então `all` passa a incluir os veículos que o mapa teria usado.

O escopo é `user_vehicle_scope` (padrão `essential`). Um veículo que carrega o horário do mapa, um arquivo `.hof` com o nome da pasta do mapa na própria pasta do veículo, é a frota do próprio mapa e usa `fleet_scope` no lugar (padrão `load`).

Os veículos do usuário são mantidos **enquanto o OMSI roda**, não antes de ele iniciar: uma atividade em segundo plano espera a previsão de tiles ficar ociosa (até dois minutos no começo, depois até 30 segundos antes de cada veículo) e mantém os veículos um a um. Escolha um escopo e uma lista que caibam no orçamento: eles são mantidos depois que o orçamento já foi para os tiles e para a IA.

<a id="sounds"></a>
## Sons

`sounds` (padrão verdadeiro) controla os arquivos `.wav` que veículos e objetos citam e, em `complete`, os arquivos `.wav` de uma pasta. Não muda outros formatos de áudio: arquivos `.ogg` e semelhantes só são mantidos por `complete`, como parte da pasta inteira, qualquer que seja o valor de `sounds`.

<a id="sizes-ordering-and-caps"></a>
## Tamanhos, ordenação e tetos

- `vehicle_budget_mb` limita a camada de IA e a camada do usuário separadamente (padrão 0: apenas `ram_budget_mb` limita).
- Tudo é limitado pelo orçamento e pelos pisos ([memória e orçamento](memory-and-budget.md)).
- Sob pressão de memória, a guarda libera primeiro os arquivos de veículos não usados ([memória e orçamento](memory-and-budget.md#the-memory-guard)).
- Os arquivos de um veículo são resolvidos quando necessários; o conjunto da IA é calculado uma vez na preparação e o conjunto do usuário uma vez durante a sessão.

<a id="next"></a>
## Próximos passos

[Pré-abertura](pre-open.md) · [Escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md) · [Referência de configurações](../reference/settings.md)
