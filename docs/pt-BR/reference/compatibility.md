# Compatibilidade

<!-- l10n: source=reference/compatibility.md source-sha256=8030c8d581f8cd70c8072dfdad6b79fef036396752ae2a6b84246d6ca7a126f7 -->
> Tradução da [página original em inglês](../../reference/compatibility.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Esta página diz com o que o Omsi Prefetch `1.0.0-beta.1` funciona: versões do OmsiLaunch, Windows, os runtimes .NET, builds do OMSI 2, mapas e veículos, outros plugins e módulos, e os idiomas das suas janelas. Fontes: `prefetch\module.template.json`, os arquivos de projeto em `prefetch\` e o código nativo e do programa de sessão.

Páginas relacionadas: [instalação](../getting-started/installation.md), [erros e degradação](errors-and-degradation.md), [convivência](../guides/coexisting.md), [limitações conhecidas](known-limitations.md).

<a id="at-a-glance"></a>
## Em resumo

| Assunto | Compatibilidade |
| --- | --- |
| OmsiLaunch | `0.1.0-beta4` ou posterior; qualquer build que ofereça o protocolo de bridge 2. O manifesto declara `bridge_protocol` `{ "min": 2, "max": 2 }`, ativação `default`, capacidades obrigatórias `bridge.events` e `bridge.hooks.iat`, política de falha `degrade`. |
| Windows | Windows 10 ou posterior, 64 bits (a plataforma que o OmsiLaunch suporta). O programa de sessão é x64. |
| .NET | x64: .NET 6 Desktop Runtime (o programa de sessão é um arquivo único `net6.0-windows` dependente do framework; o controlador do OmsiLaunch precisa do mesmo runtime). x86: .NET 6 Runtime para o componente gerenciado dentro do OMSI (`net6.0`, AnyCPU; o plugin do OmsiLaunch já precisa dele). |
| Nativo | `prefetch-native.dll` é uma DLL C++ de 32 bits (x86) compilada com o cabeçalho de módulo nativo do OmsiLaunch (`omsilaunch_module_native_v1.h`), carregada no OMSI pela bridge. |
| OMSI 2 | Independente de build; veja abaixo. |
| Memória | Até 12 GB mantidos, qualquer que seja a quantidade de RAM. Veja [escolhendo configurações pelo tamanho da memória](../guides/choosing-settings-by-memory.md). |
| Outros plugins | Conflita com `OmsiLoadingPoC.dll` (declarado no manifesto). Veja abaixo. |
| Idioma de exibição das janelas do módulo | Português quando o idioma de exibição do Windows é o português; inglês nos demais casos. |
| Licença | GNU General Public License, versão 3. |

<a id="omsi-2-builds"></a>
## Builds do OMSI 2

O módulo **não carrega nenhum endereço, offset, padrão de bytes nem hash de build do OMSI**. O componente nativo pede à bridge do OmsiLaunch hooks nas importações do `Omsi.exe` pelo nome (`iat:kernel32.dll!CreateFileW`, `iat:d3dx9.dll!D3DXCreateTextureFromFileExW`, ...), e a bridge instala um hook somente sobre o valor que a importação deveria conter, recusando um ponto que outra pessoa alterou. Por causa disso:

- não há perfil por build a manter, e um build não é "suportado" ou "não suportado" pelo módulo; o que importa é se a bridge consegue fazer hook em cada importação no seu `Omsi.exe`, o que é decidido quando o OMSI inicia e informado no [evento `hooks`](events-and-notices.md#the-hooks-event);
- o módulo exige o recurso de bridge `bridge.hooks.iat` (e `bridge.events`); o OmsiLaunch os verifica quando planeja a sessão;
- as funções em que os recursos nativos fazem hook são importações do kernel32 e do d3dx9 do `Omsi.exe`; em um build em que uma importação está ausente ou já foi alterada, apenas esse hook é recusado e os recursos que dependem dele ficam desligados.

O programa de sessão lê os mapas e veículos de uma instalação do modo como o OMSI os armazena: foi verificado na instalação limpa (Grundorf e Berlin-Spandau para o layout de `[entrypoints]` do `global.cfg`) e, para o estudo, no mapa RMG Leste.

<a id="maps-vehicles-and-add-ons"></a>
## Mapas, veículos e add-ons

- Qualquer mapa com um `global.cfg` que o OMSI consiga ler: o módulo precisa da lista `[map]` (coordenadas dos tiles e nomes de arquivos) e, para os tiles iniciais, da lista `[entrypoints]`. Um mapa sem ponto de entrada começa pelo tile mais próximo do meio.
- Qualquer veículo que siga o layout de arquivos do OMSI (`.bus` ou `.ovh` com uma seção `[model]`; `.cfg`, `.o3d`, texturas, repinturas por `[ctc]`/`.cti`, sons, `.hof`).
- Add-ons são apenas lidos. O módulo não ativa, não desativa nem altera nenhum conteúdo, licença ou direito de uso, e nunca grava em uma pasta de add-on.
- Pastas que são junctions ou links simbólicos não são percorridas quando o módulo lista uma pasta (os arquivos do mapa inteiro, a pasta de um veículo); um arquivo citado por caminho ainda é resolvido se estiver dentro da pasta do OMSI.

<a id="other-plugins-and-modules"></a>
## Outros plugins e módulos

| Caso | Resultado |
| --- | --- |
| `OmsiLoadingPoC.dll` listado em um `plugins\*.opl` | `OL_E_MODULE_CONFLICT` para um pedido explícito, `OL_W_MODULE_NOT_LOADED` nos demais casos; o módulo não é carregado ([convivência](../guides/coexisting.md)). |
| Outro plugin que faz hook nas mesmas importações do `Omsi.exe` | A bridge recusa um hook sobre um ponto que alguém fora da bridge alterou (resultado `4`); o módulo degrada ([erros e degradação](errors-and-degradation.md)). |
| Outros módulos do OmsiLaunch | Compartilham o registro de hooks da bridge; hooks em um mesmo ponto se empilham e são removidos do último para o primeiro. A memória é compartilhada: todo módulo que mantém memória conta contra a mesma RAM. |
| Perfis de sessão do OmsiLaunch | Um preset pode carregar as configurações do módulo ou desligá-lo ([perfis de sessão](../guides/session-profiles.md)). |
| Plugins que leem ou gravam arquivos que o OMSI usa | Veem os mesmos arquivos; o módulo nunca mantém um arquivo que o jogo reescreve. |

<a id="platform-notes"></a>
## Observações de plataforma

- O módulo não suporta Windows 7, 8.x nem XP, nem ARM64, porque o OmsiLaunch não suporta.
- Antivírus que examinam toda abertura de arquivo se aplicam às aberturas do módulo como às de qualquer programa; o módulo não muda isso.
- O módulo precisa que o OmsiLaunch inicie o OMSI: ele não faz nada para um OMSI iniciado à mão ou por outro launcher.

<a id="next"></a>
## Próximos passos

[Limitações conhecidas](known-limitations.md) · [FAQ](faq.md) · [Erros e degradação](errors-and-degradation.md)
