# Prospice Gamepad Placement

Locally installed candidate **0.5.0** adds purchasable paint buckets, an equipped sprayer and persistent graffiti. The build and 300 standalone checks pass, as does validation of 546 paint-query meshes. Native shop, controller, tree-swap, rendering and persistence acceptance remain pending. The published ZIP remains **0.3.2**. See [paint implementation and acceptance](docs/paint-system.md) and [cargo behavior](docs/cargo-belts.md).

[Download the published mod](https://b2kdaman.itch.io/prospice-gamepad-placement) | [Gameplay video](https://www.youtube.com/watch?v=8wn0nT1W1JI)

## Controls

Carry an ordinary cargo item, artifact, toolbox or supply. Release LT to neutral first, then:

| Control | Action |
| --- | --- |
| Hold LT | Show the filled surface placement preview |
| Left stick | 90-degree turns around grid Y/X axes; return to neutral between turns |
| Right stick | Normal camera movement, including during placement |
| RT + left stick | Position sideways and select support height; rotation stays off |
| Release LT | Place when cyan; keep carrying when red |
| B | Cancel and keep carrying |
| X / mapped Use | Cancel preview and perform the game's ordinary drop |

The native interaction panel shows the mapped **[X / Use] Drop** row and a **Hold [LT] Place manually** row while carrying an eligible item with a gamepad. The existing TextMeshPro rows, masks, layout and button formatter are reused. Both the added standalone carry label and the IMGUI preview box are removed. During preview the LT row reads **Release to place** or **Cannot place**; normal native prompts are restored afterward. Contextual use actions keep their native labels with LT appended in the same row.

The preview is the filled translucent mesh again: cyan for valid placement and red for invalid placement. It renders on the normal world layer through the scene camera before post-processing, so the game applies its current visual effects. Scene depth occludes it normally. The separate outline texture and after-effects GUI compositor are removed.

Walking, sprinting and jumping are suppressed during preview. The right stick retains normal camera movement, including native sensitivity and inversion. Horizontal inertia is cleared immediately; gravity remains active. RT positioning uses world up and camera-relative sideways movement captured on entry, at 0.5 metres/second by default. Each preview starts horizontal at the target grid's zero heading. Turns use exact grid-relative axes, independent of the carried item angle and camera direction. Placement snaps to a shared 6.25 cm lattice, and the item bottom seats against the actual surface or object below it. On a truck grid, vertical offset is ignored: the item is always seated above its verified storage floor or cargo support. The floor query starts above the storage volume and spans its bottom regardless of preview height. Mesh and collider bounds must remain above the floor before release; missing floor support keeps the item held. Normal placement reach still applies. Outside truck grids, vertical input selects a support height rather than leaving the item floating. Releasing RT resumes rotation after the stick returns to neutral. The position offset persists until placement/cancellation. Surfaces over 60 degrees, blocked positions and targets beyond the actual interaction reach are invalid. Winch anchors and special deployment objects are excluded. Unsupported collider geometry keeps the item held. Keyboard/mouse use remains vanilla outside the gamepad preview; switching to those devices cancels it.

While LT placement is active near a truck, exposed cargo-bed and roof-rack surfaces show a subtle cyan glow grid through the game's visual effects. Covered patches are omitted. This shows free surface area; the red/cyan item preview still determines whether the complete item fits. The common **6.25 cm step** was calculated from 46 eligible item records (16 distinct collider sizes), and stays the same for every item. [Calculation and dimensions](docs/item-size-grid.md).

While on foot and empty-handed, look at an exposed cargo deck or roof rack within interaction reach. The native panel shows **Secure cargo** and its button. Right-stick click (**R3**) is preferred; enabled player and system mappings are checked, with RB, LB, Y and D-pad directions as fallbacks. If all candidates are assigned, the action stays hidden. Release and press the displayed button to re-secure the targeted surface, including newly added loads. Blocked views, carried items, menus and vehicle control suppress the action. Existing belts are released and rebuilt on the next frame so deferred joint destruction cannot exclude old cargo.

## Build, test and install

From `C:\git\prospicemod`, with the game closed:

```powershell
.\tools\build-placement.ps1
.\tools\test-placement.ps1
.\tools\install-placement.ps1
```

The build uses the already installed local .NET SDK and generated bindings. The installer verifies the game hash and backs up any replaced placement DLL. It does not install the loader or recover metadata on another machine: follow [the verified metadata setup](docs/metadata-workaround.md) first.

Installed DLL:
`E:\SteamLibrary\steamapps\common\Prospice Demo\BepInEx\plugins\Prospice.Placement.dll`

Configuration:
`E:\SteamLibrary\steamapps\common\Prospice Demo\BepInEx\config\prospice.gamepadplacement.cfg`

Defaults: Enabled=true, TriggerEnter=0.55, TriggerRelease=0.35, StickDeadZone=0.18, MoveMetersPerSecond=0.5, ShowTruckStorageGrid=true. StickDeadZone applies to both positioning and quarter-turn rearming. Rotation activates at max(0.55, StickDeadZone + 0.01), so raising the neutral zone above 0.55 still requires leaving it before a turn. Default controls are unchanged. Restart after changing configuration. Diagnostics.RunSelfTests is off for ordinary play. The optional self-tests create temporary objects and report results in `BepInEx\LogOutput.log`, but the surrounding game can still process menu input and write/reset saves. Do not run diagnostics against a live player profile. An independently verified disposable profile and full save backup are required; a hidden window or separate log file does not isolate user data.

## Removal

Close the game, then run:

```powershell
.\tools\remove-placement.ps1
```

This archives only the placement DLL outside the plugin folder. Configuration and the loader remain available for reinstalling. To bypass the entire loader too, set `enabled = false` under `[General]` in the game's `doorstop_config.ini`. No original game binary replacement or save rollback is needed.

## Tested dependencies

| Dependency | Version / location |
| --- | --- |
| Unity | 2022.3.62f2, game Windows x64 IL2CPP |
| BepInEx | 6.0.0-be.788+5b766a3, Windows x64 IL2CPP |
| Loader runtime | .NET 6.0.7 |
| Il2CppInterop | 1.5.3.0, loader supplied |
| HarmonyX (0Harmony) | 2.10.2.1, loader supplied |
| SDK | 8.0.425, `local/dotnet-sdk` |
| Plugin target | net6.0 |
| Game bindings | Generated from this game's recovered metadata v31; 122 interop assemblies |
| ILSpy command-line research tool | 9.1.0.7988, local only |

The loader's GlobalMetadataPath currently points to `C:/git/prospicemod/local/runtime-metadata/recovered/global-metadata.runtime.dat`. Keep that private file in place. Game binaries, decoded metadata, generated bindings, logs and dependencies are ignored by Git and are not redistributable parts of this source repository.

## Implementation and evidence

- [Code review, resolved findings and component map](docs/code-review-0.3.3.md)
- [Verified pickup/drop/throw loop and integration details](docs/verified-drop-loop.md)
- [Validation results and controller playtest checklist](docs/placement-validation.md)
- [Agreed behavior](docs/placement-spec.md)
- [Agent instructions](AGENTS.md)
- [Metadata recovery and diagnostic plugin](docs/metadata-workaround.md)
- [Initial installation analysis](docs/installation-analysis.md), [deeper game analysis](docs/deep-analysis.md), [class index](docs/game-class-index.csv)

Source components live in `src/Prospice.Placement`: input arbitration, gamepad adapter, solver, renderer, game integration and runtime checks. The plugin refuses an unsupported GameAssembly hash instead of using stale bindings. Phase 2 replaces truck-bed and roof-rack packing eligibility and visuals while retaining native release timing and save formats. See [cargo behavior and validation](docs/cargo-belts.md).

## Read-only asset inspection

```powershell
python -m pip install --target .\local\python-deps -r .\tools\requirements-analysis.txt
python -B .\tools\inspect_game.py --game-dir 'E:\SteamLibrary\steamapps\common\Prospice Demo' --output .\local\analysis
```

Historical analysis documents retain the findings from before the metadata workaround and gameplay implementation.

## Release ZIP

Run `tools/prepare-release.ps1` from a clean committed checkout. It runs standalone tests and the Release build, then stages an explicit plugin/document allowlist, records the exact source commit and SHA-256 manifest, and verifies the extracted ZIP. Output is under ignored `artifacts/releases/`.

This produces a Windows x64 playtest ZIP for an already configured matching loader. Game files, recovered metadata, bindings, third-party DLLs, user settings and saves are excluded. Installation and rollback instructions are in `release/INSTALL.md`. Automated checks, manual acceptance and publication are recorded separately; packaging does not publish a GitHub release.

## Cargo belts (Phase 2)

Version 0.4.0 adds two perpendicular matte dark-gray flat belts, a center keeper, and painted deck loops. Actual gravity determines deck connections. Contact geometry determines which items and stacks can be secured; unsupported loads and blocked routes stay loose. Pickup, exit, unloading, and tipping release cargo through the native lifecycle.

`[Cargo] EnableFlatBelts = false` restores native packing on the next vehicle entry. Installation generates and validates a private mesh-query cache under `BepInEx/cache` from the installed game. The source installer needs Python and the pinned dependencies in `tools/requirements-analysis.txt`, installed in `local/python-deps` as described above. Never redistribute this cache. If it is missing or incompatible, the plugin logs the reason and uses native packing for the affected surface.

Offline build, geometry, and placement checks do not establish in-game visual or driving acceptance. Do not launch diagnostics against a live save profile.

## Paint equipment (0.5.0 candidate)

The airbrush is automatically available in the tool inventory with an empty 250 ml tank. Only 1-liter paint buckets (25) are sold in the shop, in eight colors. Tap D-pad right to cycle **empty -> camera -> airbrush -> empty**. No airbrush purchase or pickup is required. Stowing frees the native hand slot and preserves paint in the sidecar save. RT/left mouse sprays, hold left mouse/RT while looking at a bucket gradually draws paint at 50 ml/s and mixes it with the current tank color, and B/Escape stows the inventory airbrush. Hold Use for two seconds away from buckets to discard the remaining color. Buckets show their percentage in the native interaction overlay. A 250 ml tank lasts two minutes of continuous spray.

Original Blender models and eight textures are embedded in the DLL. Regenerate them with Blender 5.0 and tools/build-paint-assets.py. The source installer generates a private world geometry cache, including static batches, and validates it before replacing the plugin. Do not redistribute that game-derived cache. Paint sidecars live beside the native save under ProspicePaint; include this directory in save backups.

The physical bucket model and collider are doubled in all dimensions; spray size and paint capacity remain unchanged. The airbrush is free.

While equipped, hold **LB + right stick**: up/down changes alpha (5–100%), left/right changes brush size (25–200%). Native HUD rows show tank, alpha and size. The airbrush uses a close camera-attached model, a live mixed-color indicator and a spray hiss. These latest audio/visual changes still require in-game acceptance.
