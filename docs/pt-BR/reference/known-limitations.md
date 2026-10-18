# Limitações conhecidas

<!-- l10n: source=reference/known-limitations.md source-sha256=da048af1a131f516200fc2aa39c25929f3c8509b465025aa2820bb48f1c1fd37 -->
> Tradução da [página original em inglês](../../reference/known-limitations.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Esta página lista, a partir do código, o que o Omsi Prefetch `1.0.0-beta.1` não faz, faz apenas em parte ou faz com um risco aceito, para que você não crie expectativas sobre um comportamento que ele não oferece. Cada linha diz qual é a limitação, por que ela existe e o que você pode fazer a respeito. Onde o README do repositório e o código foram comparados, vale o código; as diferenças encontradas estão listadas no fim.

Páginas relacionadas: [segurança](../concepts/safety.md), [erros e degradação](errors-and-degradation.md), [compatibilidade](compatibility.md), [FAQ](faq.md), [cobertura de testes](../status/test-coverage.md).

<a id="measurement-and-scope"></a>
## Medição e escopo

| Limitação | Detalhe |
| --- | --- |
| Os resultados publicados são de uma máquina e um mapa (n = 1) | A tabela da [visão geral](../getting-started/overview.md#what-you-get) vem do estudo de otimização de carregamento de 30/09/2026 no mapa RMG Leste. Outro hardware e outros mapas vão diferir ([medindo o ganho](../getting-started/measuring-the-gain.md)). |
| A taxa de quadros não muda | O tempo mediano de quadro (cerca de 52 ms no estudo) e as partes de pura CPU do jogo ("Refresh Schedules") não são tocados. |
| Funciona somente por meio do OmsiLaunch | O programa de sessão é iniciado pelo OmsiLaunch antes do OMSI, e os hooks são instalados pela bridge dele. |
| Os dados são por mapa | Nada aprendido em um mapa ajuda outro, mesmo que compartilhem conteúdo. |

<a id="memory"></a>
## Memória

| Limitação | Detalhe |
| --- | --- |
| No máximo 12 GB são mantidos | O orçamento é limitado a 12.288 MB, qualquer que seja a RAM. |
| O orçamento é fixado no início | O orçamento automático lê a memória livre uma vez. Mudanças posteriores na memória livre são tratadas apenas pelos pisos e pela guarda, que liberam arquivos que o OMSI não usou; o orçamento em si não é recalculado. |
| Sem descarte de tiles atrás do jogador | Quando o orçamento está cheio, a previsão não mantém nenhum tile novo até a memória voltar (a guarda, um arquivo que o jogo grava, o fim da sessão). Tiles que o jogador deixou para trás permanecem mantidos. Um `tile_radius` menor ou um orçamento que caiba na área em que você dirige evita isso. |
| Os arquivos mantidos são memória apoiada em arquivo de outro processo | O Gerenciador de Tarefas a mostra como o working set de `Prefetch.Preloader.exe`. Ela conta contra a memória livre que os outros programas veem. |
| O pedido de working set é suave | Se o Windows o recusar (o log informa) ou o sistema estiver sob pressão, as páginas mantidas podem ser reduzidas. |
| A pré-abertura é uma dica | Abrir um arquivo antes aquece o cache de arquivos do sistema, que o Windows pode descartar antes de o OMSI pedi-lo. |

<a id="learning-and-data"></a>
## Aprendizado e dados

| Limitação | Detalhe |
| --- | --- |
| A lista aprendida só cresce | Um arquivo usado uma vez permanece em `learned.txt` até você limpar os dados; muitas rotas geram uma lista grande, e os arquivos usados primeiro são mantidos primeiro. Com um orçamento pequeno, os posteriores não são mantidos antes de o OMSI iniciar. |
| O índice de tiles percebe apenas o `global.cfg` e os arquivos de tile | A impressão digital dele cobre nomes, tamanhos e datas de gravação. Uma textura, um objeto ou uma spline que mudou sem tocar nesses arquivos não é percebido até o índice ser limpo. |
| A primeira sessão de um mapa não é mais rápida | O módulo não tem nada aprendido; ele mantém o que consegue derivar do mapa. Também atrasa o início do OMSI pelo tempo da preparação. |
| Os tiles iniciais de um mapa novo usam as duas ordens de pontos de entrada | A ordem em que o OMSI apresenta os pontos de entrada não é conhecida offline, então o tile do ponto de entrada da ordem do arquivo e o da ordem dos nomes são ambos tratados como tiles iniciais. Um dos dois é extra. |
| O aprendizado precisa de um mapa legível | Sem mapa na sessão, ou com um mapa que não pode ser lido, nada é aprendido. |
| Os dados não são compartilhados entre instalações | Tudo fica abaixo de `.omsilaunch\module-data\` da instalação. |
| Os veículos do usuário são mantidos tarde | Eles são mantidos enquanto o OMSI roda, depois de os tiles e os veículos de IA terem sido atendidos; um veículo que você escolhe nos primeiros momentos pode ainda não estar mantido ([escopos de veículos](../concepts/vehicle-scopes.md)). |

<a id="safety-and-accepted-risks"></a>
## Segurança e riscos aceitos

| Limitação | Detalhe |
| --- | --- |
| Terminar o OMSI à força perde bytes de `.rdy` em buffer | Com write-behind, até 256 KiB por handle aberto do cache de terreno `.rdy` estão apenas na memória até um descarregamento. Uma saída normal descarrega; terminar o OMSI (`session stop`, a bandeja, uma falha) perde o restante. O arquivo é um cache regenerável. |
| Um descarregamento de write-behind que falha é silencioso para o OMSI | O OMSI já foi informado de que os bytes foram gravados. A falha é contada (`write_behind_failed`) e o write-behind é desligado para novos handles. |
| Um arquivo mantido não pode ser truncado nem substituído por outro programa | O módulo libera um arquivo quando o OMSI pede (em até 1,5 s). Ele não libera quando outro programa, como um editor, grava o arquivo no mesmo lugar; esse programa recebe `ERROR_USER_MAPPED_FILE`. Edite os seus add-ons antes de uma sessão, não durante. |
| Um handle reaberto para escrita é devolvido ao handle somente leitura do jogo na parada | Se o OMSI grava em um asset de veículo que foi aberto somente leitura e ainda está aberto no momento em que o módulo para, uma gravação posterior pelo handle do próprio jogo seria recusada. |
| Os hooks são hooks de importação do `Omsi.exe` | Um hook é recusado quando alguém fora da bridge alterou a importação, e é removido quando o módulo para. Uma thread que ainda retorna por um desvio permanece segura porque a DLL fica carregada até o OMSI encerrar. |
| Uma sessão encerrada terminando o OMSI à força pula a limpeza do módulo dentro do OMSI | A limpeza do módulo dentro do OMSI acontece em uma saída normal. O programa de sessão sempre recebe o seu stop. |

<a id="behavior-at-the-edges"></a>
## Comportamento nos extremos

| Limitação | Detalhe |
| --- | --- |
| A fila de relatórios é limitada | 400.000 linhas na fila e 500.000 caminhos distintos lembrados; além disso, as linhas são descartadas e contadas e os primeiros usos posteriores não são informados. Handles além de 4096 ao mesmo tempo são atendidos nativamente. |
| O feed desiste após dez segundos sem um programa de sessão | Os recursos nativos continuam funcionando; sem aprendizado a partir daí. |
| Os hooks D3DX dependem de o `d3dx9.dll` estar carregado no início | Se não estiver, texturas e malhas carregadas pelo próprio Direct3D não são informadas como usos (`-1` no evento `hooks`). |
| Referências que o resolvedor não encontra são puladas | O módulo segue referências como as ferramentas do estudo faziam (primeiro campo de tab de uma linha, strings entre aspas, uma pasta `model\`, pastas `texture\` acima na árvore, outra extensão de imagem). Um arquivo nomeado de uma forma que nenhuma dessas encontra não é mantido até a lista aprendida tê-lo. |
| Pastas que são links não são listadas | Junctions e links simbólicos não são percorridos quando o módulo lista uma pasta de mapa ou de veículo. |
| Apenas dois idiomas de exibição | As janelas do módulo são em português ou inglês. |
| Uma janela pode aparecer antes do OMSI | A janela de primeira abertura fica sempre por cima; ela nunca toma o foco. |

<a id="evidence"></a>
## Evidências

Os gates offline do módulo e o estudo que mediu os mecanismos estão descritos em [cobertura de testes](../status/test-coverage.md). Os gates offline exercitam o programa de sessão, o componente nativo com uma bridge falsa e o pacote com o próprio parser de manifesto do OmsiLaunch; o ganho medido é o do estudo. O README declara os resultados do estudo como válidos para o módulo.

<a id="differences-found-between-the-readme-the-manifest-and-the-code"></a>
## Diferenças encontradas entre o README, o manifesto e o código

Quando o código e o README, uma descrição do manifesto ou um comentário discordaram, estas páginas seguem o código. As diferenças encontradas ao escrevê-las:

- O README dá o título da janela de primeira abertura como "Pré-carregamento sendo inicializado…" como se fosse fixo. O código o mostra em português **somente** quando o idioma de exibição do Windows é o português; em qualquer outro idioma, é mostrado o texto em inglês ("Preloading is being set up").
- O README diz que a previsão de tiles usa "the centroid of the tile files OMSI opened in the last minute". O código usa os arquivos de tile abertos nos últimos 60 segundos e, se menos de 20 foram abertos, os 20 mais recentes.
- A descrição do manifesto para `read_only_folders` diz que os assets abaixo das pastas são abertos somente leitura. O código também exige uma de 18 extensões de asset ([shim nativo](../concepts/native-shim.md#read-only-open)).
- A descrição do manifesto para `sounds` diz que inclui "the sound files" que veículos e objetos citam. O código segue apenas citações `.wav` (e, no escopo `complete`, deixa os arquivos `.wav` fora da pasta quando está desligado).
- A descrição do manifesto para `survey` diz que lê os tiles do mapa para saber do que cada um precisa. Com `survey` desligado, o código também interrompe a previsão de tiles durante a sessão e a manutenção dos tiles iniciais antes do OMSI.
- A descrição do manifesto para `learn` diz que lembra o que cada sessão usou. Com `learn` desligado, o código ainda lê e usa uma lista aprendida existente; apenas deixa de atualizá-la.
- Um comentário em `prefetch\preloader\Windows.cs` nomeia a flag do monitor como `/preload-monitor`; a flag é `/prefetch-monitor` (manifesto e README).

<a id="next"></a>
## Próximos passos

[FAQ](faq.md) · [Erros e degradação](errors-and-degradation.md) · [Cobertura de testes](../status/test-coverage.md)
