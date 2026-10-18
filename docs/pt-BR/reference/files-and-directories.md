# Arquivos e diretórios

<!-- l10n: source=reference/files-and-directories.md source-sha256=7428ae4e0e8081b90d55dc78833ce54888b8d4eaee91f47a78070a49de1db4fa -->
> Tradução da [página original em inglês](../../reference/files-and-directories.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Esta página lista tudo o que o Omsi Prefetch lê e grava: os arquivos do próprio módulo, os arquivos que ele lê da instalação do OMSI, os dados que guarda e os formatos deles, os outros recursos que usa (um pipe nomeado, memória, threads) e os limites que se aplicam a cada um. Tudo foi extraído do código do programa de sessão (`prefetch\preloader\`) e do componente nativo (`prefetch\native\`).

Páginas relacionadas: [segurança](../concepts/safety.md), [dados aprendidos](../concepts/learned-data.md), [formato do log](log-format.md), [limpando dados](../guides/clearing-data.md), [configurações](settings.md#data).

<a id="the-modules-files"></a>
## Os arquivos do módulo

```text
<OMSI root>\plugins\OmsiLaunch.Modules\omsi-prefetch\
  module.json              manifest, with the SHA-256 of each component
  Prefetch.Module.dll      managed component (inside OMSI)
  prefetch-native.dll      native component, x86 (inside OMSI)
  Prefetch.Preloader.exe   session program, x64 (before and next to OMSI)
  NOTICE.txt  GPL-3.0.txt  LGPL-3.0.txt
```

O módulo não lê nada desta pasta a não ser pelo carregador do OmsiLaunch, e não grava nada nela.

<a id="what-it-reads"></a>
## O que ele lê

Somente dentro da pasta do OMSI; um caminho que sai dela nunca é aberto. Todas as aberturas são para leitura com compartilhamento de leitura, escrita e exclusão, e, para os arquivos que são mantidos, o handle é fechado depois do mapeamento.

| Arquivos | Por quê | Limite de leitura |
| --- | --- | --- |
| `maps\<map>\global.cfg` | Tiles, pontos de entrada, os arquivos que o mapa cita (texturas do solo, sistema de dinheiro, pacote de bilhetes), o nome amigável. | 64 MiB |
| `maps\<map>\tile_X_Y.map` | Quais objetos e splines cada tile posiciona (linha a linha; referências `.sco` e `.sli`). | 64 MiB por arquivo de tile |
| `maps\<map>\ailists.cfg`, `parklist_p.txt`, `humans.txt`, `drivers.txt`, `unsched_vehgroups.txt` | Os veículos de IA, pessoas e objetos do mapa. | 8 MiB cada |
| A pasta do mapa, recursivamente | Os arquivos do mapa inteiro a manter. | no máximo 50.000 arquivos listados |
| Objetos de cenário, splines, arquivos de veículos (`.sco`, `.sli`, `.bus`, `.ovh`, `.hum`, `.cfg`, `.cti`, `.zug`) | Seguidos pelos arquivos que citam. | 8 MiB cada |
| Modelos `.o3d` | Examinados em busca dos nomes das texturas que contêm. | 256 MiB cada |
| Modelos, texturas, scripts, sons, `.hof` e o resto que o fechamento alcança | Mantidos em memória, ou abertos antes. | Mantidos inteiros; um arquivo que não cabe no orçamento é pulado. |
| Pastas sob `Vehicles\` | Para encontrar veículos válidos (`.bus`/`.ovh` com uma seção `[model]`) para `user_vehicles` e `pre_open`. | no máximo 200.000 arquivos listados por pasta (20.000 para a pré-abertura no primeiro uso) |
| Os seus próprios arquivos de dados (abaixo) | Listas aprendida e de escrita, índice de tiles. | 64 MiB (listas), 512 MiB (índice de tiles) |

Os arquivos de texto são lidos como o OMSI os grava: UTF-16 com marca de ordem de bytes, UTF-8 com marca de ordem de bytes ou Windows-1252. Pontos de reanálise (junctions, links simbólicos) não são seguidos quando uma pasta é listada. Uma referência com mais de 1.024 caracteres, ou com caracteres de caminho inválidos, é ignorada. Uma contagem de mais de 4.096 pontos de entrada no `global.cfg` é ignorada.

Ele não lê `options.cfg`, as suas situações salvas, os seus documentos nem nada fora da pasta do OMSI. Os arquivos de que a transação de sessão do OmsiLaunch é dona nunca são mantidos ([segurança](../concepts/safety.md#what-is-never-held)).

<a id="what-it-writes"></a>
## O que ele grava

Somente abaixo de `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\` (o diretório de dados do módulo, criado pelo OmsiLaunch):

```text
.omsilaunch\module-data\omsi-prefetch\
  maps\<folder>-<hash>\
    learned.txt    files the game used on the map, first-use order
    written.txt    files the game wrote: never held
    tiles.idx      tile index, with the map's fingerprint
  logs\
    preloader-<yyyyMMdd-HHmmss>.log   the three most recent session logs
```

Ele não grava CSV, traços de amostra nem cópias de arquivos do OMSI. Não faz parte da transação de sessão: os dados permanecem depois da sessão. Um salvamento grava `<file>.tmp` e depois substitui o arquivo antigo por ele.

<a id="learnedtxt-and-writtentxt"></a>
### `learned.txt` e `written.txt`

UTF-8 sem marca de ordem de bytes, quebras de linha LF. A primeira linha é um comentário (`# omsilaunch prefetch: files the game used on this map, in first-use order` e `# omsilaunch prefetch: files the game wrote: never held in memory`); cada uma das outras linhas é um caminho relativo à pasta do OMSI. Linhas que começam com `#` e linhas vazias são ignoradas na leitura; um caminho que resolve para fora da pasta do OMSI é ignorado.

O `learned.txt` é limitado a 200.000 entradas ao ser gravado. O `written.txt` não tem limite próprio, mas só contém arquivos que o jogo gravou.

<a id="tilesidx"></a>
### `tiles.idx`

UTF-8 sem marca de ordem de bytes, quebras de linha LF:

```text
# omsilaunch prefetch tile index v1
fingerprint <64 hex digits>
F<TAB><id><TAB><path relative to the OMSI folder>
T<TAB><tile index><TAB><id>,<id>,...
```

As linhas `F` vêm primeiro (os ids são 0, 1, 2, ... em ordem, um por arquivo distinto), depois uma linha `T` por tile (a posição do tile na lista `[map]` do `global.cfg` e os ids dos arquivos de que ele precisa, na ordem de carregamento). A impressão digital (fingerprint) é o SHA-256 do nome, tamanho e data da última gravação do `global.cfg` e de todo arquivo de tile. O arquivo só é usado quando o seu cabeçalho e a sua impressão digital correspondem.

<a id="folder-names-of-a-map"></a>
### Nomes de pasta de um mapa

`maps\<folder>-<hash>`: `<folder>` é o nome da pasta do mapa com todo caractere diferente de letra, dígito, `-`, `_` ou `.` substituído por `_`, cortado em 40 caracteres; `<hash>` são os primeiros oito dígitos hexadecimais do SHA-256 de `maps\<folder>\global.cfg` em minúsculas.

<a id="logs"></a>
### Logs

`logs\preloader-<yyyyMMdd-HHmmss>.log`, UTF-8 sem marca de ordem de bytes. Veja [formato do log](log-format.md).

<a id="bounds-on-disk"></a>
## Limites em disco

| O quê | Limite |
| --- | --- |
| Dados dos mapas (todos os mapas juntos) | `data_limit_mb`, padrão 256 MB (16 a 4096). Verificado quando uma sessão começa; os mapas usados há mais tempo são removidos primeiro, nunca o mapa atual. |
| Um arquivo de log | 1 MiB (1.048.576 bytes); depois disso, uma linha marcadora e o restante é descartado. |
| Arquivos de log mantidos | 3 (quando um novo é aberto, todos os mais antigos, exceto os dois mais recentes, são apagados; os nomes ordenam por data). |
| `learned.txt` | 200.000 entradas |
| Tamanho típico | Alguns megabytes por mapa |

Tudo é removido com `OmsiLaunch.exe /clear-module-data:omsi-prefetch`.

<a id="other-resources"></a>
## Outros recursos

| Recurso | Detalhe |
| --- | --- |
| Pipe nomeado | `\\.\pipe\OmsiLaunch.Module.<session id without dashes>.omsi-prefetch`, criado pelo programa de sessão, somente para o usuário atual, um cliente, apenas de entrada (do OMSI para o programa). Existe somente enquanto a sessão roda. |
| Memória | Os arquivos mantidos, mapeados somente para leitura no `Prefetch.Preloader.exe`; os buffers do componente nativo no OMSI (64 KiB por handle de read-ahead, 256 KiB por handle de write-behind; até 4096 handles rastreados) e a sua fila de feed (até 400.000 linhas e um conjunto de até 500.000 caminhos). |
| Threads | No programa de sessão: a thread da sessão, o leitor do controlador, o leitor do pipe e o seu worker e, durante a sessão, o levantamento, a previsão, os veículos do usuário, a pré-abertura, a guarda, mais até 16 threads de pré-abertura e até 16 threads de primeiro uso por pasta de veículo. No OMSI: uma thread de feed. |
| Hooks | `iat:kernel32.dll!CreateFileW`, `ReadFile`, `CloseHandle`, `WriteFile`, `SetFilePointer`, `GetFileSize`, `SetEndOfFile`; `iat:d3dx9.dll!D3DXCreateTextureFromFileExW`, `D3DXCreateTextureFromFileExA`, `D3DXCreateTextureFromFileW`, `D3DXLoadMeshFromXA`. Pertencentes a `module:omsi-prefetch` no registro da bridge e removidos quando o módulo para. |
| Rede, registro, outros processos | Nenhum. |

<a id="next"></a>
## Próximos passos

[Formato do log](log-format.md) · [Limpando dados](../guides/clearing-data.md) · [Dados aprendidos](../concepts/learned-data.md)
