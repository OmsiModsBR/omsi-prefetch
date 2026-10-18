# Sobrescritas por sessão na linha de comando

<!-- l10n: source=guides/command-line-overrides.md source-sha256=e4a1b9db496f3a39e598efcdbe1ac2301129fb3fd03429548e4bebaac9c7d6b7 -->
> Tradução da [página original em inglês](../../guides/command-line-overrides.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Você pode mudar qualquer configuração do Omsi Prefetch para uma sessão sem mexer no `modules.ini` nem em um perfil. Esta página lista as flags do próprio módulo, a forma genérica `/module-setting` que alcança todas as configurações, como os valores são escritos e entre aspas, as regras de precedência, como conferir o resultado com `/plan` e os erros que você pode encontrar. As configurações em si estão descritas na [referência de configurações](../reference/settings.md).

Páginas relacionadas: [referência de configurações](../reference/settings.md), [perfis de sessão](session-profiles.md), [desligando](turning-off.md), [escolhendo configurações pelo tamanho da memória](choosing-settings-by-memory.md).

<a id="the-modules-own-flags"></a>
## As flags do próprio módulo

Sete configurações têm uma flag. Uma flag é escrita `/<flag>:<value>`; para uma configuração booleana, a flag sozinha significa `true`.

| Flag | Configuração | Exemplo |
| --- | --- | --- |
| `/prefetch-ram:<MB>` | `ram_budget_mb` (também `auto`) | `/prefetch-ram:6144` |
| `/prefetch-reserve:<MB>` | `reserve_mb` | `/prefetch-reserve:6144` |
| `/prefetch-stop-below:<MB>` | `stop_below_free_mb` (também `auto`) | `/prefetch-stop-below:3072` |
| `/prefetch-vehicle-ram:<MB>` | `vehicle_budget_mb` | `/prefetch-vehicle-ram:1024` |
| `/prefetch-monitor` | `monitor` | `/prefetch-monitor` ou `/prefetch-monitor:false` |
| `/prefetch-pre-open:<mode>` | `pre_open` (`none`, `learned`, `all`, `list`) | `/prefetch-pre-open:none` |
| `/prefetch-debug` | `debug_log` | `/prefetch-debug` |

Uma flag de módulo é resolvida quando o OmsiLaunch planeja uma sessão, a partir das flags dos módulos instalados (`/list:RuntimeModules` as mostra). É o mesmo que `/module-setting:omsi-prefetch.<key>=<value>`. Uma flag que nenhum módulo instalado declara, ou uma usada fora de um launch, é `Unknown argument` (saída `2`); uma flag declarada por dois módulos é `OL_E_MODULE_SETTING_INVALID`. Uma flag de módulo nunca substitui uma flag interna do OmsiLaunch.

<a id="every-other-setting"></a>
## Qualquer outra configuração

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /module-setting:omsi-prefetch.tile_radius=8
```

`/module-setting:<module id>.<key>=<value>` pode ser repetida, uma vez por configuração. A forma é exata: sem o id do módulo e um ponto antes da chave, o OmsiLaunch responde `/module-setting requires <module-id>.<key>=<value>` (saída `2`).

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-ram:6144 /module-setting:omsi-prefetch.ai_vehicles=load /module-setting:omsi-prefetch.tile_radius=8 /prefetch-monitor
```

<a id="writing-values"></a>
## Escrevendo valores

- **Booleanos**: `true`/`false`, `yes`/`no`, `on`/`off`, `1`/`0`.
- **Inteiros**: dígitos simples, dentro dos limites da [tabela de configurações](../reference/settings.md#summary). `auto` é aceito por `ram_budget_mb` e `stop_below_free_mb`.
- **Escolhas**: comparadas sem diferenciar maiúsculas de minúsculas.
- **Listas** (`user_vehicle_list`, `pre_open_list`, `read_only_folders`): entradas separadas por `;`, na ordem que você quiser. Um ponto e vírgula encerra um comando no PowerShell, então coloque o argumento entre aspas:

  ```powershell
  OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 '/module-setting:omsi-prefetch.user_vehicles=list' '/module-setting:omsi-prefetch.user_vehicle_list=Citaro_G;MAN_Lions_City'
  ```

  No `cmd.exe`, coloque aspas duplas em volta do argumento inteiro: `"/module-setting:omsi-prefetch.user_vehicle_list=Citaro_G;MAN_Lions_City"`. Não termine um argumento entre aspas com uma barra invertida (o Windows transforma `\"` em uma aspa); defina `read_only_folders` no `modules.ini` ou em um preset.

<a id="precedence"></a>
## Precedência

Para cada configuração: a linha de comando (ou um preset), depois o `.omsilaunch\modules.ini`, depois o padrão. Detalhes:

- A linha de comando vence o `modules.ini` e um arquivo `/spec`.
- Uma chave que o preset de perfil de sessão selecionado define não pode também ser dada na linha de comando: `OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT` (saída `2`). Chaves que o preset não define podem ([perfis de sessão](session-profiles.md)).
- Configurações de um módulo que a sessão não carrega (por exemplo, junto com `/no-module:omsi-prefetch`) são reportadas com `OL_W_MODULE_SETTING_IGNORED` e não fazem nada.

<a id="checking-before-you-play"></a>
## Conferindo antes de jogar

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /prefetch-ram:6144 /plan --json
```

O `/plan` não inicia nada. Na saída, procure em `Diagnostics` por `module.planned` para `omsi-prefetch` e pela ausência de `OL_E_MODULE_SETTING_INVALID`. O plano lista o valor efetivo de cada configuração e de onde ele veio (`request`, `settings-file` ou `default`).

<a id="errors"></a>
## Erros

| Você vê | Causa |
| --- | --- |
| `Unknown argument: /prefetch-...` | O módulo não está instalado, ou a flag está escrita errada. |
| `/module-setting requires <module-id>.<key>=<value>` | A forma está errada. |
| `OL_E_MODULE_SETTING_INVALID` | Um valor fora dos limites, uma escolha que não existe ou uma chave que o módulo não declara. A mensagem nomeia a configuração e o motivo. |
| `OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT` | O preset já define essa chave. |

<a id="next"></a>
## Próximos passos

[Perfis de sessão](session-profiles.md) · [Desligando](turning-off.md) · [Referência de configurações](../reference/settings.md)
