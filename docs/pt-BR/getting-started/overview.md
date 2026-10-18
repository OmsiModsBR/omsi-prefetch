# O que é o Omsi Prefetch

<!-- l10n: source=getting-started/overview.md source-sha256=3eb8414a0f7207877936c1568d779821e3220396d0490ac1b0dda9970bda1456 -->
> Tradução da [página original em inglês](../../getting-started/overview.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

O Omsi Prefetch (id do módulo `omsi-prefetch`, versão `1.0.0-beta.1`) é um Runtime Module do OmsiLaunch que faz o OMSI 2 carregar mapas e veículos mais rápido. Ele faz isso de duas maneiras que trabalham juntas: antes de o OMSI ser iniciado, lê para a memória os arquivos de que a sessão vai precisar; e, enquanto o OMSI roda, elimina a maior parte do custo das minúsculas chamadas de arquivo que o jogo faz. Esta página diz o que o módulo faz por você, o que você ganha, o que ele não altera e de quais partes é feito. A instalação está em [instalação](installation.md); a primeira inicialização está em [primeira execução](first-run.md).

Páginas relacionadas: [instalação](installation.md), [primeira execução](first-run.md), [medindo o ganho](measuring-the-gain.md), [como o prefetch funciona](../concepts/how-it-works.md), [referência de configurações](../reference/settings.md), [limitações conhecidas](../reference/known-limitations.md), [FAQ](../reference/faq.md).

<a id="why-omsi-is-slow-to-load"></a>
## Por que o OMSI demora a carregar

O OMSI 2 lê o seu conteúdo em pedaços muito pequenos. Uma única sessão em um mapa grande emite dezenas de milhões de chamadas `ReadFile` de poucos bytes cada (de 39,8 a 48,8 milhões por sessão no estudo de onde este módulo vem). Cada chamada custa uma ida ao sistema operacional, de modo que o carregamento é limitado pelo número de chamadas, não pela velocidade do disco. Um disco mais rápido ajuda muito pouco; menos chamadas ajudam muito.

<a id="what-the-module-does-for-you"></a>
## O que o módulo faz por você

| O que acontece | Quando | Efeito |
| --- | --- | --- |
| O módulo lê o mapa e, até onde o orçamento de memória permite, traz os arquivos dele para a RAM: o que sessões anteriores deste mapa usaram, os arquivos do mapa inteiro, os tiles mais próximos do início, os veículos de IA do mapa e mais tiles ao redor do início. | Antes de o `Omsi.exe` ser iniciado. O OmsiLaunch só inicia o OMSI quando isso está pronto. | O OMSI encontra os seus arquivos já na memória. |
| O módulo responde às leituras byte a byte do OMSI a partir de blocos de 64 KiB, em vez de pedir ao sistema operacional uma vez por byte. | Enquanto o OMSI roda. | O tempo dentro de `ReadFile` cai duas ordens de grandeza. |
| O módulo reúne em blocos de 256 KiB as gravações byte a byte do cache de terreno (`.rdy`) feitas pelo OMSI. | Enquanto o OMSI roda. | O tempo dentro de `WriteFile` cai. |
| O módulo acompanha onde você está no mapa e traz os tiles ao seu redor antes de o OMSI pedi-los. | Enquanto o OMSI roda. | Menos congelamentos durante o carregamento de tiles. |
| O módulo lembra o que o OMSI usou em cada mapa. | No fim de cada sessão. | A próxima sessão do mesmo mapa parte do que você realmente usa. |

Você não executa nada. O módulo não tem menu nem janela, a menos que você peça uma. Uma vez instalado, ele participa de toda sessão que o OmsiLaunch inicia (ativação `default`), e você pode desligá-lo por sessão ou de forma permanente ([desligando](../guides/turning-off.md)).

<a id="what-you-get"></a>
## O que você ganha

A comparação abaixo é a que isola os mecanismos do módulo: o mesmo pacote otimizado do mapa RMG Leste, primeiro com o plugin apenas observando e depois agindo, com o cache de arquivos do sistema operacional esvaziado antes da execução com os mecanismos ligados (execuções R2-A e R3-A do estudo). O módulo reúne os mecanismos do estudo de otimização de carregamento de 30/09/2026: o manifesto do mapa aprendido em sessões anteriores, a antecipação de tiles, um cache de veículos, o read-ahead (leitura antecipada em blocos) das pequenas leituras do OMSI e o write-behind (gravação adiada, reunida em blocos) das gravações do cache de terreno. Esses resultados são tomados como válidos para o módulo.

| Indicador | Sem | Com | Mudança |
| --- | ---: | ---: | ---: |
| Até o tráfego estar carregado, depois de "Loading Situation" | 190 s | 51 s | 3,7x mais rápido |
| Até o jogo estar utilizável (primeiros 60 s sem um congelamento de 1 s ou mais) | 265 s | 71 s | 3,7x mais rápido |
| Tempo congelado (quadros de 1 s ou mais) | 75,0 s | 17,9 s | 4,2x menos |
| Maior congelamento | 35,4 s | 7,2 s | 4,9x menos |
| Etapa "Loading Tile", média por ciclo | 1538 ms | 382 ms | 4,0x mais rápido |
| Etapa "Updating Terrain", média por ciclo | 3744 ms | 549 ms | 6,8x mais rápido |
| Tempo dentro de `ReadFile`, todas as threads | 146,6 s | 0,8 s | 183x menos |
| Tempo dentro de `WriteFile`, todas as threads | 22,4 s | 3,8 s | 5,9x menos |

O ganho vem do custo de dezenas de milhões de chamadas de sistema minúsculas, não da velocidade do disco. O que **não** muda: o tempo mediano de quadro (cerca de 52 ms, de 19 a 20 quadros por segundo) e as partes que são pura CPU do jogo ("Refresh Schedules", cerca de 2,1 a 2,2 s por ciclo). O estudo é uma única máquina e um único mapa (n = 1); os resultados em outro hardware e outros mapas vão diferir. [Medindo o ganho](measuring-the-gain.md) mostra como comparar na sua própria máquina.

<a id="what-the-module-does-not-do"></a>
## O que o módulo não faz

- Ele não aumenta a taxa de quadros. O tempo de quadro é CPU do jogo e gráficos; o módulo atua no carregamento e nos congelamentos causados pelo carregamento.
- Ele não altera nenhum arquivo da sua instalação do OMSI. Abre arquivos para leitura e guarda os próprios dados abaixo de `.omsilaunch\module-data\omsi-prefetch\` ([segurança](../concepts/safety.md)).
- Ele não altera add-ons, licenças, direitos de uso nem a propriedade de qualquer conteúdo. Apenas lê arquivos dentro da sua instalação do OMSI.
- Ele não simula teclado, mouse nem janelas do OMSI e não precisa que o OMSI seja modificado: funciona por meio da bridge do OmsiLaunch, que faz hook nas importações do `Omsi.exe`.

<a id="the-three-parts"></a>
## As três partes

| Parte | Onde roda | O que faz |
| --- | --- | --- |
| `Prefetch.Preloader.exe` (C#, x64) | Um programa de sessão que o OmsiLaunch inicia **antes** do OMSI | Lê o mapa solicitado e mantém na RAM o que o orçamento permitir, informa que está pronto (só então o OmsiLaunch inicia o OMSI), acompanha a sessão e lembra o que o jogo usou. |
| `prefetch-native.dll` (C++, x86) | Dentro do OMSI, carregada pela bridge do OmsiLaunch | Read-ahead, write-behind e abertura somente leitura por meio de hooks nas funções de arquivo do `Omsi.exe`, e relatórios de uso para o programa de sessão. |
| `Prefetch.Module.dll` (C#) | Dentro do OMSI | A entrada gerenciada que a bridge exige; apenas informa marcos (`armed`, `gameplay-ready`, `stopping`). |

O módulo não faz parte do OmsiLaunch e não é distribuído com ele. É software livre sob a GNU General Public License, versão 3; usa o OmsiLaunch, que é licenciado sob a GNU Lesser General Public License, somente versão 3 (veja `NOTICE.txt` na pasta do módulo).

<a id="next"></a>
## Próximos passos

[Instalação](installation.md) · [Primeira execução](first-run.md) · [Medindo o ganho](measuring-the-gain.md)
