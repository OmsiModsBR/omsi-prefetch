# O shim nativo: read-ahead, write-behind e abertura somente leitura

<!-- l10n: source=concepts/native-shim.md source-sha256=402cf896a10bd7b925fefdaa34fdd679551906148072233badf9bee283f77e72 -->
> Tradução da [página original em inglês](../../concepts/native-shim.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

O `prefetch-native.dll` é o componente do módulo dentro do OMSI. Ele pede à bridge do OmsiLaunch hooks nas importações do `Omsi.exe` e os usa para três recursos (read-ahead, write-behind, abertura somente leitura) e para os relatórios de uso que alimentam o [programa de sessão](session-program.md). Esta página descreve o que cada recurso faz, quando se aplica, o que garante, como termina e o que conta. Os recursos são chaves separadas ([configurações](../reference/settings.md#the-native-features)); os relatórios de uso estão sempre ligados.

Páginas relacionadas: [como o prefetch funciona](how-it-works.md), [segurança](safety.md), [erros e degradação](../reference/errors-and-degradation.md), [eventos](../reference/events-and-notices.md), [limitações conhecidas](../reference/known-limitations.md).

<a id="why-a-shim"></a>
## Por que um shim

O runtime do OMSI lê arquivos byte a byte: para cada byte ele chama `ReadFile` e, para testar o fim do arquivo, `SetFilePointer(current, 0)` e `GetFileSize`. Em um modelo de alguns megabytes, isso são milhões de chamadas de sistema. O shim atende essas chamadas a partir da memória que ele guarda para o handle, de modo que o sistema operacional é consultado uma vez por bloco em vez de uma vez por byte, e tudo o que o OMSI vê continua exatamente igual ao que uma chamada sem hook teria retornado.

Daqui em diante, o termo read-ahead designa a leitura antecipada em blocos (o shim lê um bloco inteiro à frente e responde às leituras pequenas a partir dele), e write-behind designa a gravação adiada (as gravações pequenas são reunidas em um buffer e escritas depois, em um bloco só). Os dois nomes são mantidos em inglês em toda a documentação, porque são os nomes das configurações e dos contadores.

<a id="hooks-the-component-requests"></a>
## Hooks que o componente solicita

O componente pede à bridge os hooks apenas por alvo simbólico (`iat:<dll>!<function>`), de modo que não carrega nenhum endereço do OMSI e funciona em qualquer build do OMSI 2 cujo `Omsi.exe` importe essas funções da forma habitual:

| Alvo | Usado para |
| --- | --- |
| `kernel32.dll!CreateFileW`, `ReadFile`, `CloseHandle` | Obrigatórios: o módulo não faz nada útil sem eles. Se algum for recusado, a inicialização do componente falha e a bridge remove o que tiver sido instalado. |
| `kernel32.dll!WriteFile` | Relatórios de gravação, write-behind, atendimento de um handle reaberto. Sempre solicitado. |
| `kernel32.dll!SetFilePointer`, `GetFileSize`, `SetEndOfFile` | Solicitados quando read-ahead, write-behind ou abertura somente leitura está ligado. Mantêm posições e tamanhos coerentes. |
| `d3dx9.dll!D3DXCreateTextureFromFileExW`, `...ExA`, `...FromFileW`, `D3DXLoadMeshFromXA` | Informam texturas e malhas que o Direct3D carrega por conta própria, fora das importações do `Omsi.exe`. Solicitados apenas para as funções encontradas em um `d3dx9.dll` já carregado. |

Os três recursos só começam quando **os sete** hooks do kernel32 estão instalados. Com um hook de posição ausente, nada é interceptado e o OMSI mantém o comportamento exato; os relatórios de uso continuam funcionando. O resultado de cada solicitação está no evento `hooks` ([eventos](../reference/events-and-notices.md#the-hooks-event)).

<a id="read-ahead"></a>
## Read-ahead

Um handle é elegível quando o OMSI abre um arquivo existente em disco com acesso de leitura e sem as flags que mudam a semântica das leituras (overlapped, sem buffer, delete on close, write through, acesso aleatório). O runtime Delphi do OMSI abre a maioria dos assets com acesso de escrita, embora só os leia; esses handles também são elegíveis.

- O buffering começa após oito leituras minúsculas consecutivas (256 bytes ou menos) no handle. Até então, e para qualquer leitura maior que 256 bytes, a chamada vai ao sistema como de costume.
- Então o shim lê blocos de 64 KiB e atende as leituras pequenas a partir do bloco.
- `SetFilePointer` e `GetFileSize` são atendidos a partir do estado do shim enquanto ele está ativo (o tamanho é lido uma vez, na abertura).
- Exatidão: leituras, posições, tamanhos e o comportamento de fim de arquivo são os que o kernel teria retornado. Uma leitura perto do fim retorna os bytes restantes e depois zero bytes.
- A primeira gravação ou truncamento por meio do handle, ou um formato de chamada incomum (overlapped, por exemplo), entrega ao kernel a posição verdadeira e encerra o shim para aquele handle. O handle continua funcionando nativamente.

Memória: 64 KiB por handle ativo, alocados quando o buffering começa e liberados no fechamento.

<a id="write-behind"></a>
## Write-behind

O OMSI grava o seu cache de terreno, `tile_X_Y.map.terrain_*.rdy`, alguns bytes por vez. Um handle é elegível quando o nome dele termina em `.rdy`, é aberto com acesso de escrita (não append, não delete, não elegível para read-ahead), sem as flags overlapped, sem buffer, delete on close ou write through, e é um arquivo em disco.

- Gravações menores que 256 KiB são reunidas em um buffer de 256 KiB; uma gravação maior esvazia o buffer primeiro e vai direto ao sistema.
- O buffer é descarregado antes de uma leitura, de um seek, de uma consulta de tamanho, de uma mudança de fim de arquivo ou de um fechamento por meio do mesmo handle, antes de qualquer nova abertura do mesmo caminho e quando o buffer está cheio. A ordem é preservada.
- Um descarregamento que falha é repetido uma vez; uma segunda falha desliga o write-behind para novos handles e é contada (`write_behind_failed`). Os bytes desse bloco são perdidos e o OMSI já foi informado de que foram gravados. O arquivo `.rdy` é um cache regenerável, e é por isso que apenas ele passa por buffer.
- Quando o módulo para (uma saída normal do OMSI), todo buffer é descarregado primeiro. Quando o OMSI é terminado à força, os bytes ainda em buffer são perdidos, até 256 KiB por `.rdy` aberto ([segurança](safety.md#what-happens-when-something-goes-wrong)).

Memória: 256 KiB por handle ativo.

<a id="read-only-open"></a>
## Abertura somente leitura

Fechar um handle que foi aberto com acesso de escrita custa uma atualização síncrona de metadados, e o runtime do OMSI abre assets com acesso de escrita por padrão. Para assets que o OMSI apenas lê, o shim abre o arquivo sem acesso de escrita.

Uma chamada é rebaixada quando **todas** estas condições valem:

- a abertura é de um arquivo existente (`OPEN_EXISTING`) com acesso de leitura e de escrita, sem acesso generic-all, append ou delete e sem delete-on-close ou write-through;
- o caminho está abaixo de uma das `read_only_folders` (padrão `vehicles\`): o caminho começa com a pasta ou a contém como segmento de caminho (assim `D:\OMSI 2\Vehicles\Bus\x.o3d` corresponde a `vehicles\`);
- a extensão é uma de `.o3d .sco .bus .ovh .hof .osc .cti .org .cfg .txt .wav .ogg .bmp .png .tga .jpg .dds .x`.

O shim também amplia o modo de compartilhamento para incluir compartilhamento de leitura e de escrita, de modo que um arquivo possa ser reaberto. Se o compartilhamento mais amplo for recusado, ou o handle não puder ser rastreado, o arquivo é aberto exatamente como o OMSI pediu.

Se o OMSI gravar em um handle que foi aberto somente leitura mesmo assim, o shim reabre o arquivo com o acesso que o OMSI pediu, na posição que o OMSI espera, e atende esse handle daí em diante (contado como `read_only_reopened`; uma reabertura que falha é `read_only_reopen_failed`). Quando o módulo para, o handle recebe a posição de volta e o handle somente leitura do próprio jogo volta a ser usado; uma gravação por ele depois disso seria recusada. Isso exige uma gravação em um asset de veículo que esteja aberto no exato momento em que o módulo para.

<a id="use-reports"></a>
## Relatórios de uso

Para o aprendizado do programa de sessão, o componente informa ao pipe da sessão (lotes a cada 60 ms, UTF-8, uma linha cada):

| Linha | Significado | Enviada |
| --- | --- | --- |
| `U<TAB><path>` | Primeiro uso de um arquivo: uma abertura por `CreateFileW`, ou um carregamento pelas funções D3DX com hook | Uma vez por arquivo (a primeira vez) |
| `T<TAB><path>` | Uma abertura de um `tile_X_Y.map`, informação de posição | Toda vez |
| `W<TAB><path>` | O jogo gravou no arquivo ou o truncou | A primeira gravação por meio de cada handle |
| `X<TAB><path>` | O OMSI não consegue substituir ou truncar um arquivo porque o programa de sessão o mantém | De imediato, à frente da fila |

O `X` funciona assim. Um arquivo mantido é mapeado, e o Windows se recusa a truncar ou substituir um arquivo mapeado (`ERROR_USER_MAPPED_FILE`). Quando o `CreateFileW` ou o `SetEndOfFile` do OMSI é recusado por esse motivo, o shim envia uma linha `X` e repete a chamada a cada 25 ms por até 1,5 s (60 tentativas). O programa de sessão libera o arquivo ao recebê-la e nunca mais o mantém; a chamada do OMSI então tem sucesso. Se nenhum programa de sessão estiver conectado, a chamada falha como falharia sem o módulo. As liberações são contadas como `release_requests`.

O feed mantém no máximo 400.000 linhas na fila e lembra até 500.000 caminhos distintos; além disso, as linhas são descartadas e contadas (`feed_dropped`), e os arquivos usados pela primeira vez depois de 500.000 caminhos distintos deixam de ser informados. Se o pipe não puder ser alcançado por dez segundos seguidos (por exemplo, porque o programa de sessão morreu), o feed desiste e não enfileira mais nada; os três recursos continuam funcionando.

<a id="limits"></a>
## Limites

| Limite | Valor |
| --- | --- |
| Handles rastreados | 4096 ao mesmo tempo; acima disso um handle é atendido nativamente e contado como `track_full` |
| Bloco de read-ahead | 64 KiB |
| Limiar de read-ahead | oito leituras consecutivas de 256 bytes ou menos |
| Bloco de write-behind | 256 KiB |
| Novas tentativas para um arquivo mantido | 60 vezes 25 ms (1,5 s) |
| Lote do feed | a cada 60 ms, em pedaços de cerca de 30 KB |

<a id="stop"></a>
## Parada

Quando o módulo para, o shim grava todo byte em buffer, devolve todo handle com a posição que o OMSI espera e transforma todo hook em uma simples passagem direta antes que a bridge remova os hooks. A DLL permanece carregada, inerte, até o OMSI encerrar. As estatísticas são emitidas no evento `stats`.

<a id="next"></a>
## Próximos passos

[Dados aprendidos](learned-data.md) · [Memória e orçamento](memory-and-budget.md) · [Segurança](safety.md)
