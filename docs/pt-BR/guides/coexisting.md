# Convivendo com outros módulos e plugins

<!-- l10n: source=guides/coexisting.md source-sha256=00764d00a15a1892ca59e4ebb6c4a98a961a15327bad3164b2bfd840e5a45bd0 -->
> Tradução da [página original em inglês](../../guides/coexisting.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

O Omsi Prefetch faz hook em funções de arquivo do `Omsi.exe`, mantém memória e lê muitos arquivos. Esta página diz o que isso significa ao lado de plugins do OMSI, de outros módulos do OmsiLaunch, de outros programas e de ferramentas que tocam nos seus arquivos do OMSI, e como resolver o único conflito declarado. As regras do registro de hooks, dos conflitos e da ativação são as da referência de Runtime Modules do OmsiLaunch.

Páginas relacionadas: [compatibilidade](../reference/compatibility.md), [erros e degradação](../reference/errors-and-degradation.md), [segurança](../concepts/safety.md), [desligando](turning-off.md).

<a id="the-declared-conflict-omsiloadingpocdll"></a>
## O conflito declarado: `OmsiLoadingPoC.dll`

O manifesto declara `conflicts.plugins` = `OmsiLoadingPoC.dll`, o plugin de prova de conceito que este módulo substitui. Ele instala hooks nas mesmas funções de arquivo do `Omsi.exe`, portanto os dois não devem rodar juntos.

- No planejamento, o OmsiLaunch lê a seção `[dll]` de todo `plugins\*.opl`. Se algum carrega `OmsiLoadingPoC.dll`, o módulo é recusado: `OL_E_MODULE_CONFLICT` quando você o pediu explicitamente (`/module:omsi-prefetch`) e o aviso `OL_W_MODULE_NOT_LOADED` nos demais casos (a sessão roda sem o módulo). O motivo nomeia o `.opl`.
- Dentro do OMSI, um módulo cuja DLL em conflito foi carregada mesmo assim não é carregado.
- O OmsiLaunch nunca altera os arquivos de outro plugin. Para usar o módulo, desligue o outro plugin você mesmo: remova a entrada dele do `.opl` ou mova o `.opl` para fora de `plugins\`.

<a id="other-plugins-that-hook-omsiexe-imports"></a>
## Outros plugins que fazem hook nas importações do `Omsi.exe`

Se outro plugin já alterou a mesma importação (`CreateFileW`, `ReadFile`, ...), a bridge recusa o hook do módulo sobre ela (resultado `4` no evento `hooks`), porque um ponto alterado fora da bridge nunca é sobrescrito.

- Se for um dos três hooks obrigatórios (`CreateFileW`, `ReadFile`, `CloseHandle`), a fase nativa do módulo falha e o módulo é liberado dentro do OMSI; a sessão continua.
- Se for outro hook, read-ahead, write-behind e abertura somente leitura ficam desligados, e o módulo continua aprendendo.

Plugins que apenas leem ou gravam os arquivos do OMSI normalmente não são afetados: eles veem os mesmos arquivos, e o módulo nunca mantém um arquivo que o jogo grava.

<a id="other-omsilaunch-modules"></a>
## Outros módulos do OmsiLaunch

- **Hooks** são registrados em um único registro com o seu dono. Hooks de módulos diferentes na mesma importação se empilham e são removidos do último para o primeiro. Um módulo que faz hook no dispositivo Direct3D (por exemplo, um HUD) faz hook em outros alvos e não interage com os hooks de arquivo deste módulo.
- **Memória.** Todo módulo que mantém memória conta contra a mesma RAM. Dois módulos que mantêm gigabytes cada um podem juntos deixar o sistema sem memória: o piso de carregamento e a guarda do módulo reagem à memória livre do sistema, não à parte de outro módulo. Se você usa outro, reduza `ram_budget_mb` ([escolhendo configurações pelo tamanho da memória](choosing-settings-by-memory.md)).
- **Programas de sessão.** Cada módulo com um programa de sessão roda em seu próprio processo e job; o OmsiLaunch inicia todos eles antes do OMSI e espera todos informarem que estão prontos. A preparação deste módulo, portanto, se soma à dos outros.
- **Configurações.** As configurações são por módulo; as configurações e flags de outro módulo não afetam este.

<a id="programs-that-touch-your-omsi-files-during-a-session"></a>
## Programas que tocam nos seus arquivos do OMSI durante uma sessão

Um arquivo mantido é mapeado, e o Windows não deixa um programa truncar nem substituir um arquivo mapeado. O OMSI é tratado (o módulo libera um arquivo quando o OMSI não consegue substituí-lo). Outro programa não:

- um editor que salva um mapa, um script ou um modelo no mesmo lugar enquanto uma sessão roda pode falhar com um erro sobre arquivo mapeado;
- uma ferramenta que reescreve texturas ou modelos no mesmo lugar tem o mesmo problema;
- um programa de backup ou de sincronização que apenas lê não tem problema.

Feche a sessão antes de editar, ou desligue o módulo para sessões de edição (`/no-module:omsi-prefetch`).

<a id="antivirus-file-system-filters-and-cloud-folders"></a>
## Antivírus, filtros de sistema de arquivos e pastas na nuvem

O módulo abre e mapeia muitos arquivos. Programas que inspecionam toda abertura se aplicam a ele como a qualquer programa; se isso deixa a preparação mais lenta é uma propriedade desses programas. Se você notar uma preparação muito mais longa, verifique `prepared in` no log. Uma pasta de sincronização na nuvem que coloca os arquivos do OMSI sob demanda pode tornar lenta a primeira leitura de cada arquivo; mantenha o OMSI em um disco local.

<a id="windows-file-cache-and-ram-disks"></a>
## Cache de arquivos do Windows e discos RAM

O módulo mantém arquivos na sua própria memória mapeada; o cache de arquivos do Windows é separado. Um disco RAM mantém os mesmos arquivos de novo: não se combina com o módulo e usa memória que o módulo poderia usar. Prefira dar essa memória ao `ram_budget_mb`.

<a id="when-something-conflicts"></a>
## Quando algo entra em conflito

1. Execute o plano com `--json` e leia os `Diagnostics` de `omsi-prefetch` ([diagnosticando lentidão](diagnosing-slowness.md)).
2. Leia o evento `hooks`: um hook diferente de `0` mostra qual importação está tomada.
3. Desligue o módulo por uma sessão (`/no-module:omsi-prefetch`) para confirmar que ele é a causa.
4. Desligue o outro plugin ou módulo, ou o mecanismo do módulo que entra em conflito (`read_ahead`, `write_behind`, `read_only_open`).

<a id="next"></a>
## Próximos passos

[Compatibilidade](../reference/compatibility.md) · [Erros e degradação](../reference/errors-and-degradation.md) · [Diagnosticando lentidão](diagnosing-slowness.md)
