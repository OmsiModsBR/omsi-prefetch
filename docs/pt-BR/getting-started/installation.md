# Instalação

<!-- l10n: source=getting-started/installation.md source-sha256=8330a34984ef746b8faf0a82a85610a4ef35b2900be5b52439ab132e337b0168 -->
> Tradução da [página original em inglês](../../getting-started/installation.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Esta página lista o que o Omsi Prefetch `1.0.0-beta.1` exige, como a pasta do módulo é instalada em uma instalação do OmsiLaunch, como confirmar que o OmsiLaunch enxerga o módulo e vai carregá-lo, e como removê-lo ou atualizá-lo. Ela pressupõe que o OmsiLaunch já está instalado na raiz do OMSI, como descrito na página de instalação do OmsiLaunch. O módulo é um Runtime Module no sentido da referência de Runtime Modules do OmsiLaunch; as regras de lá (local, integridade, ativação) são as regras desta página.

Páginas relacionadas: [visão geral](overview.md), [primeira execução](first-run.md), [desligando](../guides/turning-off.md), [compatibilidade](../reference/compatibility.md), [erros e degradação](../reference/errors-and-degradation.md), [arquivos e diretórios](../reference/files-and-directories.md).

<a id="requirements"></a>
## Requisitos

| Requisito | Detalhe | Falha quando ausente |
| --- | --- | --- |
| OmsiLaunch `0.1.0-beta4` ou posterior | O módulo declara `bridge_protocol` `{ "min": 2, "max": 2 }` e as capacidades `bridge.events` e `bridge.hooks.iat`. Qualquer OmsiLaunch que ofereça o protocolo de bridge 2 consegue carregá-lo. | Não aparece como utilizável: `OL_E_MODULE_PROTOCOL_UNSUPPORTED` (ou `OL_E_MODULE_CAPABILITY_UNRESOLVED`). |
| Windows 10 ou posterior, 64 bits | A mesma plataforma que o OmsiLaunch exige. O programa de sessão é x64. | O próprio OmsiLaunch recusa a plataforma. |
| .NET 6 Desktop Runtime, **x64** | O `Prefetch.Preloader.exe` é um programa `net6.0-windows` dependente do framework (o Windows Forms é usado pelas suas duas janelas opcionais). O controlador do OmsiLaunch precisa do mesmo runtime, portanto ele já está presente quando o OmsiLaunch roda. | O OmsiLaunch inicia o programa e ele termina antes de ficar pronto: `OL_E_MODULE_LOAD_FAILED` (veja [erros e degradação](../reference/errors-and-degradation.md)). |
| .NET 6 Runtime, **x86** | O `Prefetch.Module.dll` (`net6.0`, AnyCPU) é carregado dentro do `Omsi.exe` de 32 bits pelo plugin do OmsiLaunch, que de qualquer forma precisa do runtime x86. | O plugin do OmsiLaunch não inicia. |
| OMSI 2 | O componente nativo faz hook nas importações do `Omsi.exe` pelo nome; ele não carrega nenhum endereço do OMSI. Veja [compatibilidade](../reference/compatibility.md). | Por hook, veja [erros e degradação](../reference/errors-and-degradation.md). |
| Memória livre | O módulo mantém arquivos na RAM até o seu orçamento ([memória e orçamento](../concepts/memory-and-budget.md)). Com o orçamento automático, ele não mantém nada quando há menos de `reserve_mb` (4096 MB) livres. | O módulo continua rodando, sem manter nada ([guia por tamanho de memória](../guides/choosing-settings-by-memory.md)). |
| Espaço em disco | Os dados dele são limitados (256 MB por padrão, veja [arquivos e diretórios](../reference/files-and-directories.md)). | Nenhuma; o módulo pula o que não consegue gravar. |
| Nenhum plugin `OmsiLoadingPoC.dll` | A prova de conceito que este módulo substitui. Ela é declarada como conflito. | `OL_W_MODULE_NOT_LOADED` / `OL_E_MODULE_CONFLICT`; veja [convivência](../guides/coexisting.md). |

<a id="what-the-module-folder-contains"></a>
## O que a pasta do módulo contém

Um módulo é uma pasta com o nome do seu id. Para este módulo, ela contém exatamente estes sete arquivos:

| Arquivo | Conteúdo |
| --- | --- |
| `module.json` | O manifesto (`omsilaunch.runtime-module/v1`): id, versão, as 29 configurações, o conflito, a política de falha e o pin SHA-256 de cada componente. |
| `Prefetch.Module.dll` | Componente gerenciado, carregado dentro do OMSI. |
| `prefetch-native.dll` | Componente nativo (x86), hooks dentro do OMSI. |
| `Prefetch.Preloader.exe` | Programa de sessão (x64), iniciado antes do OMSI. |
| `NOTICE.txt` | Aviso de direitos autorais e de licença. |
| `GPL-3.0.txt`, `LGPL-3.0.txt` | Os textos das licenças. |

<a id="install"></a>
## Instalar

1. Feche o OMSI e confirme que nenhuma sessão do OmsiLaunch está rodando (`OmsiLaunch.exe /recovery-status` não deve informar uma recuperação pendente).
2. Copie a pasta `omsi-prefetch` inteira para o diretório de módulos da instalação, de modo que o manifesto fique em:

   ```text
   <OMSI root>\plugins\OmsiLaunch.Modules\omsi-prefetch\module.json
   ```

   Crie `plugins\OmsiLaunch.Modules\` se ela não existir. O nome da pasta deve ser igual ao id do módulo, `omsi-prefetch`.
3. Não renomeie, não mova nem edite nenhum arquivo da pasta, não substitua um dos arquivos por um link ou uma junction e não copie os arquivos um a um de builds diferentes. Cada componente tem o seu SHA-256 fixado em `module.json`, e o OmsiLaunch recusa um módulo cujos arquivos não correspondam (`OL_E_MODULE_INTEGRITY`). Um arquivo alcançado por uma junction ou por um link simbólico também é recusado.

O OmsiLaunch nunca copia um módulo por você, e uma sessão nunca grava em `plugins\OmsiLaunch.Modules\`.

<a id="building-the-folder-from-source"></a>
### Montando a pasta a partir do código-fonte

O repositório compila contra um checkout do OmsiLaunch (protocolo de bridge 2 ou posterior) que precisa ser compilado antes. O `Directory.Build.props` aponta `OmsiLaunchRoot` para `..\OmsiLaunch-spec-pack\OmsiLaunch`; sobrescreva-o com `-p:OmsiLaunchRoot=...` ou com o parâmetro `-OmsiLaunchRoot` do script.

```text
powershell -File tools\Build-Modules.ps1
```

O script compila os três componentes (o nativo com o MSBuild e as ferramentas de build C++ do Visual Studio, x86), publica o preloader como um único arquivo dependente do framework, monta `artifacts\modules\omsi-prefetch\` com o `module.json` fixado e executa os testes offline (veja [cobertura de testes](../status/test-coverage.md)); `-SkipTests` pula os testes. Ele nunca copia nada para uma instalação do OMSI: copiar a pasta montada é o passo 2 acima.

<a id="verify"></a>
## Verificar

Execute a partir da raiz do OMSI.

```text
OmsiLaunch.exe /list:RuntimeModules
```

Esperado: uma entrada com id `omsi-prefetch`, versão `1.0.0-beta.1`, ativação `default`, válida, e as configurações dela com tipo, padrão e flag (por exemplo `ram_budget_mb` com a flag `prefetch-ram`). Um módulo inválido aparece listado com o seu código de erro no lugar (`OL_E_MODULE_INTEGRITY`, `OL_E_MODULE_MANIFEST_INVALID`).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /plan --json
```

Esperado: em `Diagnostics`, uma entrada `module.planned` cujos dados citam `module` `omsi-prefetch` e `activation` `default`, e `IsRunnable` verdadeiro. Use qualquer mapa da sua instalação (`/list:Maps`). O planejamento nunca inicia o OMSI e nunca grava na instalação. Estes diagnósticos dizem que o módulo vai participar:

| Diagnóstico do plano | Significado |
| --- | --- |
| `module.planned` | O módulo entra na sessão. |
| `module.disabled` | O módulo é mantido de fora por `/no-module:omsi-prefetch`, por um preset ou por `enabled=false` em `.omsilaunch\modules.ini`. |
| `OL_W_MODULE_NOT_LOADED` | O módulo entraria por padrão, mas não pode ser carregado (o `Data["code"]` dele diz o motivo). A sessão ainda roda, sem o módulo. |
| `OL_W_MODULE_SETTING_IGNORED` | Uma entrada de `.omsilaunch\modules.ini` para este módulo é inutilizável e foi ignorada. |
| `OL_E_MODULE_SETTING_INVALID` | Uma configuração ou flag que você pediu não é válida para o módulo (o plano não é executável). |

Para confirmar que o módulo está ativo em uma sessão real, inicie uma e procure qualquer um destes sinais:

- `OmsiLaunch.exe events read` (de um segundo console) lista `module.omsi-prefetch.host.started`, `module.omsi-prefetch.host.ready`, `module.omsi-prefetch.prepared`, `module.omsi-prefetch.armed` e `module.omsi-prefetch.hooks` ([eventos](../reference/events-and-notices.md)).
- Um arquivo de log `preloader-<date>-<time>.log` aparece em `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\logs\` com a primeira linha `session <id>, module 1.0.0-beta.1, OmsiLaunch <version>` ([formato do log](../reference/log-format.md)).
- Com `/prefetch-monitor`, a janela do monitor abre ([primeira execução](first-run.md)).

<a id="upgrade"></a>
## Atualizar

Substitua a pasta `omsi-prefetch` inteira pela nova enquanto nenhuma sessão estiver rodando. Misturar arquivos de duas versões falha na verificação de integridade. Os dados que o módulo guarda sobre os seus mapas ([dados aprendidos](../concepts/learned-data.md)) continuam válidos entre atualizações enquanto os formatos não mudarem; apague-os com `/clear-module-data:omsi-prefetch` se quiser recomeçar do zero.

<a id="remove"></a>
## Remover

Apague `<OMSI root>\plugins\OmsiLaunch.Modules\omsi-prefetch\`. Os dados que o módulo gravou permanecem em `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\` até você executar `OmsiLaunch.exe /clear-module-data:omsi-prefetch` (antes ou depois de remover a pasta) ou apagar essa pasta. Remover o módulo não muda mais nada: ele nunca modificou um arquivo do OMSI.

<a id="next"></a>
## Próximos passos

[Primeira execução](first-run.md) · [Medindo o ganho](measuring-the-gain.md) · [Escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md)
