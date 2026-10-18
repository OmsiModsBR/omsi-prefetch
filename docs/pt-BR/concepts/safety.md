# Segurança: o que o módulo toca e o que acontece quando algo dá errado

<!-- l10n: source=concepts/safety.md source-sha256=0107866f328b0128542aa0966f8bb405eba8819d5a9ea3fec22f1fcc89750a3a -->
> Tradução da [página original em inglês](../../concepts/safety.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

O Omsi Prefetch roda ao lado do seu simulador e lê muitos arquivos dele, por isso esta página afirma com clareza o que ele lê, o que grava, o que nunca mantém, como evita atrapalhar o OMSI e qual é o resultado quando o OMSI, o OmsiLaunch ou o próprio módulo param inesperadamente. Tudo foi extraído do código. O módulo não é um produto de segurança e o OmsiLaunch não dá aval a módulos; a fronteira que o OmsiLaunch garante para qualquer módulo está na referência de Runtime Modules ("Security boundary").

Páginas relacionadas: [como o prefetch funciona](how-it-works.md), [shim nativo](native-shim.md), [memória e orçamento](memory-and-budget.md), [arquivos e diretórios](../reference/files-and-directories.md), [erros e degradação](../reference/errors-and-degradation.md), [limitações conhecidas](../reference/known-limitations.md).

<a id="what-it-reads"></a>
## O que ele lê

O programa de sessão abre arquivos dentro da pasta do OMSI **somente para leitura**, para mantê-los ou para descobrir o que um mapa ou um veículo precisa: o `global.cfg` do mapa, os seus arquivos de tile, as suas listas (`ailists.cfg`, `parklist_p.txt`, `humans.txt`, `drivers.txt`, `unsched_vehgroups.txt`), os objetos de cenário, splines, modelos, texturas, scripts e sons que eles citam, e os arquivos dos veículos. Caminhos que saem da pasta do OMSI nunca são abertos. Ele não lê os seus documentos, o registro, a rede nem arquivos de qualquer outro programa. O módulo não tem código de rede.

Os limites de tamanho que restringem uma leitura estão em [arquivos e diretórios](../reference/files-and-directories.md#what-it-reads).

<a id="what-it-writes"></a>
## O que ele grava

| Onde | O quê | Quem |
| --- | --- | --- |
| `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\maps\<folder>-<hash>\` | `learned.txt`, `written.txt`, `tiles.idx` (e um `.tmp` transitório durante um salvamento) | O programa de sessão |
| `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\logs\` | `preloader-<date>-<time>.log` | O programa de sessão |
| Os arquivos do próprio OMSI | Os bytes que **o próprio OMSI** grava, exatamente como ele os grava: com write-behind, a gravação de um cache de terreno (`.rdy`) pode ser adiada e agrupada, nunca alterada | O componente nativo, em nome do OMSI |

Nada mais. O módulo nunca cria, edita, renomeia nem apaga um arquivo da sua instalação do OMSI, dos seus veículos, mapas ou add-ons. Ele não grava `options.cfg`, os bitmaps de splash nem qualquer outro arquivo de que a transação de sessão do OmsiLaunch seja dona, e nunca os mantém. O diretório de dados dele fica fora da transação de sessão por projeto, e por isso permanece depois da sessão. O diretório de dados é limitado ([dados aprendidos](learned-data.md#bounds-and-clean-up)).

<a id="what-is-never-held"></a>
## O que nunca é mantido

Um arquivo nunca é mantido (mapeado) quando qualquer uma destas condições é verdadeira:

- a extensão dele é uma de `.osn .rdy .tbmp .odr .tmp .log .sav .bak .owt .prt .lm.bmp` (arquivos que o jogo reescreve ou lê em streaming: ele trunca `laststn.osn` quando fecha o mapa e lê em streaming `tile_X_Y.map.prt` e `.LM.bmp` enquanto carrega tiles; um arquivo mapeado não pode ser truncado, o jogo travaria ou a thread do carregador de tiles morreria e metade do mapa não carregaria);
- o jogo o gravou em uma sessão anterior neste mapa (`written.txt`) ou nesta sessão;
- é um dos arquivos de que a transação de sessão do OmsiLaunch é dona (os `session_files` do contexto, entre eles `options.cfg` quando sobreposto e os bitmaps de splash);
- está vazio, não pode ser aberto ou não caberia no orçamento.

Os arquivos do mapa inteiro, os arquivos de tile e os escopos de veículos também deixam de fora arquivos de trabalho e arquivos compactados (`.psd .rar .zip .7z .blend .max .3ds .pdf .doc .xls .db .exe .dll .fbx .obj .svg` e semelhantes).

<a id="not-getting-in-omsis-way"></a>
## Não atrapalhar o OMSI

| Mecanismo | O que garante |
| --- | --- |
| Os handles são fechados de imediato | O preloader mapeia um arquivo e fecha o seu handle. As aberturas do próprio OMSI do mesmo arquivo nunca encontram um conflito de compartilhamento. |
| Compartilhamento total | Toda abertura feita pelo módulo permite compartilhamento de leitura, escrita e exclusão. |
| Liberação a pedido do OMSI | Se o OMSI tenta substituir ou truncar um arquivo mantido (o Windows responde `ERROR_USER_MAPPED_FILE`), o componente nativo pede ao programa de sessão que o libere (uma linha `X`) e tenta de novo por até 1,5 s. O programa responde na thread que lê o pipe, à frente de qualquer outra linha, de modo que uma rajada de relatórios não pode atrasá-lo. O arquivo é liberado e nunca mais mantido. Se nenhum programa de sessão estiver conectado, a chamada falha como falharia sem o módulo. |
| Semântica exata | Read-ahead, write-behind e abertura somente leitura mantêm leituras, posições, tamanhos e o comportamento de fim de arquivo exatamente como uma chamada sem hook os retorna ([shim nativo](native-shim.md)). |
| Os recursos exigem todos os hooks | Se um hook de posição não está instalado, nenhum dos três recursos começa. |
| A abertura somente leitura se reverte | Se o OMSI grava em um handle aberto somente leitura, o arquivo é reaberto com o acesso que o OMSI pediu e o handle passa a ser atendido por ele. |
| Baixa prioridade | O levantamento, a manutenção dos veículos do usuário e a pré-abertura rodam com prioridade abaixo do normal, e a pré-abertura com prioridade de E/S em segundo plano. |
| Pisos de memória e guarda | O módulo para de carregar abaixo de um piso de memória livre e devolve arquivos que o OMSI não usou quando a memória fica baixa ([memória e orçamento](memory-and-budget.md)). |

<a id="process-and-channel-boundary"></a>
## Fronteira de processo e de canal

- O programa de sessão é um processo separado iniciado pelo OmsiLaunch dentro de um job object: termina com a sessão, não pode iniciar outros programas e não pode alterar configurações do sistema nem de vídeo.
- O pipe dele é restrito ao usuário atual e aceita um cliente, o processo que o OmsiLaunch nomeia como o OMSI; qualquer outro cliente é recusado e contado.
- As linhas no pipe são limitadas (4096 bytes), aceitam apenas quatro tipos, devem ser UTF-8 válido sem caracteres de controle e nomeiam caminhos que são resolvidos dentro da pasta do OMSI; um caminho fora dela é ignorado. Um caminho é dado: só é usado para procurar um arquivo a manter, nunca é executado nem seguido.
- Os componentes gerenciado e nativo rodam no OMSI com os direitos do OMSI. Eles não mantêm arquivos da instalação.
- Nada sai da máquina.

<a id="what-happens-when-something-goes-wrong"></a>
## O que acontece quando algo dá errado

| Evento | Efeito no OMSI | Efeito nos seus arquivos | O que o módulo conserva |
| --- | --- | --- | --- |
| O OMSI encerra normalmente | Nenhum | Nenhum. O shim descarrega todo buffer antes de os hooks saírem; o programa de sessão salva e encerra. | Os dados aprendidos são salvos no fim. |
| O OMSI falha ou é morto | O OMSI some | Os bytes de `.rdy` ainda em buffer no write-behind (até 256 KiB por `.rdy` aberto) são perdidos; `.rdy` é um cache regenerável, que o OMSI refaz. Nenhum outro arquivo é afetado. | O programa de sessão é parado pelo OmsiLaunch depois que o OMSI encerra; ele salva o que aprendeu. |
| A sessão é encerrada pela bandeja ou por `session stop` | O OMSI é terminado pelo OmsiLaunch | O mesmo que um OMSI morto. Um session stop termina o OMSI, então a limpeza do módulo dentro do OMSI não roda. | O programa de sessão é mandado parar e salva. |
| O OmsiLaunch é morto | O OMSI continua rodando | Nenhum | O job object encerra o programa de sessão. O que resta é o último salvamento periódico (com no máximo dois minutos); a memória mantida é liberada junto com o processo. |
| O programa de sessão falha ou é morto | O OMSI continua rodando | Nenhum | A memória mantida é liberada. O componente nativo deixa de informar (desiste após dez segundos sem um pipe); read-ahead, write-behind e abertura somente leitura continuam funcionando. Nada é aprendido a partir daí; o que foi salvo antes permanece. |
| A máquina perde energia ou o Windows falha | | A ressalva do `.rdy` acima. Um salvamento em andamento deixa o arquivo de dados antigo intacto (os dados novos são gravados em um `.tmp` e depois o substituem). | O último salvamento concluído. |
| O diretório de dados está cheio ou não é gravável | Nenhum | Nenhum | Os salvamentos falham e são registrados (`save: ...`); a sessão continua. |
| Um hook nativo é recusado | O OMSI roda como sem esse hook | Nenhum | Conforme [erros e degradação](../reference/errors-and-degradation.md). |

Como o módulo nunca modifica um arquivo por conta própria, a recuperação de uma sessão do OmsiLaunch após uma falha (`/recovery-status`) nunca o envolve. Depois de uma falha, não há nada para o módulo restaurar.

<a id="accepted-risks"></a>
## Riscos aceitos

Estes são os riscos que o módulo aceita, cada um descrito por completo em [limitações conhecidas](../reference/known-limitations.md):

- Terminar o OMSI enquanto ele grava um `.rdy` perde os bytes ainda em buffer; o arquivo é um cache regenerável.
- Uma gravação em um asset de veículo que ainda está aberto no exato momento em que o módulo para pode ser recusada, porque o handle somente leitura do próprio jogo volta a ser usado.
- Um arquivo mantido em memória não pode ser truncado nem substituído por outro programa enquanto está mantido; o módulo libera um arquivo quando o OMSI pede, não quando um editor pede.

<a id="next"></a>
## Próximos passos

[Arquivos e diretórios](../reference/files-and-directories.md) · [Erros e degradação](../reference/errors-and-degradation.md) · [Limitações conhecidas](../reference/known-limitations.md)
