# Pré-abertura: abrir arquivos de veículos antes, sem mantê-los

<!-- l10n: source=concepts/pre-open.md source-sha256=01515e8832e018b6911df5fb373a53a9226b0391683ca620cdc00c0c2c42e69e -->
> Tradução da [página original em inglês](../../concepts/pre-open.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Manter um arquivo o conserva na memória. A pré-abertura faz algo mais barato: abre o arquivo e o fecha de novo, em uma thread em segundo plano, para que o Windows o leia do disco para o seu próprio cache de arquivos antes de o OMSI pedi-lo. A memória não é reservada, o cache pertence ao sistema, e o arquivo pode sair dele mais tarde. A pré-abertura existe para arquivos de veículos porque a primeira abertura de um arquivo custa uma leitura completa dele em um disco frio, e os veículos são os arquivos que não são previsíveis a partir do mapa. Esta página descreve as duas formas: a pré-abertura planejada (`pre_open`) e a disparada pelo primeiro uso de um veículo (`pre_open_on_first_use`).

Páginas relacionadas: [escopos de veículos](vehicle-scopes.md), [memória e orçamento](memory-and-budget.md), [programa de sessão](session-program.md), [referência de configurações](../reference/settings.md#pre-open), [diagnosticando lentidão](../guides/diagnosing-slowness.md).

<a id="how-a-file-is-opened-ahead"></a>
## Como um arquivo é aberto antes

O módulo abre cada arquivo com acesso de leitura e compartilhamento total (leitura, escrita e exclusão), mede quanto tempo a abertura levou e o fecha. Uma abertura mais lenta que 1,5 ms conta como fria; o tamanho do arquivo é somado a um total de dados frios. Quando o total atinge o teto, a pré-abertura para. O trabalho é feito por um pequeno número de threads que rodam com prioridade de E/S em segundo plano, para que as leituras do próprio jogo passem na frente. A cada 16 arquivos as threads verificam a memória livre e param quando ela está abaixo do piso de carregamento ([memória e orçamento](memory-and-budget.md)).

No fim, o log tem uma linha com as contagens: `pre-open: <n> files: <w> warm, <c> cold (<size>), <f> failed in <s> s`, terminando com `; cold data limit reached` ou `; free memory low` quando um desses motivos a interrompeu.

<a id="planned-pre-open-pre_open"></a>
## Pré-abertura planejada (`pre_open`)

Ela começa depois que os veículos do usuário foram tratados (esperando até três minutos por eles) e depois que a previsão de tiles ficou ociosa (até um minuto). Nunca abre arquivos que já estão mantidos ou que o módulo nunca mantém.

| `pre_open` | Arquivos abertos antes |
| --- | --- |
| `none` | Nada. |
| `learned` (padrão) | Os arquivos abaixo de `Vehicles\` que sessões anteriores deste mapa usaram (a lista aprendida) e os que o jogo usou até agora nesta sessão, sem duplicatas. |
| `all` | Todo arquivo dos veículos que o módulo consideraria com `user_vehicles=all`, com o escopo `pre_open_scope` (padrão `load`). |
| `list` | Os arquivos das pastas em `pre_open_list` (separadas por `;`), com o escopo `pre_open_scope`. Uma pasta que é um veículo da IA do mapa é pulada. |

Limites: `pre_open_max_mb` (padrão 2048) são os dados frios após os quais ela para, e `pre_open_threads` (padrão 2) é o número de threads. Na primeira abertura de um mapa não há lista aprendida, então `learned` não tem nada a fazer além dos arquivos que o jogo abre nesse meio-tempo.

<a id="on-first-use-pre_open_on_first_use"></a>
## No primeiro uso (`pre_open_on_first_use`)

Quando o OMSI usa pela primeira vez um arquivo de veículo (um `.bus` ou `.ovh` abaixo de `Vehicles\<folder>\`), normalmente quando começa a pré-visualização de um veículo, o módulo abre o restante da pasta desse veículo em paralelo, antes do jogo. Isso acontece uma vez por pasta e sessão, nunca para as pastas da IA do mapa (essas são mantidas), e é pulado quando pelo menos 90 % dos arquivos da pasta já estão mantidos.

A ordem dos arquivos é: scripts e outros arquivos de texto primeiro, depois modelos (`.o3d`), depois texturas e, por último, as texturas de repinturas (pastas chamadas `skins`, `pinturas`, `paint` ou `lackierung`). Ele pula sons (`.wav .ogg .mp3 .flac`), arquivos `.ini`, arquivos de trabalho e arquivos compactados.

Limites: `pre_open_first_use_max_mb` (padrão 400) são os dados frios após os quais ele para, e `pre_open_first_use_threads` (padrão 4) é o número de threads. A thread roda com prioridade abaixo do normal e termina quando acaba.

<a id="cost-and-safety"></a>
## Custo e segurança

- Abrir um arquivo para leitura não o altera. Nada é gravado.
- O custo são leituras de disco em prioridade de segundo plano e um pouco de CPU. Com um disco rápido e um veículo já no cache de arquivos, as aberturas são quentes (abaixo de 1,5 ms) e custam quase nada.
- A pré-abertura é uma dica: o cache de arquivos pode descartar os dados antes de o OMSI pedi-los, e a configuração não garante um acerto.

<a id="next"></a>
## Próximos passos

[Segurança](safety.md) · [Escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md) · [Referência de configurações](../reference/settings.md)
