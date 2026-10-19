# Cobertura de testes

<!-- l10n: source=status/test-coverage.md source-sha256=a736b41ba4e886aa69b08b9b0dfb38d9d0e2fa50eb3534635bfdaffb66ce9051 -->
> Tradução da [página original em inglês](../../status/test-coverage.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Esta página lista o que os testes offline do Omsi Prefetch `1.0.0-beta.1` cobrem, pelo nome do gate, e o que não cobrem. Os testes são compilados e executados por `tools\Build-Modules.ps1` (a menos que `-SkipTests` seja informado) e nunca iniciam o OMSI. A lista abaixo foi extraída de `prefetch\tests\Prefetch.Tests\Program.cs` (os gates do programa de sessão e do pacote) e de `prefetch\tests\native\ShimTests.cpp` (o componente nativo). Quando esta página e os testes discordam, valem os testes e a página contém um bug.

Páginas relacionadas: [limitações conhecidas](../reference/known-limitations.md), [visão geral](../getting-started/overview.md), [shim nativo](../concepts/native-shim.md), [programa de sessão](../concepts/session-program.md).

<a id="what-the-tests-are"></a>
## O que são os testes

| Suíte | O que é | Como roda |
| --- | --- | --- |
| `Prefetch.Tests.exe` (C#, x64) | Gates unitários sobre uma instalação sintética (uma pequena pasta do OMSI em um diretório temporário: um mapa com três tiles e dois pontos de entrada, texturas do solo, uma lista de IA, objetos de cenário com modelos e texturas, um ônibus de IA e um ônibus de usuário), e depois o programa de sessão empacotado executado de ponta a ponta. | `Prefetch.Tests.exe --package <artifacts\modules\omsi-prefetch> --template <prefetch\module.template.json>`; sem `--package`, os gates `package.manifest` e `package.omsilaunch-pack-verify` são pulados e é usado o programa compilado ao lado dos testes, e sem `--template` o gate `manifest.protocol-3` é pulado. |
| `ShimTests.exe` (C++, x86) | O componente nativo, carregado como DLL contra uma **bridge falsa** (as solicitações de hook são atendidas por uma tabela substituta; os desvios são chamados diretamente), com arquivos reais e um pipe nomeado real. | `ShimTests.exe`; cada cenário roda em seu próprio processo. |
| `Prefetch.Tests.exe --survey <OMSI folder> <maps\<map>\global.cfg>` | Não é um gate: lê uma instalação real somente para leitura e informa o mapa, o índice de tiles e os veículos com tempos. | Manual ([medindo o ganho](../getting-started/measuring-the-gain.md#estimating-a-map-before-you-play-it)). |

<a id="session-program-gates-prefetchtestsexe"></a>
## Gates do programa de sessão (`Prefetch.Tests.exe`)

| Gate | O que verifica |
| --- | --- |
| `text.encodings-and-tokens` | Decodificação de UTF-16, UTF-8 com BOM e Windows-1252; quebras de linha CRLF, LF e CR; a quebra de linha final; os tokens de uma linha (primeiro campo de tab e strings entre aspas) e que linhas de seção não os têm. |
| `text.o3d-texture-names` | O nome de textura armazenado em um `.o3d` (prefixado por comprimento, extensão de imagem) é encontrado, e um arquivo sem nenhum não produz nenhum. |
| `settings.budget` | O orçamento automático é 70 % da memória livre depois de 4 GB, no máximo 12 GB, zero quando há pouca livre; um valor explícito; escolhas e listas; os padrões. |
| `map.global-cfg` | Nome amigável, tiles, pontos de entrada, tiles iniciais na ordem do arquivo e depois na ordem dos nomes, todos os pontos de entrada quando nenhum é apresentado, texturas do solo citadas, arquivos do mapa inteiro (sem tiles, sem `.osn`), um mapa ausente ou externo é recusado, a impressão digital acompanha os tiles. |
| `tiles.closure-and-index` | Os arquivos de um tile (o arquivo do tile primeiro, nunca o `LM.bmp` de streaming), os tiles ao redor de um ponto, o índice de tiles salvo carregado de volta com os mesmos arquivos e um índice de outra impressão digital não carregado. |
| `vehicles.scopes` | A IA do mapa encontrada a partir das suas listas; os escopos `load`, `essential` e `complete` (os arquivos que cada um inclui e exclui); sons desligados; veículos do usuário; veículos do usuário listados, com o log de uma entrada pulada. |
| `settings.usage` | Os padrões da reserva, do piso, do teto de veículos, dos valores de pré-abertura, do monitor desligado, do log de depuração desligado, do aviso de primeira abertura e do limite de dados; valores lidos e limitados aos seus limites; o orçamento automático segue `reserve_mb`; um orçamento explícito interrompe o carregamento com 1 GB livre. |
| `disk.log-and-data-bounded` | Um log nunca passa de 1 MiB, apenas três são mantidos, linhas detalhadas não são escritas sem `debug_log`, o limite é marcado uma vez; os dados de mapas antigos são removidos do menos recentemente usado para o mais recente, nunca o mapa atual, mesmo com limite zero. |
| `holdings.hold-release` | Um arquivo é mapeado e mantido uma vez; arquivos que o jogo reescreve e arquivos de sessão nunca são mantidos; orçamento e contabilidade; um arquivo ausente; primeiro uso e ordem do primeiro uso; um arquivo gravado é liberado de imediato e nunca mais mantido; liberação sob pressão de memória. |
| `holdings.game-streamed-files-never-held` | Toda extensão que o jogo reescreve ou lê em streaming (`.osn .rdy .tbmp .odr .tmp .log .sav .bak .owt .prt .lm.bmp`) é negada e nunca mapeada; os arquivos do mapa inteiro e os arquivos próprios de um tile excluem os arquivos de streaming; o próprio arquivo do tile ainda é mantido. |
| `feed.release-is-never-queued` | Uma liberação (linha `X`) é respondida de imediato atrás de uma rajada de linhas `U` lentas, e sobrevive a uma enxurrada maior que a fila, que descarta e conta as dicas. |
| `feed.omsi-version-is-log-only` | Uma linha `V` (a versão do OMSI, protocolo de bridge 3) é escrita no log como `OMSI <version>`; não é contada, não é entregue à sessão como relatório de uso, e um texto que não é simples (um caminho, um texto muito longo) é descartado. |
| `manifest.protocol-3` (com `--template`) | O template, lido pelo próprio parser de manifesto do OmsiLaunch: identidade e versão `1.0.0-beta.1`, `bridge_protocol` `{ "min": 3, "max": 3 }` aceito por este OmsiLaunch, capacidades obrigatórias `bridge.events` e `bridge.hooks.iat` e nenhuma opcional, todo componente com pin, ativação `default`, 29 configurações. |
| `package.manifest` (com `--package`) | A pasta empacotada é um módulo válido para o próprio parser de manifesto e pins do OmsiLaunch; identidade, ativação e protocolo de bridge 3; os componentes de host e nativo; 29 configurações, as flags de `ram_budget_mb` e `monitor`, o monitor e o log de depuração desligados por padrão; o conflito com `OmsiLoadingPoC.dll`; exatamente os sete arquivos do módulo; o texto da LGPL. |
| `package.omsilaunch-pack-verify` (com `--package`) | O próprio empacotador do OmsiLaunch (`ModulePacker`, o que `/verify-module` executa) sobre a pasta montada: três componentes, todo pin confere, empacotar não grava nada; em uma cópia com um componente alterado a divergência é apontada e o catálogo recusa o módulo (`OL_E_MODULE_INTEGRITY`). |
| `session.end-to-end` | O programa de sessão empacotado executado pelo próprio lançador de programa de sessão do OmsiLaunch (job object) com um cliente OMSI falso: **primeira sessão** (ready, `prepared` com `first_open`, tiles iniciais, mapa e IA mantidos, um tile além de `tile_radius` não mantido, arquivos reescritos não mantidos, `omsi-started`, o feed do pipe, liberação a pedido do OMSI, stop, código de saída 0, o resumo `session` com todas as linhas recebidas, a lista aprendida na ordem do primeiro uso, a lista de escrita, o índice de tiles salvo, um log); **segunda sessão** (não é uma primeira abertura, um arquivo aprendido mantido, um arquivo substituído não mantido de novo, um cliente que não é o OMSI desconectado e contado, as linhas dele ignoradas). |

<a id="native-component-scenarios-shimtestsexe"></a>
## Cenários do componente nativo (`ShimTests.exe`)

| Cenário | O que cobre |
| --- | --- |
| `main` | Todos os sete hooks do kernel32 instalados; o feed configurado. Correção do read-ahead em um arquivo de 300 KB (cada byte lido um de cada vez com uma consulta de posição e de tamanho antes de cada leitura, depois 4.000 seeks aleatórios a partir do início, da posição atual e do fim com tamanhos de leitura aleatórios, fim de arquivo, uma posição negativa e uma posição além de 4 GiB, cada um conferido com os bytes e posições esperados) e um arquivo com nome não ASCII; write-behind com os seus descarregamentos; abertura somente leitura e a reabertura quando o jogo grava; relatórios de tile; a liberação de um arquivo mapeado que o jogo substitui; esvaziamento com handles abertos; os relatórios de uso (apenas o primeiro uso, apenas a primeira gravação, aberturas de tile como `T`, a versão do OMSI como uma linha `V`); e as estatísticas (leituras atendidas do buffer e leituras de sistema abaixo de um vigésimo das da aplicação, gravações reais de write-behind abaixo de 20, nenhuma falha, uma abertura somente leitura e uma reabertura, uma solicitação de liberação, nenhuma linha descartada). |
| `missing-hooks` | Um hook de posição recusado (`SetFilePointer`): nada é interceptado, as leituras permanecem exatas, uma gravação em um candidato somente leitura é aberta como pedido, sem feed. |
| `features-off` | Os três recursos desligados por configuração: restam apenas os relatórios de uso; as leituras da aplicação são iguais às leituras de sistema. |
| `protocol1` | Uma tabela de bridge sem `get_value` (protocolo 1): o componente se recusa a iniciar. |
| `core-refused` | `CreateFileW` recusado: a inicialização falha, e `stop` ainda emite as estatísticas. |

<a id="what-the-tests-do-not-do"></a>
## O que os testes não fazem

- Eles nunca iniciam o OMSI, o plugin do OmsiLaunch nem a bridge real: a bridge é falsa nos testes nativos, e o programa de sessão conversa com um cliente OMSI falso.
- Eles não medem velocidade. O ganho medido dos mecanismos é o resultado do estudo de otimização de carregamento de 30/09/2026 no mapa RMG Leste, que o README do repositório declara válido para o módulo ([visão geral](../getting-started/overview.md#what-you-get)).
- Eles não exercitam um mapa muito grande, uma máquina com pouca memória nem programas concorrentes que tocam os arquivos mantidos.
- Eles não testam as duas janelas (a janela de primeira abertura e o monitor).

<a id="documentation-checks"></a>
## Verificações da documentação

`tools\Check-Docs.ps1` verifica estas páginas: todo link relativo e âncora resolve, toda configuração e flag de `prefetch\module.template.json` aparece em [configurações](../reference/settings.md) com o seu padrão e limites, e toda página está listada no [índice](../README.md). Execute-o a partir da raiz do repositório depois de editar a documentação:

```text
powershell -File tools\Check-Docs.ps1
```

<a id="next"></a>
## Próximos passos

[Limitações conhecidas](../reference/known-limitations.md) · [Visão geral](../getting-started/overview.md) · [Índice](../README.md)
