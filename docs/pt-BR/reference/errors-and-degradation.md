# Erros e degradação

<!-- l10n: source=reference/errors-and-degradation.md source-sha256=6496b65ad65f8d10f5b1ae8ae290fec42f015ad46c27dd4b580f195bad33cf97 -->
> Tradução da [página original em inglês](../../reference/errors-and-degradation.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

O Omsi Prefetch é construído para falhar pequeno: a política de falha no seu manifesto é `degrade`, e cada uma das suas partes continua funcionando quando outra não consegue. Esta página lista o que pode dar errado, qual código do OmsiLaunch ou mensagem do módulo mostra isso, o que o módulo faz e o que você pode fazer. Os códigos são os da referência de erros do OmsiLaunch; esta página diz o que eles significam para este módulo.

Páginas relacionadas: [eventos](events-and-notices.md), [formato do log](log-format.md), [segurança](../concepts/safety.md#what-happens-when-something-goes-wrong), [diagnosticando lentidão](../guides/diagnosing-slowness.md), [convivência](../guides/coexisting.md), [instalação](../getting-started/installation.md).

<a id="the-policy"></a>
## A política

`failure_policy` é `degrade`, com `prepare_ms` = 600.000 ms e `host_stop_ms` = 20.000 ms. Sob `degrade`, uma falha do programa de sessão do módulo (ele não consegue iniciar, termina antes de ficar pronto ou não fica pronto a tempo) **deixa o módulo na sessão**: o diagnóstico é registrado, o OMSI inicia e as partes que não precisam do programa continuam funcionando. Uma falha de uma fase dentro do OMSI (a inicialização do componente nativo, o armamento) libera o módulo dentro do OMSI e a sessão continua; a sessão nunca falha por causa deste módulo.

O planejamento é diferente para um módulo que entra sozinho (ativação `default`): se ele não pode ser carregado de forma alguma, o plano reporta o aviso `OL_W_MODULE_NOT_LOADED` e a sessão roda sem o módulo. Somente quando você pede o módulo explicitamente (`/module:omsi-prefetch`) é que um módulo que não pode ser carregado torna o plano não executável.

<a id="at-planning"></a>
## No planejamento

| Situação | Código | O que acontece | O que fazer |
| --- | --- | --- | --- |
| Um arquivo da pasta do módulo foi alterado, está ausente ou é alcançado por um link | `OL_E_MODULE_INTEGRITY` (como `OL_W_MODULE_NOT_LOADED` para a ativação padrão) | O módulo não é carregado. | Reinstale a pasta do módulo ([instalação](../getting-started/installation.md)). |
| O `module.json` está ausente ou inválido, ou a pasta não se chama `omsi-prefetch` | `OL_E_MODULE_MANIFEST_INVALID` | Não é carregado. | Reinstale. |
| O OmsiLaunch não oferece o protocolo de bridge 2 | `OL_E_MODULE_PROTOCOL_UNSUPPORTED` | Não é carregado. | Use um OmsiLaunch que ofereça o protocolo 2 (`0.1.0-beta4` ou posterior). |
| O build do OMSI não resolve uma capacidade de que o módulo precisa (`bridge.hooks.iat`) | `OL_E_MODULE_CAPABILITY_UNRESOLVED` | Não é carregado. | Veja a referência de capacidades e `profiles` do OmsiLaunch. |
| O plugin `OmsiLoadingPoC.dll` está ligado em `plugins\*.opl` | `OL_E_MODULE_CONFLICT` (como `OL_W_MODULE_NOT_LOADED` para a ativação padrão) | Não é carregado. | Desligue esse plugin ([convivência](../guides/coexisting.md)). |
| Uma configuração ou um valor de flag é inválido, ou uma chave não existe | `OL_E_MODULE_SETTING_INVALID` | O plano não é executável. | Corrija o valor; `/list:RuntimeModules` mostra as configurações. |
| Uma entrada de `.omsilaunch\modules.ini` não pode ser usada, ou configurações são pedidas para um módulo que não está carregado | `OL_W_MODULE_SETTING_IGNORED` | O padrão é usado. | Corrija ou remova a entrada. |
| Um preset define uma chave (ou liga/desliga o módulo) que a linha de comando também define | `OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT` | O plano não é executável. | Remova um dos dois ([perfis de sessão](../guides/session-profiles.md)). |

<a id="before-omsi-the-session-program"></a>
## Antes do OMSI: o programa de sessão

| Situação | Código ou mensagem | O que acontece |
| --- | --- | --- |
| O programa não pode ser iniciado, ou termina antes de `ready` | `OL_E_MODULE_LOAD_FAILED` (diagnóstico de sessão), evento `host.failed` | O módulo permanece; o OMSI inicia. O componente nativo não encontra pipe, desiste após dez segundos e não envia relatórios; read-ahead, write-behind e abertura somente leitura continuam funcionando. Nada é mantido nem aprendido nesta sessão. Uma causa é a falta do .NET 6 Desktop Runtime (x64), de que o programa precisa. |
| O programa não fica pronto dentro de `prepare_ms` | `OL_E_MODULE_PHASE_TIMEOUT`, evento `host.failed` | O módulo permanece e o OMSI inicia. O limite do próprio programa para carregar é de cinco minutos, então chegar a isto exige uma leitura do mapa travada ou extremamente lenta. |
| A preparação lança uma exceção | `OL_E_MODULE_OPERATION_FAILED`, detalhe `prefetch: the preparation failed: <message>`; linha de log `prepare failed:` | O programa ainda informa que está pronto; o módulo continua com o que já tinha mantido. |
| Sem mapa na sessão, ou com um mapa que não pode ser lido | Log: `no map in this session...` / `the map ... cannot be read...` | Nada é mantido nem aprendido; os recursos nativos funcionam. |
| O orçamento é zero (o orçamento automático com menos de `reserve_mb` livres) | Log: `no memory to hold anything: preparation skipped` | Nada é mantido; os recursos nativos e o aprendizado funcionam. |
| A memória livre cai abaixo do piso de carregamento durante a preparação | Log: `prepared in ... (free memory low)` | A preparação termina mais cedo; a previsão espera e tenta de novo durante a sessão. |
| O pipe da sessão não pode ser criado | Log: `the session channel cannot be served: ...` | O preloader mantém arquivos, mas o componente nativo do OMSI não consegue informá-lo; nada é aprendido e nenhum tile é previsto. |
| O mínimo do working set não pode ser elevado | Log: `the working set minimum was not raised...` | As páginas mantidas podem ser reduzidas pelo Windows sob pressão de memória. |

<a id="inside-omsi-the-native-component"></a>
## Dentro do OMSI: o componente nativo

| Situação | Onde você vê | O que acontece |
| --- | --- | --- |
| `CreateFileW`, `ReadFile` ou `CloseHandle` não podem receber hook | O evento `hooks` (um valor diferente de `0`); `OL_E_MODULE_PHASE_FAILED` ("the native component's start returned 2") | A fase falha e a bridge remove o que tiver sido instalado; o módulo é liberado dentro do OMSI e a sessão continua. |
| A bridge é do protocolo 1, ou a sua tabela não tem `get_value` | A inicialização retorna `1` | O componente se recusa a iniciar. Não ocorre com uma bridge que oferece o protocolo 2. |
| Outro plugin já alterou a mesma importação | O evento `hooks` mostra `4` para esse hook | Esse hook é recusado, nunca sobrescrito. Para um dos três hooks obrigatórios, veja acima; para os demais, os recursos dependentes ficam desligados. |
| Um hook de posição (`SetFilePointer`, `GetFileSize`, `SetEndOfFile`) ou `WriteFile` é recusado | O evento `hooks` | Read-ahead, write-behind e abertura somente leitura ficam todos desligados; os relatórios de uso continuam funcionando. |
| Uma função D3DX não recebe hook | O evento `hooks` mostra `-1` ou um código | Texturas e malhas carregadas pelo próprio Direct3D não são informadas como usos; nada mais muda. |
| Mais de 4096 handles estão abertos ao mesmo tempo | `track_full` em `stats` | Os handles extras são atendidos nativamente. |
| Um descarregamento de write-behind falha duas vezes | `write_behind_failed` em `stats` | O write-behind é desligado para novos handles. Os bytes desse bloco são perdidos; o OMSI já tinha sido informado de que foram gravados. O arquivo é um cache `.rdy` regenerável. |
| O programa de sessão não pode ser alcançado por dez segundos | `feed_dropped` em `stats` | O componente nativo deixa de informar. Os três recursos continuam funcionando. |
| O OMSI não consegue substituir um arquivo mantido | `release_requests` em `stats` | Uma liberação é solicitada e a chamada é repetida por até 1,5 s; sem programa conectado, a chamada falha como falharia sem o módulo. |

<a id="during-the-session"></a>
## Durante a sessão

| Situação | O que acontece |
| --- | --- |
| A memória livre cai abaixo de 1.024 MB | A guarda libera arquivos mantidos que o OMSI não usou, até que cerca de 1.536 MB fiquem livres, e interrompe o carregamento até que mais de 2.560 MB estejam livres de novo. |
| A fila do pipe está cheia | Linhas `U` (dicas) são descartadas e contadas; linhas `T` e `W` esperam até 50 ms; linhas `X` nunca são enfileiradas. |
| Um cliente que não é o OMSI se conecta | É desconectado e contado (`feed_refused`); linha de log `feed: refused a client that is not OMSI's process`. |
| Uma atividade em segundo plano lança uma exceção | O log diz `<activity>: stopped by an unexpected error`; as outras atividades continuam. |
| Um salvamento dos dados aprendidos falha | Linha de log `save: ...`; a sessão continua; os dados anteriores permanecem. |
| O log não pode ser gravado | O programa roda sem log. |

<a id="stopping"></a>
## Parando

| Situação | O que acontece |
| --- | --- |
| O OmsiLaunch pede ao programa que pare | Ele interrompe as suas atividades (esperando até seis segundos), salva, emite `session` e encerra. O OmsiLaunch espera `host_stop_ms` (20 s) e depois o encerra. |
| O programa não para a tempo | O OmsiLaunch o encerra (evento `host.killed`); a memória mantida é liberada junto com o processo; o que resta é o último salvamento periódico. |
| O OMSI é terminado à força | Veja [segurança](../concepts/safety.md#what-happens-when-something-goes-wrong): os bytes de `.rdy` em buffer são perdidos; nada mais. |

<a id="next"></a>
## Próximos passos

[Eventos](events-and-notices.md) · [Limitações conhecidas](known-limitations.md) · [Diagnosticando lentidão](../guides/diagnosing-slowness.md)
