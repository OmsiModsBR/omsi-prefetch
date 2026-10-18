# Usando com perfis de sessão predefinidos

<!-- l10n: source=guides/session-profiles.md source-sha256=85bfc7bb0fe4a62b7354ee43b42395f135c08b2932eab614148bd24ed4085cea -->
> Tradução da [página original em inglês](../../guides/session-profiles.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Um perfil de sessão do OmsiLaunch é um pacote YAML que fixa um mapa, um ponto de entrada e até 99 presets de configurações. Um preset pode carregar as configurações do Omsi Prefetch, de modo que cada mapa ou forma de jogar comece com os valores que lhe convêm: um orçamento pequeno para uma sessão leve, um grande com os seus próprios veículos para uma sessão longa, ou o módulo desligado. Esta página mostra como escrevê-los, como se combinam com a linha de comando e com o `modules.ini` e quais erros o OmsiLaunch rejeita. O formato do perfil em si está na referência de perfis de sessão do OmsiLaunch (`docs\reference\session-profiles.md` do OmsiLaunch).

Páginas relacionadas: [referência de configurações](../reference/settings.md), [sobrescritas pela linha de comando](command-line-overrides.md), [desligando](turning-off.md), [escolhendo configurações pelo tamanho da memória](choosing-settings-by-memory.md).

<a id="the-modules-block-of-a-preset"></a>
## O bloco `modules` de um preset

Um preset tem um mapeamento `modules` opcional, com uma entrada por id de módulo. Para este módulo a entrada é `omsi-prefetch`, com duas chaves:

| Chave | Significado |
| --- | --- |
| `enabled` | `true` pede o módulo para a sessão; `false` o mantém de fora (também quando ele entraria por padrão). Não definida: o módulo segue a sua ativação (`default`: ele entra). |
| `settings` | As configurações do módulo por chave, com os valores da [referência de configurações](../reference/settings.md). Conferidas com o manifesto quando a sessão é planejada. |

```yaml
modules:
  omsi-prefetch:
    settings:
      ram_budget_mb: 8192
      user_vehicles: list
      user_vehicle_list: "Bus1;Bus2"
      user_vehicle_scope: load
      monitor: false
```

Os valores podem ser escritos como números, booleanos ou strings YAML; o OmsiLaunch os converte para as configurações do módulo do mesmo modo que na linha de comando e recusa um valor fora dos limites do manifesto quando a sessão é planejada (`OL_E_MODULE_SETTING_INVALID`). Uma chave de configuração deve corresponder a `[a-z][a-z0-9_]{0,31}` e o id do módulo a `[a-z0-9][a-z0-9-]{1,47}`, ou o perfil é inválido (`OL_E_SESSION_PROFILE_INVALID`). Se o módulo não está instalado, `enabled: true` é reportado quando a sessão é planejada (`OL_E_MODULE_NOT_FOUND`) e configurações sozinhas são reportadas com `OL_W_MODULE_SETTING_IGNORED`.

<a id="a-complete-example"></a>
## Um exemplo completo

`<OMSI root>\.omsilaunch\session-profiles\grundorf-prefetch\profile.yaml`: dois presets do mesmo mapa, um leve e um completo. O mapa e o ponto de entrada são fixados pelo perfil; cada preset carrega as suas próprias configurações do módulo.

```yaml
schema: omsilaunch.session-profile/v1
id: grundorf-prefetch
name: Grundorf with Omsi Prefetch
author: You
version: "1.0"
compatibility:
  maps:
    - maps\Grundorf\global.cfg
new:
  map: maps\Grundorf\global.cfg
  entrypoint-index: 1
presets:
  - index: 1
    id: light
    name: Light (8 GB)
    modules:
      omsi-prefetch:
        settings:
          ram_budget_mb: 1536
          stop_below_free_mb: 1536
          tile_radius: 4
          ai_vehicles: essential
  - index: 2
    id: full
    name: Full (32 GB)
    modules:
      omsi-prefetch:
        settings:
          ram_budget_mb: 8192
          user_vehicles: list
          user_vehicle_list: "Citaro_G;MAN_Lions_City"
          user_vehicle_scope: load
          monitor: false
```

Inicie um com:

```text
OmsiLaunch.exe /predefined-profile:grundorf-prefetch /predefined-profile-index:2 /new
```

As chaves do perfil fora do bloco `modules` (`compatibility`, `new`, `settings`, ...) são definidas pela referência de perfis de sessão do OmsiLaunch; o exemplo apenas mostra onde o bloco vai. O `id` deve ser igual ao nome do diretório.

<a id="switching-the-module-off-in-a-preset"></a>
## Desligando o módulo em um preset

```yaml
    modules:
      omsi-prefetch:
        enabled: false
```

Isso mantém o módulo fora das sessões iniciadas com aquele preset, inclusive a sua ativação padrão. Veja [desligando](turning-off.md).

<a id="precedence-and-conflicts"></a>
## Precedência e conflitos

Para cada configuração, o valor efetivo é, em ordem: a requisição (um preset **ou** a linha de comando, veja abaixo), a seção `[omsi-prefetch]` de `.omsilaunch\modules.ini`, o padrão.

- Os `modules.omsi-prefetch.settings` de um preset contam como parte da requisição, portanto vencem o `modules.ini`.
- A linha de comando não pode também definir uma chave que o preset define: `/module-setting:omsi-prefetch.ram_budget_mb=...` ou `/prefetch-ram:...` com um preset que define `ram_budget_mb` é rejeitado com `OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT`. Uma chave que o preset não define ainda pode ser dada na linha de comando.
- `/module:omsi-prefetch` contra um preset com `enabled: false`, e `/no-module:omsi-prefetch` contra um preset com `enabled: true`, são rejeitados do mesmo modo.
- As configurações de módulo de um arquivo `/spec` são unidas às do preset.

<a id="tips"></a>
## Dicas

- Mantenha um preset por classe de hardware ou forma de jogar, em vez de editar valores à mão.
- Coloque no `modules.ini` os valores que raramente mudam (o monitor desligado, os pisos) e no preset os que dependem do mapa (orçamento, veículos).
- Execute `OmsiLaunch.exe /predefined-profile:<id> /predefined-profile-index:<n> /new /plan --json` para conferir um preset: o diagnóstico `module.planned` e a ausência de `OL_E_MODULE_SETTING_INVALID` o confirmam.

<a id="next"></a>
## Próximos passos

[Sobrescritas pela linha de comando](command-line-overrides.md) · [Desligando](turning-off.md) · [Referência de configurações](../reference/settings.md)
