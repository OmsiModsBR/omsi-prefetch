# Como o prefetch funciona

<!-- l10n: source=concepts/how-it-works.md source-sha256=d46e091bbe97206a6817d6c0c9349571b046bc2f47c693777f43a05c5e54e72d -->
> Tradução da [página original em inglês](../../concepts/how-it-works.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Esta página descreve o Omsi Prefetch de ponta a ponta: os três componentes, a ordem dos acontecimentos do início ao fim de uma sessão, pelo que cada componente é responsável e como eles se comunicam. As outras páginas de conceitos detalham cada mecanismo: o [programa de sessão](session-program.md), o [shim nativo](native-shim.md), os [dados aprendidos](learned-data.md), [memória e orçamento](memory-and-budget.md), [escopos de veículos](vehicle-scopes.md), a [pré-abertura](pre-open.md) e a [segurança](safety.md). Tudo aqui é extraído do código-fonte do módulo em `prefetch\`.

Páginas relacionadas: [visão geral](../getting-started/overview.md), [primeira execução](../getting-started/first-run.md), [referência de configurações](../reference/settings.md), [arquivos e diretórios](../reference/files-and-directories.md).

<a id="two-problems-two-mechanisms"></a>
## Dois problemas, dois mecanismos

O custo de carregamento do OMSI se compõe de duas coisas, e o módulo tem um mecanismo para cada uma:

| Problema | Mecanismo | Componente |
| --- | --- | --- |
| Os arquivos não estão na memória quando o OMSI os pede, e a primeira leitura de um arquivo frio é lenta. | **Prefetch para a RAM**: ler os arquivos que a sessão vai usar antes de o OMSI pedi-los e mantê-los mapeados na memória. | `Prefetch.Preloader.exe` (fora do OMSI) |
| O OMSI pede os seus dados alguns bytes por vez, dezenas de milhões de vezes. | **Read-ahead e write-behind** (leitura antecipada em blocos e gravação adiada em blocos): responder a essas pequenas leituras a partir de blocos de 64 KiB e reunir em blocos de 256 KiB as pequenas gravações do cache de terreno. | `prefetch-native.dll` (dentro do OMSI) |

Um terceiro elemento amarra os dois: o componente nativo informa ao programa de sessão o que o OMSI usa, de modo que o programa aprende, por mapa, quais arquivos trazer da próxima vez e em que ponto do mapa o jogador está agora.

<a id="the-components-and-how-they-connect"></a>
## Os componentes e como eles se conectam

```text
OmsiLaunch (controller, x64)
  │  starts before OMSI, writes the session context on stdin
  ▼
Prefetch.Preloader.exe (x64, session program)            Omsi.exe (x86)
  holds files in RAM                                        ├─ OmsiLaunch plugin + bridge
  reads the map, the tiles, the vehicles                    ├─ prefetch-native.dll   (hooks on kernel32 / d3dx9 imports)
  creates the session pipe  ◄──── U / T / W / X lines ──────┤       read-ahead, write-behind, read-only open
  saves learned data                                        └─ Prefetch.Module.dll   (events: armed, gameplay-ready, stopping)
```

| Ligação | O que trafega |
| --- | --- |
| Controlador para o preloader, entrada padrão | O contexto da sessão (a primeira linha), depois `omsi-started` com o id de processo do OMSI e então `stop` (o fim da entrada também é um stop). |
| Preloader para o controlador, saída padrão | `ready`, eventos (`prepared`, `session`) e, se a preparação falhar, uma linha `error`. |
| Componente nativo para o preloader, o pipe da sessão | Relatórios de uso, cada um em uma linha UTF-8: `U` primeiro uso de um arquivo, `T` uma abertura de um `tile_X_Y.map`, `W` o jogo gravou em um arquivo, `X` o OMSI não consegue substituir um arquivo que está mantido (libere-o agora). |
| Componentes para o OmsiLaunch, eventos | Eventos de runtime `module.omsi-prefetch.*` ([eventos](../reference/events-and-notices.md)). |

O pipe é `\\.\pipe\OmsiLaunch.Module.<session id without dashes>.omsi-prefetch`. O preloader o cria, restrito ao usuário atual, **antes** de informar que está pronto, e aceita apenas o processo do próprio OMSI ([segurança](safety.md)).

<a id="a-session-in-order"></a>
## Uma sessão, em ordem

| Etapa | Onde | O que acontece |
| --- | --- | --- |
| 1 | OmsiLaunch | O plano inclui `omsi-prefetch` (ativação `default`) com as suas configurações efetivas. |
| 2 | Preloader | Inicia; lê o contexto; abre o seu log; calcula o orçamento de memória a partir da memória livre naquele momento ([memória e orçamento](memory-and-budget.md)). |
| 3 | Preloader | Cria o pipe da sessão. |
| 4 | Preloader | Lê o `global.cfg` do mapa: tiles, pontos de entrada, arquivos que o mapa cita. Abre a pasta de dados do mapa, remove os dados dos mapas usados há mais tempo se a pasta exceder `data_limit_mb`, carrega a lista aprendida, a lista de escrita e o índice de tiles. |
| 5 | Preloader | **Preparar**: mantém arquivos nesta ordem até o orçamento, cinco minutos ou pouca memória livre o interromper: (1) o que sessões anteriores do mapa usaram, na ordem do primeiro uso; (2) os arquivos do mapa inteiro; (3) os tiles mais próximos do início (raio 2, ou `tile_radius` se menor); (4) os veículos de IA do mapa; (5) mais tiles ao redor do início, até `tile_radius`. |
| 6 | Preloader | Emite `prepared` e escreve `ready`. |
| 7 | OmsiLaunch | Inicia o `Omsi.exe` e depois informa ao preloader o seu id de processo (`omsi-started`). |
| 8 | OMSI | A bridge carrega o `prefetch-native.dll`, que lê as suas configurações, solicita os hooks, inicia o feed para o pipe e emite `hooks`; em seguida o `Prefetch.Module.dll` é armado (`armed`). |
| 9 | Preloader | **Sessão**: cinco atividades em segundo plano começam ([programa de sessão](session-program.md)): o levantamento de tiles, a previsão de posição, os veículos do usuário, a pré-abertura e a guarda de memória; o feed nativo é atendido; os dados aprendidos são salvos a cada dois minutos. |
| 10 | OMSI | Carrega o mundo. As suas pequenas leituras são atendidas a partir de blocos, as suas gravações do cache de terreno são reunidas, e os arquivos de que ele precisa já estão na memória. |
| 11 | OMSI | Encerra. O componente nativo esvazia os seus buffers e emite `stats`; o componente gerenciado emite `stopping`. |
| 12 | OmsiLaunch, preloader | O OmsiLaunch escreve `stop` para o preloader. Ele interrompe as suas atividades, salva os dados aprendidos, emite `session`, libera todo arquivo mantido e encerra. O OmsiLaunch então restaura os seus próprios arquivos de sessão. |

<a id="what-is-held-and-why-in-a-separate-process"></a>
## O que é mantido, e por que em um processo separado

Os arquivos são mantidos pelo preloader, um processo separado de 64 bits, e não pelo OMSI. O OMSI é um processo de 32 bits com no máximo alguns gigabytes de espaço de endereçamento, de que ele precisa para o seu próprio mundo; um auxiliar de 64 bits consegue manter até 12 GB sem tirar nada disso. Cada arquivo é mapeado somente para leitura e cada página é tocada, de modo que as páginas realmente estão na memória e pertencem ao preloader. As páginas permanecem residentes enquanto o jogo lê os mesmos arquivos pelas suas próprias aberturas. O handle do arquivo é fechado de imediato, de modo que as aberturas do próprio OMSI nunca encontram um conflito de compartilhamento. Veja [memória e orçamento](memory-and-budget.md).

<a id="what-the-module-learns-and-keeps"></a>
## O que o módulo aprende e guarda

Para cada mapa, o módulo guarda uma lista curta dos arquivos que o jogo usou (na ordem do primeiro uso), uma lista dos arquivos que o jogo gravou e um índice dos arquivos de que cada tile precisa. A segunda sessão de um mapa parte do que você realmente usa, em vez de um palpite ([dados aprendidos](learned-data.md)). Tudo isso fica em `.omsilaunch\module-data\omsi-prefetch\`; nada é gravado nas pastas do próprio OMSI ([segurança](safety.md), [arquivos e diretórios](../reference/files-and-directories.md)).

<a id="degradation"></a>
## Degradação

O módulo é construído para falhar pequeno. Cada uma das suas partes pode falhar sem derrubar a sessão, e a política de falha do módulo é `degrade` ([erros e degradação](../reference/errors-and-degradation.md)):

- sem memória para manter qualquer coisa (o orçamento automático é zero): nada é mantido, mas o read-ahead e o write-behind nativos e o aprendizado continuam funcionando;
- sem mapa na sessão, ou com um mapa que não pode ser lido: nada é mantido e nada é aprendido (os dados são guardados por mapa), mas o read-ahead e o write-behind nativos continuam funcionando;
- o programa de sessão não pode ser alcançado a partir do OMSI por dez segundos: o componente nativo deixa de informar; o read-ahead e o write-behind continuam funcionando;
- um hook não pode ser instalado: os recursos que dependem dele ficam desligados e o OMSI roda como sem o módulo.

<a id="next"></a>
## Próximos passos

[Programa de sessão](session-program.md) · [Shim nativo](native-shim.md) · [Dados aprendidos](learned-data.md)
