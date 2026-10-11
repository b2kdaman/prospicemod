# Metadata workaround verified

On 2026-10-12, the metadata compatibility blocker was bypassed for this installed build. This report supersedes the failed-gate status in [the initial loader test](placement-compatibility.md).

## What worked

1. Launched the unmodified game without BepInEx, using a separate log file.
2. Read only that game's process memory. A first scan restricted to metadata version 29 found no match. Static inspection of the actual metadata-loading code then identified the live metadata pointer and revealed version 31.
3. Updated the scanner to recognize structurally valid versions 29 and 31. It recovered a copy with a standard header, bounded metadata sections, and expected game type names.
4. Set BepInEx's `GlobalMetadataPath` to the recovered copy, leaving the game's original encrypted metadata untouched.
5. BepInEx generated 122 interop DLLs and reached `Chainloader startup complete`.
6. Built and loaded `Prospice Read-Only Diagnostics 0.1.0`. Its runtime result was:

```text
PROSPICE_DIAGNOSTICS_OK: inspected 9 native classes; no gameplay methods invoked.
```

The recovered metadata is version **31**, 16,566,480 bytes. SHA-256:

`72E167DA15FDD66F540A64B76A70B3B93A9EC09A195F2A2FA33A65C5C53ADFC3`

The installed original remains 16,566,504 bytes with its original hash. Recovery does not require patching its header, modifying GameAssembly.dll, or writing into process memory. The scanner opens only query/read process permissions and verifies the executable path before scanning. It writes only validated metadata, not a full process dump.

## Setup at the diagnostic checkpoint (historical)

BepInEx **6.0.0-be.788+5b766a3** remains installed in the game folder with generated bindings. The diagnostic plugin was removed from `BepInEx/plugins` after the successful test and archived locally. The test process was closed. At that checkpoint the gameplay mod had not yet been implemented. It is now implemented; see [the verified drop loop](verified-drop-loop.md) and [current source/build status](../README.md).

Configuration in `BepInEx/config/BepInEx.cfg`:

```ini
[IL2CPP]
GlobalMetadataPath = C:/git/prospicemod/local/runtime-metadata/recovered/global-metadata.runtime.dat
DumpDummyAssemblies = true
```

The path is specific to this workstation. Keep the decoded copy and generated game assemblies private and outside Git. Recheck the game/metadata hashes after an update; recover matching metadata before regenerating interop. Do not reuse this copy blindly with a different GameAssembly.dll.

The loader emitted `Class::Init signatures have been exhausted, using a substitute!`. It still completed initialization and the diagnostic plugin succeeded. This warning remains relevant to later gameplay testing; plugin load success does not verify arbitrary patches.

The local Microsoft SDK is **8.0.425**, installed under `local/dotnet-sdk` and verified against the official download's SHA-512. Diagnostics targets .NET 6.0, matching BepInEx's bundled .NET 6.0.7 runtime. Il2CppInterop file version is 1.5.3.0; Cpp2IL.Core/LibCpp2IL file versions are 2022.1.0.0.

## Newly verified runtime information

Runtime inspection successfully resolved PlayerInteraction, FPActions, PlayerHeldItem, Pickupable, Packable, TruckBed, PlayerController, PlayerCore, and PauseManager.

PlayerInteraction exposes fields including:

- `heldRigidbody`, `handTransform`, `heldScale`.
- `interactionDistance`, `interactables`, `dropMask`.
- `dropCheckRadius`, `maxDropAttempts`.
- `playerHeldItem`, `actions`, `characterController`.

Its runtime methods include `bvr()`, `bvs(Rigidbody, Vector3, Vector3, bool, Packable)`, `bvt(Rigidbody, Vector3, Vector3)`, `bvu(float)`, and `bvw()`. At this diagnostic checkpoint only the signatures were verified. Their subsequently established semantics are documented in [the verified drop loop](verified-drop-loop.md). Do not label bvs or another candidate as the ordinary drop callback solely from its signature.

Pickupable exposes `rb`, `packable`, `holdOffset`, `angleOffset`, `OnPickup`, and pickup/stash state. Packable exposes `packed`, `joint`, and `ropes`. TruckBed exposes bed/body references, its packed flag, and a list of Packable objects. These make the placement integration substantially more tractable than the original stripped asset trees suggested.

The metadata/runtime-access part of the gate has passed. Tracing the X-drop and RT-throw callbacks was the next prerequisite at that time; it has since been completed as recorded in the linked drop-loop report.

## Reproducing and building

When reproducing runtime work, use an independently verified disposable game profile and complete backups; the game itself can process menu input and reset/write saves. Never launch automated diagnostics against the live user profile.

With the unmodified game running in that verified environment, recover metadata to a **new** ignored output directory:

```powershell
python -B .\tools\recover_runtime_metadata.py --pid <game-pid> --game-dir 'E:\SteamLibrary\steamapps\common\Prospice Demo' --output .\local\new-metadata-recovery
```

The scanner supports this observed metadata layout; it is not a universal IL2CPP dumper. It validates version, all 31 section bounds, and required game strings. Downstream Cpp2IL parsing and actual plugin loading provided the additional verification in this run.

Build the read-only diagnostic plugin from this repository:

```powershell
.\tools\build-diagnostics.ps1
```

The build output is `src/Prospice.Diagnostics/bin/Release/net6.0/Prospice.Diagnostics.dll`. To rerun diagnostics, copy only that DLL into `BepInEx/plugins`, launch the game, and check for the success marker and `BepInEx/diagnostics/runtime-types.json`. Remove that DLL afterward. Do not mistake the diagnostic plugin for the placement mod.

To disable the loader, set `enabled = false` under `[General]` in `doorstop_config.ini` while the game is closed. Full removal consists of the six roots originally added by this task: `BepInEx`, `dotnet`, `.doorstop_version`, `changelog.txt`, `doorstop_config.ini`, and `winhttp.dll`. Preserve any later mods/configuration before removing them.

## Evidence and validation

Ignored local evidence:

- `local/runtime-metadata/recovered/metadata-recovery.json` and its validated metadata file.
- `local/compatibility-runtime-metadata/loader-interop-success.log`.
- `local/compatibility-runtime-metadata/loader-diagnostics-success.log`.
- `local/compatibility-runtime-metadata/runtime-types.json`.
- `local/compatibility-runtime-metadata/after-checks.json`.
- `local/dotnet-sdk-source.json` records the official SDK source and hash.

Diagnostics built with zero warnings and zero errors. Its plugin loaded, inspected nine classes, and invoked no gameplay methods. GameAssembly.dll, the original global-metadata.dat, demo.json, and settings.json matched their original SHA-256 values after testing.
