# O programa de sessão

<!-- l10n: source=concepts/session-program.md source-sha256=6d15055770ae097e752d4e773c931bb20ee2d6557f2a793d7f44d80bb25207e8 -->
> Tradução da [página original em inglês](../../concepts/session-program.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

O `Prefetch.Preloader.exe` é o programa de sessão do módulo: um programa x64 que o OmsiLaunch inicia antes do OMSI, que mantém arquivos na memória durante a sessão e que acompanha a sessão até o fim. Esta página descreve as suas duas fases (antes do OMSI e enquanto o OMSI roda), as atividades que rodam na segunda fase, a previsão da posição em tiles, a guarda de memória, como ele para e os limites que o restringem. O protocolo geral de programa de sessão é especificado na referência de Runtime Modules do OmsiLaunch ("Session programs"); esta página trata do que este programa faz com ele.

Páginas relacionadas: [como o prefetch funciona](how-it-works.md), [memória e orçamento](memory-and-budget.md), [dados aprendidos](learned-data.md), [escopos de veículos](vehicle-scopes.md), [pré-abertura](pre-open.md), [segurança](safety.md), [formato do log](../reference/log-format.md).

<a id="start-and-context"></a>
## Início e contexto

O OmsiLaunch inicia o programa como `Prefetch.Preloader.exe --omsilaunch-session <session id>` a partir da pasta do módulo, dentro de um job object que o encerra junto com a sessão, e escreve o contexto como a primeira linha da sua entrada padrão. O programa se recusa a rodar sozinho (iniciado à mão, mostra uma caixa de mensagem e termina com o código 2) e termina com o código 2 quando o contexto não é um contexto `omsilaunch.module-session/v1` válido para o mesmo id de sessão.

Do contexto o programa usa a raiz da instalação, o diretório de dados do módulo, o nome do pipe da sessão, o mapa da sessão (`world.map`), a forma como a sessão começa (`world.mode`; para um mapa novo, também o índice do ponto de entrada apresentado), as configurações efetivas e os arquivos de que a transação da sessão é dona (`session_files`), que ele nunca mantém ([segurança](safety.md)).

<a id="phase-1-before-omsi-prepare"></a>
## Fase 1: antes do OMSI (preparar)

O programa trabalha nesta ordem e informa `ready` no fim; só depois disso o OmsiLaunch inicia o OMSI.

| Etapa | O que faz |
| --- | --- |
| Orçamento | Lê a memória física livre, calcula o orçamento ([memória e orçamento](memory-and-budget.md)) e tenta elevar o mínimo do seu working set para o orçamento mais 256 MB, para que o Windows mantenha residentes as páginas mantidas. Se isso for recusado, o log informa, e as páginas podem ser reduzidas sob pressão de memória. |
| Monitor | Abre a janela do monitor somente quando `monitor` é verdadeiro. |
| Pipe | Cria o pipe da sessão, para que o componente nativo do OMSI possa se conectar assim que carregar. |
| Mapa | Lê o `global.cfg`. Sem mapa na sessão, ou com um mapa que não pode ser lido, registra isso no log e pula o restante (o OMSI ainda inicia). |
| Dados | Abre a pasta de dados do mapa, poda os dados de mapas antigos quando os dados excedem `data_limit_mb`, carrega a lista de escrita, a lista aprendida e (com `survey`) o índice de tiles salvo. |
| Tiles iniciais | Determina os tiles em que a sessão começa: para um mapa novo, o tile do ponto de entrada apresentado (a ordem de apresentação do OMSI não é conhecida offline, então são usados tanto o ponto de entrada da ordem do arquivo quanto o da ordem dos nomes); para uma situação salva ou o último estado do mapa, os tiles de todos os pontos de entrada; sem ponto de entrada, o tile mais próximo do meio do mapa. |
| Janela de primeira abertura | Mostra-a quando é uma primeira abertura e `first_open_notice` está ligado ([primeira execução](../getting-started/first-run.md)). |
| Manter | Mantém arquivos nesta ordem: `learned`, `map`, `start tiles` (raio 2, ou `tile_radius` quando menor; somente com `survey`), os veículos de IA do mapa (a menos que `ai_vehicles` seja `off`), `tiles around the start` (até `tile_radius`; somente com `survey`). Cada etapa para assim que o orçamento está cheio, que cinco minutos se passaram desde o início da primeira etapa ou que a memória livre caiu abaixo do piso. |
| Relatório | Registra `prepared in ...`, emite o evento `prepared` e escreve `ready`. |

Se a preparação lançar uma exceção, o programa registra a falha, envia uma linha `error` ao OmsiLaunch (`OL_E_MODULE_OPERATION_FAILED`) e mesmo assim escreve `ready`: o OMSI inicia e o módulo degrada para o que já tiver sido mantido. O tempo que o controlador concede para `ready` é o `prepare_ms` do manifesto, 600.000 ms (dez minutos); o limite de carregamento do próprio programa é de cinco minutos, portanto em condições normais ele nunca chega a isso.

O que cada etapa mantém está descrito em [dados aprendidos](learned-data.md) (a primeira), [escopos de veículos](vehicle-scopes.md) (os veículos de IA) e abaixo (os arquivos do mapa inteiro e os tiles).

<a id="the-map-wide-files-and-the-tiles"></a>
### Os arquivos do mapa inteiro e os tiles

- **Arquivos do mapa inteiro**: tudo o que o `global.cfg` cita (texturas do solo, sistema de dinheiro, pacote de bilhetes) e todo arquivo da pasta do mapa, recursivamente, exceto os arquivos próprios dos tiles (`tile_*`), a pasta `chrono\`, `texture\map\`, arquivos de trabalho e arquivos compactados, e os arquivos que o jogo reescreve ([segurança](safety.md#what-is-never-held)).
- **Um tile**: os seus próprios arquivos (primeiro o `tile_X_Y.map`, depois os companheiros como `.terrain` e `.water`, na pasta do mapa e em `texture\map\`, nunca o cache `.rdy` e nunca os arquivos de streaming), depois todo objeto (`.sco`) e spline (`.sli`) que o arquivo do tile posiciona, cada um com tudo o que cita: modelos, texturas (o `.cfg` de uma textura vai junto), scripts, subobjetos e, com `sounds`, os arquivos `.wav`.
- **Distância**: um tile conta como "ao redor" de um ponto quando está a até `tile_radius` tiles nos dois eixos (um quadrado, não um círculo); eles são mantidos do mais próximo ao mais distante.

<a id="phase-2-while-omsi-runs"></a>
## Fase 2: enquanto o OMSI roda

Quando o controlador informa o id de processo do OMSI, o programa inicia estas atividades. Elas rodam em threads próprias, abaixo da prioridade do trabalho do próprio jogo onde isso importa.

| Atividade | Prioridade | O que faz | Precisa de |
| --- | --- | --- | --- |
| Levantamento (survey) | abaixo do normal | Calcula o índice de tiles de todo tile ainda não calculado, do mais próximo do início para o mais distante, e o guarda para salvar. | `survey` e um mapa legível |
| Previsão | normal | Estima onde o jogador está e mantém os tiles ao redor desse ponto ([abaixo](#tile-prediction)). | `survey` e um mapa legível |
| Veículos do usuário | abaixo do normal | Mantém os veículos do usuário escolhidos por `user_vehicles` ([escopos de veículos](vehicle-scopes.md#the-users-vehicles)). | `user_vehicles` diferente de `none` |
| Pré-abertura | abaixo do normal | Abre arquivos de veículos antes do jogo, sem mantê-los ([pré-abertura](pre-open.md)). | `pre_open` diferente de `none` |
| Guarda | normal | Devolve memória quando o sistema fica com pouca ([memória e orçamento](memory-and-budget.md#the-memory-guard)). | sempre |
| Feed | n/d | Atende o pipe da sessão: recebe os relatórios de uso e os aplica. | componente nativo conectado |
| Pré-abertura no primeiro uso | abaixo do normal | Uma thread curta por pasta de veículo, iniciada pelo primeiro uso do seu `.bus` ou `.ovh` ([pré-abertura](pre-open.md#on-first-use-pre_open_on_first_use)). | `pre_open_on_first_use` |

Cada linha `U` marca um arquivo como usado pela primeira vez e alimenta a lista aprendida. Uma linha `T` (uma abertura de arquivo de tile) também alimenta a previsão. Uma linha `W` marca um arquivo como gravado: ele é liberado de imediato, se mantido, e nunca mais é mantido. Uma linha `X` faz o mesmo e é respondida imediatamente, na thread que lê o pipe, para que uma rajada de outras linhas nunca possa atrasá-la (o OMSI fica esperando enquanto um `X` está pendente, por até 1,5 s).

<a id="tile-prediction"></a>
### Previsão de tiles

O OMSI carrega uma janela de tiles ao redor do jogador, e novos tiles aparecem na borda dianteira dela. O programa pega os arquivos de tile que o OMSI abriu no último minuto (e pelo menos os 20 mais recentes, se menos de 20 foram abertos nesse minuto), tira a média das coordenadas deles e toma esse centroide como a posição estimada. Em seguida mantém os tiles ao redor do centroide dentro de `tile_radius`, do mais próximo ao mais distante. Se o jogador se mover enquanto ele trabalha, a próxima passagem começa de imediato; se a memória ou o orçamento a bloquear, ele tenta de novo a cada cinco segundos; caso contrário, espera movimento. A posição estimada é exibida na janela do monitor.

A previsão nunca libera tiles que já mantém só porque o jogador se afastou; a memória volta apenas pela guarda, pela liberação de um arquivo que o jogo grava e pelo fim da sessão ([limitações conhecidas](../reference/known-limitations.md)).

<a id="the-feed"></a>
## O feed

O pipe aceita um cliente por vez e recusa um cliente que não seja o processo do OMSI (o controlador nomeia o processo; uma conexão que chega antes do nome espera até dez segundos por ele). As linhas têm no máximo 4096 bytes, um tab separa o tipo do caminho, e apenas os tipos `U`, `T`, `W` e `X` são aceitos; caminhos com caracteres de controle ou fora da instalação são ignorados. A fila de trabalho comporta 4096 linhas; quando está cheia, uma linha `U` (uma dica) é descartada e contada, enquanto `T` e `W` esperam até 50 ms. Depois que o OMSI se desconecta, o pipe é criado de novo.

<a id="saving"></a>
## Salvando

A cada dois minutos durante a sessão, e mais uma vez no fim, o programa grava a lista aprendida e a lista de escrita (com `learn`) e o índice de tiles (com `survey`, quando cresceu desde o último salvamento) na pasta de dados do mapa, por meio de um arquivo temporário que substitui o antigo ([dados aprendidos](learned-data.md)).

<a id="stopping"></a>
## Parando

Quando o controlador escreve `stop` (ou a entrada padrão termina), o programa cancela as suas atividades, espera até seis segundos por elas, salva, emite o evento `session`, registra a linha `end:`, fecha o pipe e libera todo arquivo mantido; depois encerra. O controlador concede `host_stop_ms` (20.000 ms neste módulo) para isso e encerra o programa em seguida. Se o próprio OmsiLaunch for morto, o job object encerra o programa; ele não chega a salvar, e o que resta é o último salvamento periódico (com no máximo dois minutos).

<a id="windows"></a>
## Janelas

O programa mostra no máximo duas janelas, ambas na sua própria thread: a [janela de primeira abertura](../getting-started/first-run.md#the-first-open-window) e a janela do monitor. O monitor só abre quando as configurações do OmsiLaunch pedem (`monitor`; flag `/prefetch-monitor`). Ele mostra quanto está mantido em relação ao orçamento, a fase, a memória do sistema, os bytes mantidos por origem (aprendida, mapa, tiles, IA, usuário), a posição estimada, o número de tiles indexados, se o OMSI está conectado e uma lista de arquivos com um filtro (todos, em uso, na memória mas sem uso, usados fora da memória) e uma caixa "Always on top". Linhas verdes são arquivos em uso, linhas laranja foram usadas fora da memória, linhas cinza estão mantidas e sem uso. Fechá-la apenas a oculta até o fim da sessão. Ambas as janelas usam português quando o idioma de exibição do Windows é o português e inglês nos demais casos.

<a id="limits-that-bound-the-program"></a>
## Limites que restringem o programa

| Limite | Valor |
| --- | --- |
| Carregamento antes do OMSI | 5 minutos |
| Espera do controlador por `ready` | `prepare_ms`, 600.000 ms |
| Espera do controlador pelo stop | `host_stop_ms`, 20.000 ms |
| Arquivos em um fechamento (um veículo, um objeto de cenário) | 60.000 |
| Entradas na lista aprendida | 200.000 |
| Entradas listadas em uma pasta de mapa (mapa inteiro) | 50.000 |
| Linha de contexto | 1 MiB; as linhas seguintes, no máximo 64 KiB |

<a id="next"></a>
## Próximos passos

[Shim nativo](native-shim.md) · [Memória e orçamento](memory-and-budget.md) · [Escopos de veículos](vehicle-scopes.md)
