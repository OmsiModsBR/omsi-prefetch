# Escolhendo configurações pelo tamanho da memória

<!-- l10n: source=guides/choosing-settings-by-memory.md source-sha256=7d7f7e69c8407a39a1f35defdcb47456a92bac16bf024d8fd805b188799d96fc -->
> Tradução da [página original em inglês](../../guides/choosing-settings-by-memory.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Os padrões foram feitos para uma máquina com bastante memória livre. Este guia dá valores iniciais para máquinas com 8, 16, 32 e 64 GB de RAM, explica como o orçamento automático se comporta em cada uma e mostra como ajustar a partir do que o log informa. Os valores são pontos de partida derivados de como o orçamento, os pisos e os escopos funcionam ([memória e orçamento](../concepts/memory-and-budget.md), [escopos de veículos](../concepts/vehicle-scopes.md)); o ganho publicado foi medido em uma máquina (n = 1), então ajuste na sua ([medindo o ganho](../getting-started/measuring-the-gain.md)).

Páginas relacionadas: [referência de configurações](../reference/settings.md), [sobrescritas pela linha de comando](command-line-overrides.md), [perfis de sessão](session-profiles.md), [diagnosticando lentidão](diagnosing-slowness.md).

<a id="what-decides-what-you-get"></a>
## O que decide o que você recebe

1. **O orçamento automático** é `0.7 x (free memory - reserve_mb)` no início, no máximo 12.288 MB, e zero quando há menos de `reserve_mb` (4.096 MB) livres. Ele segue a memória *livre*, não a memória instalada: um navegador e uma chamada de vídeo abertos tomam a sua parte antes de o módulo ler o valor.

   | Memória livre no início | Orçamento automático |
   | ---: | ---: |
   | 4.000 MB ou menos | 0 |
   | 5.000 MB | 632 MB |
   | 8.000 MB | 2.732 MB |
   | 11.000 MB | 4.832 MB |
   | 20.000 MB | 11.132 MB |
   | 28.000 MB ou mais | 12.288 MB |

2. **Um `ram_budget_mb` explícito** (256 a 12.288) ignora a memória livre para o seu tamanho e reduz o piso de carregamento para 1.024 MB. É o que você quer em uma máquina pequena, em que o orçamento automático é zero ou minúsculo, e quando quer a mesma quantidade toda vez.
3. **O que você pede para ser mantido** (o escopo dos veículos de IA, os veículos do usuário, `tile_radius`) decide como o orçamento é gasto. Um orçamento pequeno demais para os escopos é gasto em ordem, de modo que os veículos no fim da ordem (os do usuário) são os que ficam de fora ([memória e orçamento](../concepts/memory-and-budget.md#where-the-budget-goes-first)).

O OMSI é um programa de 32 bits e usa alguns gigabytes no máximo; o módulo mantém `reserve_mb` livres para ele e para o Windows. Em qualquer máquina, feche programas que consomem muita memória antes de iniciar uma sessão e deixe pelo menos 4 GB de memória livre depois do orçamento.

<a id="starting-values"></a>
## Valores iniciais

| | 8 GB | 16 GB | 32 GB | 64 GB |
| --- | --- | --- | --- | --- |
| `ram_budget_mb` | `1536` | `4096` | `8192` | `12288` |
| `stop_below_free_mb` | `1536` | `2048` | `2048` | `2048` |
| `tile_radius` | `4` | `8` | `12` | `16` |
| `ai_vehicles` | `essential` | `load` | `complete` | `complete` |
| `vehicle_budget_mb` | `512` | `1536` | `0` | `0` |
| `user_vehicles` | `none` | `list` | `list` | `all` |
| `user_vehicle_scope` | | `essential` | `load` | `load` |
| `fleet_scope` | | `load` | `complete` | `complete` |
| `pre_open` | `learned` | `learned` | `learned` | `all` |
| `pre_open_max_mb` | `512` | `1024` | `2048` | `4096` |
| `pre_open_threads` | `1` | `2` | `2` | `4` |

Onde uma célula está em branco, a configuração não importa para aquele perfil. As configurações não listadas mantêm os padrões.

Coloque os valores onde lhe convier: a seção `[omsi-prefetch]` de `<OMSI root>\.omsilaunch\modules.ini` para todas as sessões, um preset de perfil de sessão para um mapa ou uma forma de jogar ([perfis de sessão](session-profiles.md)) ou a linha de comando para uma sessão ([sobrescritas pela linha de comando](command-line-overrides.md)).

<a id="8-gb"></a>
## 8 GB

Com 8 GB, a memória livre no início costuma ser de 4 a 5 GB, então o orçamento automático é zero ou algumas centenas de MB. Use um orçamento explícito e modesto e peça pouco:

```ini
[omsi-prefetch]
ram_budget_mb=1536
stop_below_free_mb=1536
tile_radius=4
ai_vehicles=essential
vehicle_budget_mb=512
user_vehicles=none
pre_open=learned
pre_open_max_mb=512
pre_open_threads=1
```

Por quê: o orçamento deixa mais de 3 GB para o Windows e o OMSI; um piso de 1.536 MB interrompe o carregamento antes de o sistema ficar sem memória; `essential` mantém apenas a parte necessária primeiro de cada veículo de IA; os veículos do usuário não são mantidos (a pré-abertura apenas aquece no cache de arquivos do sistema os arquivos que você usou antes, sem reservar memória). O read-ahead e o write-behind funcionam qualquer que seja o orçamento, de modo que uma máquina pequena conserva parte do ganho mesmo com orçamento zero ([memória e orçamento](../concepts/memory-and-budget.md#when-there-is-no-budget)). Se o sistema começar a paginar durante o carregamento, diminua primeiro o `ram_budget_mb`.

<a id="16-gb"></a>
## 16 GB

Cerca de 10 a 12 GB estão livres quando nada mais está rodando, o que dá um orçamento automático em torno de 4,8 a 5,5 GB. Um valor explícito de 4.096 MB torna a quantidade previsível:

```ini
[omsi-prefetch]
ram_budget_mb=4096
stop_below_free_mb=2048
tile_radius=8
ai_vehicles=load
vehicle_budget_mb=1536
user_vehicles=list
user_vehicle_list=Bus_You_Drive_1;Bus_You_Drive_2
user_vehicle_scope=essential
pre_open=learned
pre_open_max_mb=1024
```

Substitua a lista pelas pastas dos veículos que você realmente dirige (nomes de pastas sob `Vehicles\`). Deixe `ram_budget_mb` de fora se preferir o orçamento automático e mantenha a máquina livre de outros programas grandes.

<a id="32-gb"></a>
## 32 GB

O orçamento automático chega ao teto de 12.288 MB quando a maior parte da memória está livre. Peça menos para deixar espaço para o restante do seu sistema:

```ini
[omsi-prefetch]
ram_budget_mb=8192
tile_radius=12
ai_vehicles=complete
user_vehicles=list
user_vehicle_list=Bus_You_Drive_1;Bus_You_Drive_2;Bus_You_Drive_3
user_vehicle_scope=load
fleet_scope=complete
pre_open=learned
pre_open_max_mb=2048
```

`complete` mantém a pasta inteira de cada veículo de IA, então é a maior pegada de IA; se o log mostrar `(budget reached)` enquanto muitos arquivos mantidos nunca foram usados, reduza para `load`.

<a id="64-gb"></a>
## 64 GB

O teto de orçamento de 12 GB é alcançado em qualquer máquina desse porte. Use-o, amplie a área e mantenha mais veículos:

```ini
[omsi-prefetch]
ram_budget_mb=12288
tile_radius=16
ai_vehicles=complete
user_vehicles=all
user_vehicle_scope=load
fleet_scope=complete
pre_open=all
pre_open_scope=load
pre_open_max_mb=4096
pre_open_threads=4
```

`user_vehicles=all` vai do menor para o maior depois da frota do próprio mapa; o orçamento é gasto primeiro com os tiles e a IA, de modo que uma coleção grande demais para 12 GB é simplesmente cortada no fim. A memória além de 12 GB fica para o cache de arquivos do próprio Windows, que também ajuda o OMSI.

<a id="adjusting-from-the-log"></a>
## Ajustando a partir do log

Depois de uma sessão, leia as linhas `prepared in` e `end:` ([formato do log](../reference/log-format.md)):

| Você vê | Significado | Tente |
| --- | --- | --- |
| `prepared in ... (budget reached)` e muitos arquivos `used outside memory` | O orçamento é pequeno demais para o que você usa. | Aumente `ram_budget_mb`; reduza os escopos do que você não usa. |
| `prepared in ... (budget reached)` e `used` é uma pequena parte de `files held` | O orçamento está sendo gasto com arquivos que você não usa. | Diminua `tile_radius`, `ai_vehicles` ou os escopos de veículos. |
| `prepared in ... (free memory low)` | O piso de carregamento interrompeu o carregamento. | Feche outros programas, diminua `ram_budget_mb` ou diminua `stop_below_free_mb` se aceitar menos folga. |
| `prepared in ... (time limit reached ...)` | Passaram-se cinco minutos de carregamento. | Reduza o que você mantém, ou aceite que o restante é trazido durante a sessão. |
| `no memory to hold anything` | O orçamento automático é zero. | Defina um `ram_budget_mb` explícito ([8 GB](#8-gb)). |
| `used outside memory` grande em uma segunda execução | A sua rota difere do que foi aprendido, ou a lista aprendida é rala. | Execute o mapa de novo; considere um `tile_radius` maior. |
| O Windows pagina ou engasga durante o carregamento | Há coisa demais mantida para a máquina. | Diminua `ram_budget_mb` e aumente `stop_below_free_mb`. |

Mude uma configuração de cada vez e compare as execuções como descreve [medindo o ganho](../getting-started/measuring-the-gain.md).

<a id="next"></a>
## Próximos passos

[Perfis de sessão](session-profiles.md) · [Sobrescritas pela linha de comando](command-line-overrides.md) · [Diagnosticando lentidão](diagnosing-slowness.md)
