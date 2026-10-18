# Referência de configurações

<!-- l10n: source=reference/settings.md source-sha256=166dfef9707242117fe37d5722e678fcba3f221d209c627afd30d0506f6dcebd -->
> Tradução da [página original em inglês](../../reference/settings.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

O Omsi Prefetch declara 29 configurações no seu manifesto. Esta página lista cada chave com o seu tipo, padrão, limites e flag, depois descreve o que cada uma faz, como interage com as outras e mostra um exemplo. A tabela de resumo é gerada a partir de `prefetch\module.template.json` (a fonte do `module.json` empacotado) e conferida com ele por `tools\Check-Docs.ps1`; o comportamento descrito foi verificado contra o código do programa de sessão (`prefetch\preloader\Protocol.cs` lê e limita cada valor) e do componente nativo (`prefetch\native\Module.cpp`).

Estabilidade: o módulo é `1.0.0-beta.1`; o recurso de Runtime Modules do OmsiLaunch é `EXPERIMENTAL`. Páginas relacionadas: a referência de Runtime Modules do OmsiLaunch (`docs\reference\runtime-modules.md` do OmsiLaunch, offline em `<OMSI root>\.omsilaunch\docs\reference\runtime-modules.md`: configurações, o arquivo de configurações, precedência), [escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md), [sobrescritas pela linha de comando](../guides/command-line-overrides.md), [perfis de sessão](../guides/session-profiles.md), [conceitos: memória e orçamento](../concepts/memory-and-budget.md), [escopos de veículos](../concepts/vehicle-scopes.md), [pré-abertura](../concepts/pre-open.md).

<a id="how-settings-reach-the-module"></a>
## Como as configurações chegam ao módulo

O módulo não tem um arquivo ini próprio. Toda configuração chega a ele por meio do OmsiLaunch, que a valida contra o manifesto e recusa um valor fora dos limites. Em ordem de precedência, o valor efetivo de uma configuração é:

1. a requisição: a flag do próprio módulo (`/prefetch-ram:6144`), `/module-setting:omsi-prefetch.<key>=<value>`, `LaunchSpec.Modules.Settings` (`"omsi-prefetch.<key>": "<value>"`) ou `modules.omsi-prefetch.settings` do preset de perfil de sessão selecionado. Uma chave que o preset define não pode também ser definida na linha de comando (`OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT`);
2. a seção `[omsi-prefetch]` de `<OMSI root>\.omsilaunch\modules.ini` (`key=value`, um por linha; linhas de comentário começam com `;` ou `#`; um comentário não pode vir depois de um valor);
3. o padrão do manifesto na tabela abaixo.

Os valores são normalizados pelo OmsiLaunch: booleanos aceitam `true`/`false`, `yes`/`no`, `on`/`off`, `1`/`0`; escolhas são comparadas sem diferenciar maiúsculas de minúsculas; inteiros são conferidos contra os seus limites; texto não tem caracteres de controle e tem um limite de comprimento. Um valor inválido pedido, ou uma chave que o módulo não declara, torna o plano não executável (`OL_E_MODULE_SETTING_INVALID`); uma entrada inutilizável em `modules.ini` é ignorada com `OL_W_MODULE_SETTING_IGNORED`. `/list:RuntimeModules` mostra cada configuração com o seu tipo, padrão e flag. O próprio módulo limita os números uma segunda vez aos limites abaixo (ele não pode receber do OmsiLaunch um valor fora deles).

<a id="summary"></a>
## Resumo

| Chave | Tipo | Padrão | Intervalo ou escolhas | Flag |
| --- | --- | --- | --- | --- |
| `ram_budget_mb` | inteiro | `auto` | 256 a 12288 ou `auto` | `/prefetch-ram` |
| `monitor` | booleano | `false` | `true` ou `false` | `/prefetch-monitor` |
| `reserve_mb` | inteiro | `4096` | 0 a 65536 | `/prefetch-reserve` |
| `stop_below_free_mb` | inteiro | `auto` | 256 a 65536 ou `auto` | `/prefetch-stop-below` |
| `vehicle_budget_mb` | inteiro | `0` | 0 a 65536 | `/prefetch-vehicle-ram` |
| `read_ahead` | booleano | `true` | `true` ou `false` |  |
| `write_behind` | booleano | `true` | `true` ou `false` |  |
| `read_only_open` | booleano | `true` | `true` ou `false` |  |
| `read_only_folders` | texto | `vehicles\` | até 512 caracteres |  |
| `tile_radius` | inteiro | `12` | 0 a 64 |  |
| `ai_vehicles` | escolha | `complete` | `off`, `essential`, `load`, `complete` |  |
| `user_vehicles` | escolha | `none` | `none`, `list`, `all` |  |
| `user_vehicle_list` | texto | (vazio) | até 4096 caracteres |  |
| `user_vehicle_scope` | escolha | `essential` | `essential`, `load`, `complete` |  |
| `fleet_scope` | escolha | `load` | `essential`, `load`, `complete` |  |
| `sounds` | booleano | `true` | `true` ou `false` |  |
| `pre_open` | escolha | `learned` | `none`, `learned`, `all`, `list` | `/prefetch-pre-open` |
| `pre_open_scope` | escolha | `load` | `essential`, `load`, `complete` |  |
| `pre_open_list` | texto | (vazio) | até 4096 caracteres |  |
| `pre_open_max_mb` | inteiro | `2048` | 16 a 65536 |  |
| `pre_open_threads` | inteiro | `2` | 1 a 16 |  |
| `pre_open_first_use_max_mb` | inteiro | `400` | 16 a 65536 |  |
| `pre_open_first_use_threads` | inteiro | `4` | 1 a 16 |  |
| `pre_open_on_first_use` | booleano | `true` | `true` ou `false` |  |
| `learn` | booleano | `true` | `true` ou `false` |  |
| `survey` | booleano | `true` | `true` ou `false` |  |
| `first_open_notice` | booleano | `true` | `true` ou `false` |  |
| `debug_log` | booleano | `false` | `true` ou `false` | `/prefetch-debug` |
| `data_limit_mb` | inteiro | `256` | 16 a 4096 |  |

Uma flag é escrita `/<flag>:<value>`; a flag de um booleano sozinha (`/prefetch-monitor`) significa `true`. `enabled` não é uma configuração do módulo: o OmsiLaunch liga ou desliga um módulo da mesma forma para todos os módulos ([desligando](../guides/turning-off.md)).

Nos exemplos, a forma de linha de comando vale para uma sessão; a mesma chave e o mesmo valor funcionam em `modules.ini` (`key=value` sob `[omsi-prefetch]`) e em um preset (`modules.omsi-prefetch.settings`). Os exemplos com aspas simples são para o PowerShell, onde um argumento que contém `;` precisa de aspas; no `cmd.exe`, use aspas duplas em volta do argumento inteiro.

<a id="memory"></a>
## Memória

<a id="ram_budget_mb"></a>
### `ram_budget_mb`

A memória que o preloader pode manter, em MB. `auto` (padrão) é 70 % da memória física livre no início, depois de reservar `reserve_mb` para o OMSI, no máximo 12.288 MB; um número de 256 a 12.288 é usado como está. O teto de 12 GB é o mesmo para ambos. Interações: `reserve_mb` só é usado com `auto`; com um número, `stop_below_free_mb` passa a ter padrão de 1.024 MB em vez de 2.048 MB. O orçamento explícito não é reduzido quando há pouca memória livre; o piso de carregamento interrompe o carregamento no lugar ([memória e orçamento](../concepts/memory-and-budget.md)). Se houver menos que `reserve_mb` livres com `auto`, o orçamento é zero e nada é mantido.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-ram:6144
```

<a id="reserve_mb"></a>
### `reserve_mb`

Memória reservada para o OMSI e o sistema quando `ram_budget_mb` é `auto`, em MB (0 a 65.536, padrão 4.096). O orçamento automático é `0.7 x (free - reserve_mb)`. Ignorada quando `ram_budget_mb` é um número. Aumente-a em uma máquina que roda outros programas ao lado do OMSI; reduzi-la abaixo do padrão dá ao módulo mais da memória de uma máquina pequena e deixa menos para o OMSI.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-reserve:6144
```

<a id="stop_below_free_mb"></a>
### `stop_below_free_mb`

O carregamento para quando a memória física livre está abaixo deste valor, em MB (256 a 65.536). `auto` (padrão) é 2.048 com o orçamento automático e 1.024 com um `ram_budget_mb` explícito. O piso interrompe a preparação, a previsão de tiles, os veículos do usuário e a pré-abertura; não libera o que já está mantido (isso é feito pela guarda, em 1.024 MB fixos: [memória e orçamento](../concepts/memory-and-budget.md#the-memory-guard)).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-stop-below:3072
```

<a id="vehicle_budget_mb"></a>
### `vehicle_budget_mb`

Teto, em MB, do que os veículos de IA do mapa podem manter e, separadamente, do que os veículos do usuário podem manter (0 a 65.536, padrão 0). 0 significa que apenas `ram_budget_mb` os limita. Um veículo que não cabe no teto é pulado inteiro. Não limita os tiles, os arquivos do mapa inteiro, os arquivos aprendidos nem a pré-abertura.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-vehicle-ram:1024
```

<a id="the-native-features"></a>
## Os recursos nativos

Estas três chaves atuam dentro do OMSI. Elas só começam quando todos os hooks de que precisam estão instalados ([shim nativo](../concepts/native-shim.md)). Desligar as três deixa apenas os relatórios de uso.

<a id="read_ahead"></a>
### `read_ahead`

Padrão `true`. Atende as leituras byte a byte do OMSI a partir de blocos de 64 KiB. Desligue para descartá-lo ao diagnosticar; não há motivo para desligá-lo de outra forma.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.read_ahead=false
```

<a id="write_behind"></a>
### `write_behind`

Padrão `true`. Reúne em blocos de 256 KiB as gravações byte a byte do cache de terreno (`.rdy`). Com ele ligado, terminar o OMSI à força perde os bytes ainda em buffer; o arquivo é um cache regenerável ([segurança](../concepts/safety.md)). Desligue-o se você costuma encerrar sessões terminando o OMSI à força e prefere conservar todo byte de `.rdy` (você abre mão do ganho no tempo de `WriteFile`).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.write_behind=false
```

<a id="read_only_open"></a>
### `read_only_open`

Padrão `true`. Abre assets abaixo de `read_only_folders` sem acesso de escrita, o que poupa uma atualização síncrona de metadados quando o handle é fechado. Se o OMSI gravar nesse handle mesmo assim, o arquivo é reaberto com o acesso que ele pediu.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.read_only_open=false
```

<a id="read_only_folders"></a>
### `read_only_folders`

Pastas, relativas à pasta do OMSI e separadas por `;`, cujos assets são abertos somente leitura (texto, até 512 caracteres, padrão `vehicles\`). As entradas são aparadas, comparadas sem diferenciar maiúsculas de minúsculas, `/` é lida como `\` e uma `\` final é acrescentada quando falta. Um caminho corresponde quando começa com a pasta ou a contém como segmento (assim caminhos absolutos também correspondem). Somente as extensões de asset listadas em [shim nativo](../concepts/native-shim.md#read-only-open) são rebaixadas. Um valor vazio é lido como o padrão. Só é efetiva com `read_only_open` ligado.

```ini
[omsi-prefetch]
read_only_folders=vehicles\;sceneryobjects\
```

Não termine um valor entre aspas na linha de comando com `\` (a interpretação de argumentos do Windows transforma `\"` em uma aspa); defina esta chave em `modules.ini` ou em um preset.

<a id="tiles-and-learning"></a>
## Tiles e aprendizado

<a id="tile_radius"></a>
### `tile_radius`

Até onde ao redor do início e ao redor da posição estimada do jogador os tiles são mantidos, em tiles (0 a 64, padrão 12). É um quadrado: um tile é incluído quando está dentro do raio nos dois eixos, então 12 significa um bloco de 25 por 25. Antes do OMSI, o anel mais próximo (raio 2, ou `tile_radius` se menor) é mantido primeiro e depois o restante até o raio, depois dos veículos de IA. Durante a sessão, a previsão mantém os tiles ao redor da posição estimada, do mais próximo ao mais distante, até o raio. 0 mantém apenas o próprio tile. Precisa de `survey`.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.tile_radius=6
```

<a id="learn"></a>
### `learn`

Padrão `true`. Lembra o que cada sessão de um mapa usou (a lista aprendida) e o que gravou (a lista de escrita), para trazer a primeira antes da sessão seguinte. Com `false`, as listas ainda são lidas e usadas, mas não atualizadas nem salvas. Interação: com `learn` e `survey` ambos desligados, o módulo nunca salva nada sobre um mapa, toda sessão conta como uma primeira abertura e a janela de primeira abertura aparece toda vez (a menos que `first_open_notice` esteja desligado).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.learn=false
```

<a id="survey"></a>
### `survey`

Padrão `true`. Lê os tiles do mapa para saber do que cada um precisa (o índice de tiles): permite que o módulo mantenha os tiles perto do início antes do OMSI e os tiles ao redor do jogador enquanto ele roda. Com `false`, nenhum índice de tiles é carregado, calculado nem salvo, e o módulo não mantém arquivos de tile (nem antes do OMSI nem durante a sessão); `tile_radius` não tem efeito. A lista aprendida ainda traz os tiles que você usou antes.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.survey=false
```

<a id="vehicles"></a>
## Veículos

<a id="ai_vehicles"></a>
### `ai_vehicles`

O que dos veículos de IA do mapa é mantido (padrão `complete`): `off`, `essential`, `load` ou `complete` ([escopos de veículos](../concepts/vehicle-scopes.md)). `off` também deixa de fora os objetos e pessoas que a IA do mapa posiciona. Mantidos na preparação, antes de o OMSI iniciar.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.ai_vehicles=load
```

<a id="user_vehicles"></a>
### `user_vehicles`

Quais dos veículos do usuário são mantidos enquanto o OMSI roda (padrão `none`): `none`, `list` (as pastas de `user_vehicle_list`) ou `all` (toda pasta de veículo válida que a IA do mapa não usa). Com `ai_vehicles` em `off`, `all` inclui os veículos que o mapa teria usado.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.user_vehicles=all
```

<a id="user_vehicle_list"></a>
### `user_vehicle_list`

Pastas de veículos mantidas com `user_vehicles=list`, separadas por `;`, em ordem (texto, até 4096 caracteres, padrão vazio). Os nomes são nomes de pastas diretamente sob `Vehicles\`, comparados sem diferenciar maiúsculas de minúsculas. Um nome que não é uma pasta de veículo válida, ou que é um veículo da IA do mapa, é pulado e o log informa. Usada somente com `user_vehicles=list`.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.user_vehicles=list '/module-setting:omsi-prefetch.user_vehicle_list=MAN_Lions_City;Citaro_G'
```

<a id="user_vehicle_scope"></a>
### `user_vehicle_scope`

O que de cada veículo do usuário é mantido (padrão `essential`): `essential`, `load` ou `complete`. Usado para os veículos escolhidos por `user_vehicles`, exceto aqueles que carregam o horário do mapa (veja `fleet_scope`).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.user_vehicle_scope=load
```

<a id="fleet_scope"></a>
### `fleet_scope`

O que de cada veículo do usuário que carrega o horário do mapa (um arquivo `.hof` com o nome da pasta do mapa, na pasta do próprio veículo) é mantido (padrão `load`). Esses veículos são a frota do próprio mapa e são mantidos primeiro entre os veículos do usuário. Só importa com `user_vehicles` diferente de `none`.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.fleet_scope=complete
```

<a id="sounds"></a>
### `sounds`

Padrão `true`. Inclui os arquivos `.wav` que veículos e objetos citam (e, com o escopo `complete`, os arquivos `.wav` de uma pasta). Arquivos `.ogg` e outros de áudio não são seguidos a partir de citações; o escopo `complete` os mantém como parte da pasta, qualquer que seja o valor de `sounds`. Desligue para economizar memória quando os sons carregam rápido o bastante do disco.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.sounds=false
```

<a id="pre-open"></a>
## Pré-abertura

<a id="pre_open"></a>
### `pre_open`

Arquivos de veículos abertos antes do jogo sem serem mantidos (padrão `learned`): `none`, `learned` (os arquivos de veículos que sessões anteriores usaram), `all` (todo veículo de usuário válido, com `pre_open_scope`) ou `list` (as pastas de `pre_open_list`). Flag `/prefetch-pre-open:<mode>`. Veja [pré-abertura](../concepts/pre-open.md).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-pre-open:none
```

<a id="pre_open_scope"></a>
### `pre_open_scope`

O que de cada veículo é aberto antes com `pre_open` `all` ou `list` (padrão `load`): `essential`, `load` ou `complete`. Ignorado com `none` e `learned`.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-pre-open:all /module-setting:omsi-prefetch.pre_open_scope=essential
```

<a id="pre_open_list"></a>
### `pre_open_list`

Pastas de veículos abertas antes com `pre_open=list`, separadas por `;` (texto, até 4096 caracteres, padrão vazio). Uma pasta que é um veículo da IA do mapa é pulada.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-pre-open:list '/module-setting:omsi-prefetch.pre_open_list=MAN_Lions_City;Citaro_G'
```

<a id="pre_open_max_mb"></a>
### `pre_open_max_mb`

Dados frios, em MB, após os quais a pré-abertura planejada para (16 a 65.536, padrão 2048). Dados frios são o tamanho dos arquivos cuja abertura levou 1,5 ms ou mais.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.pre_open_max_mb=1024
```

<a id="pre_open_threads"></a>
### `pre_open_threads`

Threads da pré-abertura planejada (1 a 16, padrão 2). Rodam com prioridade de E/S em segundo plano.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.pre_open_threads=4
```

<a id="pre_open_on_first_use"></a>
### `pre_open_on_first_use`

Padrão `true`. Quando a pré-visualização de um veículo começa (o primeiro uso do seu `.bus` ou `.ovh`), abre a pasta dele antes do jogo, uma vez por pasta e sessão. Pulada para os veículos de IA do mapa e quando 90 % da pasta já está mantida.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.pre_open_on_first_use=false
```

<a id="pre_open_first_use_max_mb"></a>
### `pre_open_first_use_max_mb`

Dados frios, em MB, após os quais a abertura da pasta de um veículo no seu primeiro uso para (16 a 65.536, padrão 400). Aplicado por pasta.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.pre_open_first_use_max_mb=800
```

<a id="pre_open_first_use_threads"></a>
### `pre_open_first_use_threads`

Threads que abrem a pasta de um veículo no seu primeiro uso (1 a 16, padrão 4).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.pre_open_first_use_threads=2
```

<a id="windows-and-diagnostics"></a>
## Janelas e diagnóstico

<a id="monitor"></a>
### `monitor`

Padrão `false`. Mostra a janela que acompanha o que está mantido em memória. Ela só abre quando o OmsiLaunch pede: a flag `/prefetch-monitor`, `/module-setting:omsi-prefetch.monitor=true`, `monitor=true` em `modules.ini` ou em um preset. Fechar a janela a oculta até o fim da sessão ([programa de sessão](../concepts/session-program.md#windows)).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-monitor
```

<a id="first_open_notice"></a>
### `first_open_notice`

Padrão `true`. Mostra a pequena janela que explica a espera na primeira abertura de um mapa ([primeira execução](../getting-started/first-run.md#the-first-open-window)). Com `false` não há janela; a espera é a mesma.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.first_open_notice=false
```

<a id="debug_log"></a>
### `debug_log`

Padrão `false`. Grava no log as linhas por arquivo e por veículo: veículos mantidos ou pulados, arquivos liberados a pedido do OMSI, liberações da guarda, pré-aberturas no primeiro uso ([formato do log](log-format.md)). O log normal tem algumas dezenas de linhas por sessão, no máximo 1 MiB por arquivo, três arquivos mantidos; as linhas extras contam contra o mesmo limite de 1 MiB. Flag `/prefetch-debug`.

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-debug
```

<a id="data"></a>
## Dados

<a id="data_limit_mb"></a>
### `data_limit_mb`

Limite, em MB, dos dados do módulo, isto é, do que cada mapa lhe ensinou (16 a 4096, padrão 256). Quando uma sessão começa e os dados dos mapas são maiores, os mapas usados há mais tempo são removidos primeiro, nunca o atual. Os logs são limitados separadamente e não são contados ([dados aprendidos](../concepts/learned-data.md#bounds-and-clean-up)).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.data_limit_mb=64
```

<a id="related"></a>
## Relacionados

[Sobrescritas pela linha de comando](../guides/command-line-overrides.md) · [Perfis de sessão](../guides/session-profiles.md) · [Escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md) · [Arquivos e diretórios](files-and-directories.md)
