# Memória, o orçamento e os pisos

<!-- l10n: source=concepts/memory-and-budget.md source-sha256=9f5cca209629493c89667255c0177b2daf8fc463914a0f7972eb2ac75ef84fd4 -->
> Tradução da [página original em inglês](../../concepts/memory-and-budget.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Esta página explica o que "mantido em memória" significa para o Omsi Prefetch, por que o módulo mantém arquivos na RAM, qual o tamanho do orçamento, quais pisos o impedem de tomar memória de que o seu sistema precisa, quais arquivos são gastos primeiro e como ele devolve memória. Todo número foi extraído do código; as configurações que os movem estão na [referência de configurações](../reference/settings.md#memory).

Páginas relacionadas: [como o prefetch funciona](how-it-works.md), [programa de sessão](session-program.md), [escopos de veículos](vehicle-scopes.md), [escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md), [diagnosticando lentidão](../guides/diagnosing-slowness.md).

<a id="what-held-means"></a>
## O que "mantido" significa

O preloader mantém um arquivo mapeando-o somente para leitura no seu próprio espaço de endereçamento e tocando em cada página. Uma solicitação pede ao Windows que traga o intervalo inteiro (`PrefetchVirtualMemory`), e depois uma leitura por página de 4 KiB garante que cada página esteja presente. O handle do arquivo é fechado imediatamente; o mapeamento mantém os dados.

Isso tem quatro efeitos que explicam o projeto:

- **A quantidade é exata.** As páginas pertencem ao processo do preloader, portanto a quantidade mantida é um número que o módulo conhece e pode comparar com o orçamento. Não é uma estimativa do que o sistema poderia colocar em cache.
- **O Windows não entrega as páginas a outra coisa** enquanto o jogo lê os mesmos arquivos pelas suas próprias aberturas, como poderia fazer com o cache de arquivos comum.
- **O OMSI nunca encontra um conflito de compartilhamento.** O preloader abre os arquivos com compartilhamento de leitura, escrita e exclusão e não guarda o handle, de modo que o OMSI pode abrir o mesmo arquivo para leitura ou escrita como faria sem o módulo.
- **Um arquivo mapeado não pode ser truncado nem substituído.** É por isso que o módulo nunca mantém um arquivo que o jogo grava e que libera um arquivo mantido no instante em que o OMSI não consegue substituí-lo ([segurança](safety.md)).

O módulo também pede ao Windows que eleve o mínimo do working set do preloader para o orçamento mais 256 MB, para que as páginas mantidas permaneçam residentes. O pedido é suave: sob pressão real de memória, o Windows ainda pode retomar páginas. Se o pedido for recusado, o log informa. A memória mantida aparece no Gerenciador de Tarefas como o working set do `Prefetch.Preloader.exe`; é memória apoiada em arquivo, não memória alocada, e o Windows pode reutilizá-la para outros programas se ela for liberada.

<a id="why-hold-files-in-ram"></a>
## Por que manter arquivos na RAM

O estudo de onde este módulo vem mediu o custo das chamadas de arquivo do OMSI, não o do disco ([visão geral](../getting-started/overview.md)). Com o arquivo já na memória, o custo restante de uma chamada é minúsculo; sem isso, a primeira leitura de um arquivo frio é uma leitura do disco e cada pequena leitura seguinte é uma chamada de sistema que pode ter de esperar. O preloader paga o custo da leitura a frio antes de o OMSI iniciar (e em segundo plano enquanto o OMSI roda), onde isso não congela o jogo.

<a id="the-budget"></a>
## O orçamento

O orçamento é a quantidade de memória que o preloader pode manter. Ele é fixado no início da preparação.

| Configuração | Efeito |
| --- | --- |
| `ram_budget_mb` = `auto` (padrão) | 70 % da memória física livre naquele momento, depois de reservar `reserve_mb` (padrão 4096 MB) para o OMSI e o sistema, no máximo 12.288 MB. Se houver menos que `reserve_mb` livres, o orçamento é zero. |
| `ram_budget_mb` = um número | Essa quantidade de MB, entre 256 e 12.288. A memória livre não é consultada para o tamanho, somente para os pisos abaixo. |

A fórmula automática é `0.7 x (free MB - reserve_mb)`, nunca negativa, nunca maior que 12.288. Exemplos:

| Memória livre no início | `reserve_mb` | Orçamento automático |
| ---: | ---: | ---: |
| 3.000 MB | 4096 | 0 (nada é mantido; o log diz `no memory to hold anything`) |
| 6.000 MB | 4096 | 1.332 MB |
| 11.000 MB | 4096 | 4.832 MB |
| 20.000 MB | 4096 | 11.132 MB |
| 40.000 MB | 4096 | 12.288 MB (o máximo) |

O teto de 12 GB vale tanto para o orçamento automático quanto para o explícito. Um orçamento zero não desliga o módulo: o read-ahead, o write-behind e o aprendizado continuam funcionando ([degradação](#when-there-is-no-budget)).

Os arquivos são mantidos inteiros. Um arquivo que não cabe no que resta do orçamento é pulado, e arquivos menores depois dele ainda podem caber. A preparação e a previsão tratam o orçamento como cheio quando resta menos de 1 MiB.

<a id="the-vehicle-cap"></a>
### O teto de veículos

`vehicle_budget_mb` (padrão 0, significando que apenas `ram_budget_mb` limita) limita a memória que os veículos de IA do mapa podem manter e, separadamente, a memória que os veículos do usuário podem manter. Um veículo só é mantido se tudo o que ele ainda precisa couber sob o teto; um veículo que não cabe é pulado inteiro.

<a id="the-floors"></a>
## Os pisos

Um piso é a quantidade de memória livre abaixo da qual o módulo para de carregar, para que manter arquivos nunca deixe o sistema ou o OMSI sem memória.

| Piso | Valor | O que faz |
| --- | --- | --- |
| Piso de carregamento (`stop_below_free_mb`) | `auto`: 2.048 MB com o orçamento automático, 1.024 MB com um `ram_budget_mb` explícito. Ou um número de 256 a 65.536. | Quando a memória física livre está abaixo dele, nenhum arquivo é mantido e a pré-abertura para. A preparação termina (`free memory low`); a previsão espera e tenta de novo. |
| Limiar de liberação da guarda | 1.024 MB (fixo) | Quando a memória livre cai abaixo dele, a guarda libera arquivos mantidos que o OMSI não usou, até que cerca de 1.536 MB fiquem livres. |
| Limiar de retomada da guarda | 2.560 MB (fixo) | Depois que a guarda marcou a memória como baixa, o carregamento só é retomado quando a memória livre fica acima dele. |

Um orçamento explícito é escolha sua, por isso o módulo deixa o carregamento ir mais longe com ele (1.024 MB) do que com o automático (2.048 MB). Aumente `stop_below_free_mb` em uma máquina que roda outros programas ao lado do OMSI.

<a id="where-the-budget-goes-first"></a>
## Para onde o orçamento vai primeiro

A preparação gasta o orçamento nesta ordem, e as camadas mantêm essa ordem depois:

| Ordem | Camada | O quê |
| ---: | --- | --- |
| 1 | learned | Arquivos que sessões anteriores do mapa usaram, na ordem do primeiro uso. |
| 2 | map | Os arquivos citados pelo `global.cfg` e os arquivos do mapa inteiro. |
| 3 | tiles | Os tiles mais próximos do início e depois mais tiles até `tile_radius`; durante a sessão, os tiles ao redor da posição estimada. |
| 4 | AI | Os veículos de IA do mapa e os objetos e pessoas que eles posicionam, conforme `ai_vehicles`. |
| 5 | user | Os veículos do usuário escolhidos por `user_vehicles`; mantidos durante a sessão. |

O mesmo arquivo é mantido uma vez, na camada que o obteve primeiro. Dentro de uma camada, a ordem é a da lista.

<a id="the-memory-guard"></a>
## A guarda de memória

A cada dois segundos a guarda lê a memória livre. Abaixo de 1.024 MB ela marca a memória como baixa (o que interrompe todos os caminhos de carregamento) e libera arquivos mantidos, **apenas arquivos que o OMSI não usou**, começando pela camada de menor valor (user, depois AI, depois tiles, depois map, depois learned) e pelos maiores arquivos dentro de uma camada, até que cerca de 1.536 MB fiquem livres. Arquivos que o OMSI usou nunca são liberados pela guarda. Quando a memória livre depois fica acima de 2.560 MB, a guarda limpa a marca e registra `loading resumes`.

Nada mais libera memória durante uma sessão, exceto a liberação de um arquivo que o jogo grava ou não consegue substituir, e o fim da sessão. Em particular, o módulo não descarta tiles atrás do jogador quando o orçamento está cheio ([limitações conhecidas](../reference/known-limitations.md)).

<a id="when-there-is-no-budget"></a>
## Quando não há orçamento

Se o orçamento automático é zero, a preparação é pulada e o log informa. O OMSI inicia assim que o programa está pronto, o componente nativo continua fazendo read-ahead e write-behind, o programa de sessão continua aprendendo o que o jogo usa, e nada é mantido. Uma máquina com pouca memória pode, portanto, conservar parte do ganho; veja [escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md) para um orçamento explícito em máquinas pequenas.

<a id="next"></a>
## Próximos passos

[Escopos de veículos](vehicle-scopes.md) · [Pré-abertura](pre-open.md) · [Escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md)
