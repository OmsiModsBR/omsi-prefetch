# Desligando

<!-- l10n: source=guides/turning-off.md source-sha256=1adcd788e9dc877e6bff63b6df4504ca669e02d6475eae87ddf5f5a5d644640b -->
> Tradução da [página original em inglês](../../guides/turning-off.md) do Omsi Prefetch 1.0.0-beta.1. A página em inglês é a referência normativa: se as duas divergirem, valem a página em inglês e o código.

Você pode manter o Omsi Prefetch fora de uma sessão, fora de um perfil, fora de todas as sessões, ou desligar apenas alguns dos seus mecanismos. Esta página lista cada forma, da mais leve à mais permanente, com o efeito no plano para que você possa conferir. Qual forma vence quando elas discordam está dito no fim.

Páginas relacionadas: [sobrescritas pela linha de comando](command-line-overrides.md), [perfis de sessão](session-profiles.md), [limpando dados](clearing-data.md), [instalação](../getting-started/installation.md).

<a id="for-one-session"></a>
## Para uma sessão

```text
OmsiLaunch.exe /new /map:maps\Grundorf\global.cfg /entrypoint-index:1 /no-module:omsi-prefetch
```

`/no-module:omsi-prefetch` mantém o módulo fora daquela sessão, inclusive o módulo carregado por padrão. O plano mostra o diagnóstico `module.disabled` para `omsi-prefetch` (confira com `/plan --json`). Use-o para a metade "sem o módulo" de uma comparação ([medindo o ganho](../getting-started/measuring-the-gain.md)). Nada é iniciado, mantido ou gravado pelo módulo nessa sessão; os dados dele de sessões anteriores permanecem.

<a id="for-a-profile-or-a-preset"></a>
## Para um perfil ou um preset

No preset de um perfil de sessão do OmsiLaunch:

```yaml
    modules:
      omsi-prefetch:
        enabled: false
```

As sessões iniciadas com esse preset não carregam o módulo ([perfis de sessão](session-profiles.md)). A linha de comando não pode contradizê-lo: `/module:omsi-prefetch` com esse preset é `OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT`.

<a id="for-every-session"></a>
## Para todas as sessões

Edite `<OMSI root>\.omsilaunch\modules.ini` e acrescente:

```ini
[omsi-prefetch]
enabled=false
```

(`true`/`false`, `yes`/`no`, `on`/`off`, `1`/`0` são aceitos para `enabled`.) O OmsiLaunch apenas lê este arquivo. Para trazer o módulo de volta, defina `enabled=true` ou apague a linha.

<a id="permanently"></a>
## Permanentemente

Remova a pasta do módulo `<OMSI root>\plugins\OmsiLaunch.Modules\omsi-prefetch\`. Nada mais precisa mudar: o módulo nunca modificou um arquivo do OMSI. Apague o que ele aprendeu com `OmsiLaunch.exe /clear-module-data:omsi-prefetch` ([limpando dados](clearing-data.md)).

Renomear a pasta não funciona: o nome da pasta deve ser igual ao id do módulo, portanto uma pasta renomeada é reportada como módulo inválido (`OL_E_MODULE_MANIFEST_INVALID`). Mova a pasta para fora de `plugins\OmsiLaunch.Modules\`.

<a id="only-some-mechanisms"></a>
## Apenas alguns mecanismos

O módulo pode continuar instalado com um dos seus mecanismos desligado. Cada um destes é uma configuração ([referência de configurações](../reference/settings.md)):

| Para desligar | Configuração |
| --- | --- |
| A manutenção de arquivos em memória | Não pode ser desligada isoladamente: o menor orçamento é `ram_budget_mb=256`. Combine-o com `ai_vehicles=off`, `survey=false`, `user_vehicles=none` e `pre_open=none` para manter quase nada (uma lista aprendida existente ainda é trazida até o orçamento se esgotar), ou use `/no-module:omsi-prefetch`. |
| Read-ahead das pequenas leituras do OMSI | `read_ahead=false` |
| Write-behind do cache de terreno | `write_behind=false` |
| Abertura somente leitura de assets | `read_only_open=false` |
| Antecipação de tiles (índice de tiles, tiles iniciais, previsão) | `survey=false` |
| Aprendizado | `learn=false` (uma lista aprendida existente ainda é usada; para ignorá-la, limpe os dados) |
| Pré-abertura de arquivos de veículos | `pre_open=none` e `pre_open_on_first_use=false` |
| Os veículos do usuário | `user_vehicles=none` |
| Os veículos de IA | `ai_vehicles=off` |
| A janela de primeira abertura | `first_open_notice=false` |

Com `read_ahead`, `write_behind` e `read_only_open` todos desligados, o componente nativo apenas envia os seus relatórios de uso ([shim nativo](../concepts/native-shim.md)).

<a id="when-they-disagree"></a>
## Quando elas discordam

Da maior prioridade para a menor:

1. Um pedido explícito na linha de comando ou no preset selecionado: `/module:omsi-prefetch` ou `modules.omsi-prefetch.enabled: true` pedem o módulo, `/no-module:omsi-prefetch` ou `enabled: false` o mantêm fora. Um conflito entre a linha de comando e um preset é rejeitado (`OL_E_SESSION_PROFILE_OVERRIDE_CONFLICT`); ligar e desligar o módulo no mesmo pedido é `OL_E_MODULE_SETTING_INVALID`.
2. `enabled=` em `.omsilaunch\modules.ini`. Um pedido explícito vence o arquivo.
3. A ativação do módulo: `default`, de modo que ele entra em toda sessão.

<a id="next"></a>
## Próximos passos

[Limpando dados](clearing-data.md) · [Sobrescritas pela linha de comando](command-line-overrides.md) · [Perfis de sessão](session-profiles.md)
