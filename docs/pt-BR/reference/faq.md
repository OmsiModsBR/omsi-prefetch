# FAQ

<!-- l10n: source=reference/faq.md source-sha256=3659e05bb6c6526ff03de63ba3364459b4fb527041e961ec80d19f6dc992afac -->
> Tradução da [página original em inglês](../../reference/faq.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Respostas curtas às perguntas que os usuários mais fazem sobre o Omsi Prefetch. Cada resposta aponta para a página que tem o detalhe.

Páginas relacionadas: [visão geral](../getting-started/overview.md), [limitações conhecidas](known-limitations.md), [diagnosticando lentidão](../guides/diagnosing-slowness.md), [configurações](settings.md).

<a id="does-it-change-my-omsi-files"></a>
## Ele altera os meus arquivos do OMSI?

Não. Ele abre arquivos para leitura e grava apenas abaixo de `.omsilaunch\module-data\omsi-prefetch\`. A única coisa que faz com as gravações do próprio OMSI é agrupar em blocos as gravações byte a byte do cache de terreno (`.rdy`); os bytes são do OMSI, gravados depois e juntos ([segurança](../concepts/safety.md)).

<a id="does-it-raise-my-frame-rate"></a>
## Ele aumenta a minha taxa de quadros?

Não. Ele encurta o carregamento e os congelamentos causados pelo carregamento. O tempo mediano de quadro e o trabalho de CPU do jogo não mudam ([visão geral](../getting-started/overview.md#what-you-get)).

<a id="why-does-omsi-open-later-than-before"></a>
## Por que o OMSI abre mais tarde que antes?

O OmsiLaunch só inicia o OMSI depois que o módulo informou que a preparação terminou. Essa espera é o que permite ao OMSI encontrar os seus arquivos na memória. Ela é mais longa na primeira abertura de um mapa e mais curta depois ([primeira execução](../getting-started/first-run.md)).

<a id="what-is-the-small-window-that-appears-before-omsi"></a>
## Que janelinha é essa que aparece antes do OMSI?

A janela de primeira abertura. Ela aparece na primeira vez que um mapa é solicitado (ou depois de os dados dele serem limpos) e diz que o módulo está lendo o mapa e aprendendo. Fecha sozinha. Desligue-a com `first_open_notice=false` ([primeira execução](../getting-started/first-run.md#the-first-open-window)).

<a id="why-is-the-first-session-not-faster"></a>
## Por que a primeira sessão não é mais rápida?

O módulo aprende o que o jogo usa enquanto você joga. A primeira sessão de um mapa paga por isso; as seguintes partem do que você realmente usa ([dados aprendidos](../concepts/learned-data.md)).

<a id="do-i-need-to-configure-anything"></a>
## Preciso configurar alguma coisa?

Não. Os padrões funcionam em máquinas com memória livre suficiente. Em uma máquina pequena, ou quando quiser ajustar, veja [escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md).

<a id="how-much-ram-does-it-use"></a>
## Quanta RAM ele usa?

Por padrão, 70 % da memória livre no início depois de reservar 4 GB, no máximo 12 GB. Em uma máquina com 4 GB ou menos livres, ele não mantém nada. Você pode definir uma quantidade explícita com `ram_budget_mb` ([memória e orçamento](../concepts/memory-and-budget.md)).

<a id="can-i-use-it-with-8-gb-of-ram"></a>
## Posso usá-lo com 8 GB de RAM?

Sim, com um orçamento explícito (por exemplo 1.536 MB) e uma pegada pequena. O orçamento automático costuma ser zero com 8 GB, porque há menos de 4 GB livres ([perfis por tamanho de memória](../guides/choosing-settings-by-memory.md#8-gb)).

<a id="why-does-task-manager-show-a-large-prefetchpreloaderexe"></a>
## Por que o Gerenciador de Tarefas mostra um `Prefetch.Preloader.exe` grande?

Essa é a memória mantida: os arquivos estão mapeados nesse processo. Ela é liberada quando a sessão termina, e o Windows pode tomá-la de volta se o sistema precisar ([memória e orçamento](../concepts/memory-and-budget.md)).

<a id="does-it-send-anything-over-the-internet"></a>
## Ele envia alguma coisa pela Internet?

Não. O módulo não tem código de rede e não envia nada para fora da máquina.

<a id="what-happens-if-omsi-crashes-or-i-end-the-session-from-the-tray"></a>
## O que acontece se o OMSI falhar ou eu encerrar a sessão pela bandeja?

Nada acontece com os seus arquivos. O programa de sessão salva o que aprendeu e encerra. A única perda é a parte do cache de terreno (`.rdy`) que ainda estava em um buffer de write-behind; o OMSI a reconstrói ([segurança](../concepts/safety.md#what-happens-when-something-goes-wrong)). Para evitar até isso, saia do OMSI pela saída dele mesmo.

<a id="can-i-turn-it-off"></a>
## Posso desligá-lo?

Para uma sessão: `/no-module:omsi-prefetch`. Permanentemente: `enabled=false` em `.omsilaunch\modules.ini`, ou remova a pasta do módulo ([desligando](../guides/turning-off.md)).

<a id="can-i-set-it-per-map-or-per-way-of-playing"></a>
## Posso configurá-lo por mapa ou por forma de jogar?

Sim, com um preset de perfil de sessão do OmsiLaunch que carregue `modules.omsi-prefetch.settings` ([perfis de sessão](../guides/session-profiles.md)).

<a id="where-are-its-settings"></a>
## Onde ficam as configurações dele?

Não há um arquivo próprio do módulo. As configurações passam pelo OmsiLaunch: as flags dele (`/prefetch-ram:6144`), `/module-setting:omsi-prefetch.<key>=<value>`, `.omsilaunch\modules.ini` ou um preset ([configurações](settings.md)).

<a id="it-says-the-module-is-not-loaded-why"></a>
## Diz que o módulo não está carregado. Por quê?

Leia o plano: `OmsiLaunch.exe ... /plan --json`. Os motivos usuais são uma pasta de módulo alterada (`OL_E_MODULE_INTEGRITY`), o plugin `OmsiLoadingPoC.dll` estar ligado (`OL_E_MODULE_CONFLICT`) ou o módulo estar desligado ([erros e degradação](errors-and-degradation.md)).

<a id="does-it-work-without-omsilaunch"></a>
## Funciona sem o OmsiLaunch?

Não. É um Runtime Module do OmsiLaunch: o OmsiLaunch inicia o programa de sessão dele antes do OMSI e a bridge dele carrega os componentes dentro do jogo.

<a id="how-do-i-see-what-it-holds"></a>
## Como vejo o que ele mantém?

Inicie a sessão com `/prefetch-monitor` para uma janela ao vivo, ou leia o log ([formato do log](log-format.md)).

<a id="how-do-i-reset-what-it-learned"></a>
## Como reinicio o que ele aprendeu?

`OmsiLaunch.exe /clear-module-data:omsi-prefetch` enquanto nenhuma sessão estiver rodando ([limpando dados](../guides/clearing-data.md)).

<a id="why-is-a-window-in-portuguese"></a>
## Por que uma janela está em português?

As duas janelas do módulo usam português quando o idioma de exibição do Windows é o português e inglês nos demais casos.

<a id="which-license-is-it-under"></a>
## Sob que licença ele está?

GNU General Public License, versão 3. Ele usa o OmsiLaunch, que está sob a GNU Lesser General Public License, somente versão 3. Veja `NOTICE.txt` na pasta do módulo.

<a id="next"></a>
## Próximos passos

[Limitações conhecidas](known-limitations.md) · [Diagnosticando lentidão](../guides/diagnosing-slowness.md) · [Visão geral](../getting-started/overview.md)
