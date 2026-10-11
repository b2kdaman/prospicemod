# Installation analysis

Inspected on 2026-10-12. Source directory: `E:\SteamLibrary\steamapps\common\Prospice Demo`.

## Summary

The directory is a packaged Unity Windows game, not a Unity source project. The inspection found 39 files totaling 2,848,306,818 bytes (approximately 2.65 GiB). No source solution, Unity project structure, or mod-loader installation was present in the inspected tree. The game was not launched or modified.

| Finding | Evidence |
| --- | --- |
| Company / product | `Project Prospice_Data/app.info`: CodeWakers / Project Prospice |
| Unity version | `UnityPlayer.dll` product version: `2022.3.62f2 (7670c08855a9)`; `data.unity3d` header also contains `2022.3.62f2` |
| Windows x64 target | `StreamingAssets/aa/settings.json`: `StandaloneWindows64`; native plugins under `Plugins/x86_64` |
| IL2CPP packaging | Root `GameAssembly.dll` and `il2cpp_data/Metadata/global-metadata.dat` |
| Build GUID | `boot.config`: `6576f41b5bf14fb5b18db6a274b59e87` |
| Addressables | `StreamingAssets/aa/settings.json`, `catalog.json`, and 10 bundles |
| Rendering / input dependencies | Assembly list includes Universal Render Pipeline, Rewired, Cinemachine, and DOTween |
| Other notable dependencies | Unity Localization, GPU Instancer Pro, MagicaCloth, Newtonsoft.Json, GUPS.Obfuscator, and IngameDebugConsole |

Assembly-list entries establish packaged dependency names, not the availability of their APIs at runtime or whether a debug console is enabled.

## File map

| Path relative to installation | Purpose / observation |
| --- | --- |
| `Project Prospice.exe` | Player executable |
| `UnityPlayer.dll` | Unity runtime, 31,006,120 bytes |
| `GameAssembly.dll` | Native IL2CPP game assembly, 67,816,448 bytes |
| `Project Prospice_Data/data.unity3d` | UnityFS archive, 2,494,592,132 bytes |
| `Project Prospice_Data/sharedassets*.resource` | Additional resource payloads; largest is sharedassets3.resource at 204,302,441 bytes |
| `Project Prospice_Data/il2cpp_data/Metadata/global-metadata.dat` | Metadata-named payload, 16,566,504 bytes, nonstandard header |
| `Project Prospice_Data/ScriptingAssemblies.json` | Logical assembly inventory, including Assembly-CSharp and Assembly-CSharp-firstpass |
| `Project Prospice_Data/RuntimeInitializeOnLoads.json` | Runtime initializer inventory |
| `Project Prospice_Data/Plugins/x86_64` | Rewired native input plugins and lib_burst_generated.dll |
| `Project Prospice_Data/StreamingAssets/aa` | Addressables catalog/settings and localization bundles |

The eight language string-table bundles are English, French, German, Japanese, Portuguese, Russian, Spanish, and simplified Chinese. Two additional bundles contain locales and shared localization assets. Bundle names alone do not prove complete translation coverage.

## Metadata caveat

The first 32 bytes of `global-metadata.dat` are:

```text
47-4D-45-56-01-00-00-00-D4-0C-6B-08-7F-CB-F7-B2-4B-D4-E5-83-22-3C-65-45-73-77-33-E2-B5-A2-AD-13
```

The leading bytes spell `GMEV`; the next four bytes encode 1 in little-endian order. This is recorded as a file-header observation, not a confirmed IL2CPP metadata version. The assembly list includes GUPS.Obfuscator, which suggests a protection-related lead, but inspection did not establish how this payload is encoded or which component produces it. Standard extraction, generated bindings, and mod-loader compatibility have not been tested.

## Baseline fingerprints

SHA-256:

```text
GameAssembly.dll
511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D

Project Prospice_Data/il2cpp_data/Metadata/global-metadata.dat
C5F6F4887E3181EBCE0F0C6D0B95EA192EC38E6EAB642FA2A4697337594B9152
```

## Next development steps

1. Define the desired mod behavior and identify the relevant runtime types or assets.
2. Verify a Windows x64 IL2CPP toolchain against Unity 2022.3.62f2 and this metadata format.
3. Establish a minimal loader/plugin smoke test with documented installation and removal steps.
4. Add implementation, build configuration, and focused validation after compatibility is demonstrated.

No loader selection, metadata decoding, decompilation, asset extraction, game launch, or functional mod test was performed in this initial analysis.
