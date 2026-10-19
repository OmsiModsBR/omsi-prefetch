# Compatibility

This page states what Omsi Prefetch `1.0.0-beta.1` works with: OmsiLaunch versions, Windows, the .NET runtimes, OMSI 2 builds, maps and vehicles, other plugins and modules, and the languages of its windows. Sources: `prefetch\module.template.json`, the project files under `prefetch\`, and the native and session-program code.

Related pages: [installation](../getting-started/installation.md), [errors and degradation](errors-and-degradation.md), [coexisting](../guides/coexisting.md), [known limitations](known-limitations.md).

## At a glance

| Subject | Compatibility |
| --- | --- |
| OmsiLaunch | The first release with bridge protocol 3; an OmsiLaunch that serves only protocols 1 and 2 (such as `0.1.0-beta4`) does not load the module. The manifest declares `bridge_protocol` `{ "min": 3, "max": 3 }`, activation `default`, required capabilities `bridge.events` and `bridge.hooks.iat`, failure policy `degrade`. |
| Windows | Windows 10 or later, 64-bit (the platform OmsiLaunch supports). The session program is x64. |
| .NET | x64: .NET 6 Desktop Runtime (the session program is a framework-dependent `net6.0-windows` single file; OmsiLaunch's controller needs the same runtime). x86: .NET 6 Runtime for the managed component inside OMSI (`net6.0`, AnyCPU; the OmsiLaunch plugin already needs it). |
| Native | `prefetch-native.dll` is a 32-bit (x86) C++ DLL built with the OmsiLaunch native module header (`omsilaunch_module_native_v1.h`), loaded into OMSI by the bridge. |
| OMSI 2 | Build-independent; see below. |
| Memory | Up to 12 GB held, whatever the amount of RAM. See [choosing settings by memory size](../guides/choosing-settings-by-memory.md). |
| Other plugins | Conflicts with `OmsiLoadingPoC.dll` (declared in the manifest). See below. |
| Display language of the module's windows | Portuguese when the Windows display language is Portuguese; English otherwise. |
| License | GNU General Public License, version 3. |

## OMSI 2 builds

The module carries **no OMSI address, offset, byte pattern or build hash**. The native component asks the OmsiLaunch bridge for hooks on imports of `Omsi.exe` by name (`iat:kernel32.dll!CreateFileW`, `iat:d3dx9.dll!D3DXCreateTextureFromFileExW`, ...), and the bridge installs a hook only over the value the import should hold, refusing a site that someone else changed. Because of that:

- there is no per-build profile to maintain, and a build is not "supported" or "unsupported" by the module; what matters is whether the bridge can hook each import on your `Omsi.exe`, which is decided when OMSI starts and reported in the [`hooks` event](events-and-notices.md#the-hooks-event);
- the module requires the bridge feature `bridge.hooks.iat` (and `bridge.events`); OmsiLaunch checks them when it plans the session;
- the functions the native features hook are kernel32 and d3dx9 imports of `Omsi.exe`; on a build where an import is missing or was already changed, only that hook is refused and the features that depend on it stay off.

The session program reads the maps and vehicles of an installation the way OMSI stores them: it was checked on the clean installation (Grundorf and Berlin-Spandau for the `[entrypoints]` layout of `global.cfg`) and, for the study, on the RMG Leste map.

## Maps, vehicles and add-ons

- Any map with a `global.cfg` that OMSI can read: the module needs the `[map]` list (tile coordinates and file names) and, for the start tiles, the `[entrypoints]` list. A map with no entrypoint starts from the tile nearest the middle.
- Any vehicle that follows OMSI's file layout (`.bus` or `.ovh` with a `[model]` section; `.cfg`, `.o3d`, textures, repaints through `[ctc]`/`.cti`, sounds, `.hof`).
- Add-ons are only read. The module does not activate, deactivate or alter any content, license or entitlement, and it never writes into an add-on folder.
- Folders that are junctions or symbolic links are not entered when the module lists a folder (the map-wide files, a vehicle's folder); a file cited by path is still resolved if it lies inside the OMSI folder.

## Other plugins and modules

| Case | Result |
| --- | --- |
| `OmsiLoadingPoC.dll` listed in a `plugins\*.opl` | `OL_E_MODULE_CONFLICT` for an explicit request, `OL_W_MODULE_NOT_LOADED` otherwise; the module is not loaded ([coexisting](../guides/coexisting.md)). |
| Another plugin that hooks the same `Omsi.exe` imports | The bridge refuses a hook over a site someone outside the bridge changed (result `4`); the module degrades ([errors and degradation](errors-and-degradation.md)). |
| Other OmsiLaunch modules | They share the bridge's hook registry; hooks on one site stack and are removed last-in first-out. Memory is shared: every module that holds memory counts against the same RAM. |
| OmsiLaunch session profiles | A preset may carry the module's settings or switch it off ([session profiles](../guides/session-profiles.md)). |
| Plugins that read or write files OMSI uses | They see the same files; the module never holds a file the game rewrites. |

## Platform notes

- The module does not support Windows 7, 8.x or XP, nor ARM64, because OmsiLaunch does not.
- Antivirus products that scan every file open apply to the module's opens as to any program's; the module does not change that.
- The module needs OmsiLaunch to start OMSI: it does nothing for an OMSI started by hand or through another launcher.

## Next

[Known limitations](known-limitations.md) · [FAQ](faq.md) · [Errors and degradation](errors-and-degradation.md)
