# Formato do log

<!-- l10n: source=reference/log-format.md source-sha256=3f80121385e2cc74096684a8ae492a5db497938aeb7faa3289fa7e8845c8030b -->
> Tradução da [página original em inglês](../../reference/log-format.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

O programa de sessão escreve um log por sessão. Ele é feito para uso, não para depuração: uma sessão escreve algumas dezenas de linhas que dizem o que foi mantido, quanto tempo levou e o que deu errado. Esta página apresenta o arquivo, os seus limites, o formato de uma linha, um exemplo e cada mensagem que o programa escreve, para que você possa ler um log (veja [diagnosticando lentidão](../guides/diagnosing-slowness.md)). As mensagens foram extraídas do código e ficam em inglês, qualquer que seja o idioma de exibição.

Páginas relacionadas: [diagnosticando lentidão](../guides/diagnosing-slowness.md), [arquivos e diretórios](files-and-directories.md), [configurações: `debug_log`](settings.md#debug_log), [eventos](events-and-notices.md), [programa de sessão](../concepts/session-program.md).

<a id="the-file"></a>
## O arquivo

| | |
| --- | --- |
| Local | `<OMSI root>\.omsilaunch\module-data\omsi-prefetch\logs\` |
| Nome | `preloader-<yyyyMMdd-HHmmss>.log`, hora local do início do programa |
| Codificação | UTF-8 sem marca de ordem de bytes; uma linha por entrada; descarregado após cada linha |
| Tamanho | No máximo 1 MiB por arquivo. Depois disso, uma linha marcadora (`-- log limit reached (1024 KiB): further lines are dropped`) e nenhuma linha a mais |
| Mantidos | Os três arquivos mais recentes; os mais antigos são apagados quando um novo é aberto |
| Detalhamento | O log normal, mais linhas por arquivo e por veículo com `debug_log` (flag `/prefetch-debug`) |
| Comprimento da linha | Uma mensagem com mais de 2000 caracteres é cortada e termina com `...` |

Se a pasta não puder ser criada ou gravada, o programa roda sem log.

<a id="line-shape"></a>
## Formato da linha

```text
HH:mm:ss.fff <message>
```

A hora é a hora local do relógio; não há data na linha (o nome do arquivo a tem). As mensagens são texto livre nas formas abaixo; não as interprete com um programa que precise sobreviver a atualizações. Os tamanhos são escritos como `<number> MB` com uma casa decimal.

<a id="a-sample"></a>
## Um exemplo

```text
14:02:11.380 session 8f3c0a52-1d0e-4e1b-9a2f-6a3f1c7d9b10, module 1.0.0-beta.1, OmsiLaunch 0.1.0-beta4
14:02:11.384 budget 4832.0 MB (setting auto), free 11000 MB of 16072 MB
14:02:11.402 map maps\Grundorf\global.cfg (Grundorf): 124 tiles, 6 entrypoints; learned 3120 files; tile index 124 tiles from an earlier session; start 10,12 11,12
14:02:11.931 prepare: learned: 2874 files, 612.3 MB
14:02:12.020 prepare: map: 41 files, 18.7 MB
14:02:12.455 prepare: start tiles (9 tiles): 730 files, 311.5 MB
14:02:12.470 vehicles: map AI 58 citations, 12 folders, 9 objects/people (904.2 MB)
14:02:13.912 prepare: tiles around the start (121 tiles): 1980 files, 903.0 MB
14:02:13.915 prepared in 2.5 s: 6900 files, 2749.5 MB of 4832.0 MB
14:02:20.118 controller: OMSI started, pid 12044
14:02:23.540 feed: OMSI connected
14:02:23.541 OMSI 2.3.004
14:08:01.004 controller: stop
14:08:02.250 end: 6977 files held (2755.0 MB), used 3318 of them (1203.4 MB), 46 used outside memory (12.9 MB); feed 4221 lines
14:08:02.260 exit
```

(Os números são ilustrativos.)

<a id="messages"></a>
## Mensagens

<a id="start-and-map"></a>
### Início e mapa

| Mensagem | Significado |
| --- | --- |
| `session <id>, module <version>, OmsiLaunch <version>` | Primeira linha da sessão. |
| `budget <size> (setting <auto or MB>), free <MB> MB of <MB> MB` | O orçamento de memória e a memória física livre e total no início. |
| `the working set minimum was not raised: held pages may be trimmed under memory pressure` | O Windows recusou o pedido para manter residentes as páginas mantidas ([memória e orçamento](../concepts/memory-and-budget.md)). |
| `the session channel cannot be served: <reason>` | O pipe da sessão não pôde ser criado. O componente nativo do OMSI não consegue informar; o preloader ainda mantém arquivos. |
| `no map in this session: nothing to prepare` | A sessão não tem mapa. Nada é mantido nem aprendido. |
| `the map <identity> cannot be read: nothing to prepare` | O `global.cfg` está ausente, não é um `global.cfg` ou é ilegível. |
| `data: <size> of the least recently used maps removed to stay under <MB> MB` | Os dados de mapas antigos foram removidos para respeitar `data_limit_mb`. |
| `map <identity> (<name>): <n> tiles, <n> entrypoints; learned <n> files; tile index <n> tiles from an earlier session \| new; start <x,y ...>[; first open]` | O mapa, o seu tamanho, o que se sabe sobre ele, os tiles iniciais. `first open` marca um mapa sobre o qual o módulo não sabe nada. |
| `no memory to hold anything: preparation skipped` | O orçamento é zero. |

<a id="preparation"></a>
### Preparação

| Mensagem | Significado |
| --- | --- |
| `prepare: <step>: <n> files, <size>` | O que uma etapa manteve. As etapas são `learned`, `map`, `start tiles (<n> tiles)` e `tiles around the start (<n> tiles)`. |
| `vehicles: map AI <n> citations, <n> folders, <n> objects/people (<size>)` | Os veículos de IA do mapa encontrados nas suas listas, com o tamanho de tudo de que precisariam. |
| `prepared in <s> s: <n> files, <size> of <size>[ (time limit reached; the rest continues during the session) \| (budget reached) \| (free memory low)]` | O fim da preparação e, se parou antes, por que parou. |
| `prepare failed: <exception>` | A preparação lançou uma exceção. O módulo envia `OL_E_MODULE_OPERATION_FAILED` ao OmsiLaunch e mesmo assim informa que está pronto. |

<a id="during-the-session"></a>
### Durante a sessão

| Mensagem | Significado |
| --- | --- |
| `controller: OMSI started, pid <n>` | O OmsiLaunch informou o processo do OMSI. |
| `feed: OMSI connected` / `feed: OMSI disconnected` | O componente nativo se conectou ao pipe ou o deixou. |
| `OMSI <version>` | A versão do OMSI informada pela bridge (protocolo 3). O componente nativo a envia quando se conecta, então aparece uma vez, logo após `feed: OMSI connected`; o evento `armed` também a traz. |
| `feed: refused a client that is not OMSI's process` | Outro processo tentou usar o pipe. |
| `feed: line not handled: <type>: <message>` | Um relatório de uso não pôde ser aplicado. |
| `feed: the pipe cannot be created again: <reason>` | Depois de uma desconexão, o pipe não pôde ser recriado; o feed termina. |
| `survey: <n> tiles indexed` | O índice de tiles em segundo plano está completo. |
| `vehicles: <n> user vehicles (<size>), <n> folders without a valid vehicle` | Os veículos do usuário encontrados para `user_vehicles`. |
| `vehicles: list entry '<name>' is a vehicle of the map's AI \| not a valid vehicle folder` | Uma entrada de `user_vehicle_list` ou `pre_open_list` foi pulada. |
| `pre-open: <n> files: <w> warm, <c> cold (<size>), <f> failed in <s> s[; cold data limit reached \| ; free memory low]` | O resultado da pré-abertura planejada ([pré-abertura](../concepts/pre-open.md)). |
| `guard: free memory <MB> MB: loading resumes` | A memória livre se recuperou após uma fase de pouca memória. |
| `<activity>: stopped by an unexpected error: <exception>` | Uma atividade em segundo plano (`survey`, `prediction`, `vehicles`, `pre-open`, `guard`) terminou com um erro; as outras continuam. |
| `session failed: <exception>` | O laço da sessão lançou uma exceção. |

<a id="end"></a>
### Fim

| Mensagem | Significado |
| --- | --- |
| `controller: stop` | O OmsiLaunch pediu ao programa que parasse. |
| `save: <reason>` | Um salvamento dos dados aprendidos falhou (disco cheio, sem permissão); a sessão continua. |
| `end: <n> files held (<size>), used <n> of them (<size>), <n> used outside memory (<size>); feed <n> lines` | Resultado da sessão: arquivos ainda mantidos no fim, quantos deles o OMSI usou, quantos arquivos o OMSI usou que a memória não mantinha e quantas linhas o componente nativo enviou. |
| `shutdown failed: <exception>` | O encerramento lançou uma exceção. |
| `exit` | O programa terminou. |

<a id="only-with-debug_log"></a>
### Somente com `debug_log`

| Mensagem | Significado |
| --- | --- |
| `vehicles: held <folder> (<scope>): <size>` | Um veículo foi mantido. |
| `vehicles: over the budget: <folder> needs <size>` | Um veículo foi pulado: não cabe no orçamento. |
| `vehicles: over the vehicle budget: <folder> needs <size>` | Um veículo foi pulado: não cabe em `vehicle_budget_mb`. |
| `released at OMSI's request: <path>` | O OMSI não conseguiu substituir um arquivo mantido e o arquivo foi liberado. |
| `guard: free memory <MB> MB: released <size>` | A guarda liberou arquivos não usados. |
| `first use of <folder>: <n> files opened ahead: <result>` | A pasta de um veículo foi aberta antes no seu primeiro uso. |
| `first use of <folder>: <reason>` | Essa tentativa falhou. |

<a id="reading-it"></a>
## Lendo o log

- `end:` é o resumo. "Used outside memory" são arquivos que o OMSI usou e que o módulo não mantinha quando o OMSI os abriu: foram lidos do disco. Muitos deles significam um orçamento pequeno demais ou um mapa que ainda não foi aprendido.
- `prepared in ... (budget reached)` significa que o orçamento foi gasto antes de a lista terminar; `(free memory low)` significa que o piso de carregamento o interrompeu ([memória e orçamento](../concepts/memory-and-budget.md)); `(time limit reached ...)` significa que se passaram cinco minutos.
- Um log que termina sem `exit` significa que o programa foi encerrado abruptamente (OmsiLaunch morto, a máquina perdeu energia).

<a id="next"></a>
## Próximos passos

[Eventos](events-and-notices.md) · [Diagnosticando lentidão](../guides/diagnosing-slowness.md) · [Erros e degradação](errors-and-degradation.md)
