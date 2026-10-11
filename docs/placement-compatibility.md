# Initial placement compatibility test (historical)

**Update:** The metadata/runtime-access blocker was subsequently resolved. See [metadata-workaround.md](metadata-workaround.md) for the successful loader and diagnostic plugin test. The X-drop callback still needs verification.

Tested on 2026-10-12 against the installed Prospice Demo. **There is no working placement plugin or installed loader.** Implementation stopped at the explicitly required compatibility gate, before guessing hooks or modifying gameplay.

## Tested environment

| Component | Observed version |
| --- | --- |
| Unity | 2022.3.62f2 |
| Platform | Windows x64, IL2CPP |
| BepInEx | 6.0.0-be.788+5b766a3 |
| BepInEx commit | 5b766a3b7f6c164d4798924a93f3acf4db769d06 |
| Bundled runtime | .NET 6.0.7 |
| Cpp2IL.Core / LibCpp2IL file versions | 2022.1.0.0 |
| Il2CppInterop file versions | 1.5.3.0 |
| Unity base libraries downloaded by BepInEx | 2022.3.62 |

Official [IL2CPP loader installation guide](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html) and [build listing](https://builds.bepinex.dev/projects/bepinex_be).

Archive: `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip`

SHA-256: `F4CC496BD098A0DF4164B81E3737297707F13A47C2478DBA2F60EEFAB784817A`

## Observed failure

BepInEx bootstrapped and identified Unity correctly. Downloading and extracting the Unity base libraries succeeded. Failure occurred when Cpp2IL tried to parse the game's metadata:

```text
[Error :InteropManager] Failed to generate Il2Cpp interop assemblies
System.FormatException: Invalid or corrupt metadata (magic number check failed)
```

It subsequently reported:

```text
[Fatal :BepInEx] Unable to execute IL2CPP chainloader, no plugins will be loaded
System.IO.FileNotFoundException: Could not load file or assembly 'UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'.
```

The first error is the blocker. The second is a downstream consequence of missing generated interop assemblies, not evidence that the game's native Unity runtime is missing. The game's original metadata begins `GMEV`, as documented in the earlier analysis.

**This is a measured failure of the tested stock loader workflow, not proof that the game cannot be modded.** Runtime type inspection, a diagnostic plugin, and the X-drop method have not been verified. Replacing the metadata magic or supplying an ordinary managed Unity DLL would not establish compatible field layouts or hooks.

## X-drop investigation status

Static asset evidence locates:

| Asset | Component path ID | Class | GameObject |
| --- | ---: | --- | --- |
| level3 | 10024 | FPActions | OnFootPlayer |
| level3 | 10025 | PlayerHeldItem | OnFootPlayer |
| level3 | 11237 | PlayerInteraction | Cam |
| level3 | 10473 | TruckBed | Rack |
| level3 | 11519 | TruckBed | Bed |

Pickupable and Packable components also occur on toolboxes and collectible objects. Their custom field layouts could not be decoded from the available type trees.

The serialized Rewired action records associate Use with action 31, Aim with 29, Fire/Throw with 30, and Mouse X/Mouse Y camera actions with 19/18. The saved joystick map connects the on-foot Use action to controller element 8 and Aim/Fire to axis elements 4/5. These are build/controller-specific observations, not universal button IDs. The user identifies X as the existing drop control; the native callback was not traced during this failed test.

The integration hypothesis remains PlayerInteraction -> FPActions/PlayerHeldItem -> Pickupable/physics release. It is **not** a verified call graph. Ordinary release versus throwing, trigger timing, collision restoration, reach distance, and TruckBed attachment callbacks remain unknown.

## Rollback and preserved evidence

Before launch, the complete existing user-data directory was copied to ignored local backup storage. No pre-existing game-root path was overwritten by installation. A separate Unity log path preserved the user's previous Player.log.

The launched test process (PID 21016) was closed after diagnosis; it did not exit within the five-second graceful-close interval and was terminated. Only that process was targeted. All six roots introduced by this test were moved out of the game folder into the local diagnostic archive:

`BepInEx`, `dotnet`, `.doorstop_version`, `changelog.txt`, `doorstop_config.ini`, `winhttp.dll`.

After removal, the game-root listing matched the original six entries. SHA-256 verification confirmed that GameAssembly.dll, global-metadata.dat, demo.json, and settings.json were unchanged. No backup restoration of those files was necessary. No gameplay session or controller test was completed.

Local evidence, intentionally excluded from Git:

- `local/compatibility-788/removed-loader/BepInEx/LogOutput.log`: exact loader errors.
- `local/compatibility-788/Player-test.log`: separate Unity log.
- `local/compatibility-788/manifest.json`: installed roots and before hashes.
- `local/compatibility-788/after-checks.json`: unchanged-file verification.
- `local/compatibility-788/userdata-before/`: user-data backup.
- `local/bepinex-6.0.0-be.788/`: downloaded package and original staging copy.

## Required next milestone

Establish a metadata/interop workflow compatible with this GMEV-protected build, then demonstrate that a diagnostic plugin loads and can inspect the relevant player/item types. Recover and verify the normal release method before building the placement feature. Do not enable gameplay patches until that milestone passes.

The host also has no .NET SDK installed; it has runtimes only. A repository-local SDK will be needed for building C# after the compatibility problem is resolved. SDK installation was deferred because it cannot fix metadata parsing.

The agreed behavior is preserved in [placement-spec.md](placement-spec.md). No plugin DLL, mod configuration, gameplay hooks, or claimed installation instructions for a working mod are delivered at this gate.
