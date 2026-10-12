# Prospice Gamepad Placement 0.5.0 - playtest build

Windows x64 Prospice Demo, Unity 2022.3.62f2. Automated checks pass; current in-game/controller acceptance is pending. This ZIP contains the mod only, not the game or its mod loader.

## Compatibility and prerequisites

Requires the existing verified BepInEx 6.0.0-be.788+5b766a3 Windows x64 IL2CPP installation, its .NET 6.0.7 runtime, Il2CppInterop 1.5.3.0, HarmonyX 2.10.2.1 and matching generated game bindings. Supported GameAssembly.dll SHA-256:

`511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D`

The game has protected metadata. Plain BepInEx installation alone is insufficient: the loader must already use a matching locally recovered metadata copy with generated interop assemblies. None of these private game files is included. The source repository documents that setup in docs/metadata-workaround.md. Do not copy another workstation's metadata path blindly, or use this build after a game update without revalidation. An unsupported game hash disables placement hooks.

Source and setup notes: https://github.com/b2kdaman/prospicemod

## Install or upgrade

1. Save and close Prospice. Back up your current save folder and existing placement DLL before a playtest. User data is in `%USERPROFILE%\AppData\LocalLow\CodeWakers\Project Prospice`.
2. Verify the prerequisite loader/bindings are already working for the matching game build.
3. Copy only `BepInEx/plugins/Prospice.Placement.dll` from this ZIP into the same relative location in the game directory. Keep one copy of this plugin. Do not place the whole ZIP inside plugins.
4. Existing configuration is preserved. Defaults are created on first normal launch in `BepInEx/config/prospice.gamepadplacement.cfg`. Keep `Diagnostics.RunSelfTests=false`; a surrounding game process can accept menu input and reset/write saves even when diagnostic fixtures themselves are temporary. Automated game testing requires an independently verified disposable user profile.

Phase 2 cargo belts also require a private surface cache generated from your own installed game. Use the source repository tools/build-cargo-cache.py and tools/check-cargo-cache.py with the pinned analysis dependencies, or run its source installer which performs both steps. The cache stays in BepInEx/cache and must not be redistributed. A missing cache leaves cargo packing native and logs a warning; ordinary placement still works.

Defaults: Cargo.EnableFlatBelts=true, Enabled=true, ShowTruckStorageGrid=true, TriggerEnter=0.55, TriggerRelease=0.35, StickDeadZone=0.18, MoveMetersPerSecond=0.5. The ZIP includes no user configuration, so upgrades do not overwrite preferences.

## Controls

Carry ordinary cargo, artifacts, toolboxes or supplies. Return LT to neutral, then hold LT to preview. Left stick rotates in 90-degree grid-relative steps; return it to neutral between turns. Right stick retains camera look. RT + left stick positions the item; on truck grids vertical offset is ignored and the item seats above verified floor/cargo support. Release LT to place a cyan preview. A red preview remains held. B cancels; X/mapped Use performs the ordinary drop. Walking is stopped during preview. Winch anchors and deployment tools retain native behavior.

While on foot and empty-handed, look at an exposed cargo deck or roof rack within interaction reach. The native panel shows **Secure cargo** and its button. Right-stick click (**R3**) is preferred; enabled player and system mappings are checked, with RB, LB, Y and D-pad directions as fallbacks. If all candidates are assigned, the action stays hidden. Release and press the displayed button to re-secure the targeted surface, including newly added loads. Blocked views, carried items, menus and vehicle control suppress the action. Existing belts are released and rebuilt on the next frame so deferred joint destruction cannot exclude old cargo.

## Changes and limits

This revision applies StickDeadZone to rotation rearming as well as positioning, rolls back partially installed hooks on startup failure, and removes the driver and pending diagnostics on unload.

The build includes a faint cyan storage grid, filled cyan/red previews under scene effects, shared 6.25 cm grid spacing, flat grid-zero entry, exact quarter turns and storage-floor checks covering mesh and collider bounds. Missing floor support, blocking overlaps, walls and normal interaction reach prevent release. The cargo grid clears the rendered floor and has half the original opacity.

The passing standalone check count is recorded from the packaging run in manifest.json. Prior native integration evidence is from 0.3.0; 0.3.3 has not been launched for automated runtime testing. Check bed/rack appearance, controller controls, rotated items, stacks, tilted/moving supports, B/X cancellation and LT release yourself. Mesh bounds are conservative. Floor guards apply to preview and commit; subsequent native gravity, collision simulation and truck packing are unchanged. No save-format changes are introduced.

## Remove or roll back

Close Prospice, then remove only `BepInEx/plugins/Prospice.Placement.dll`, or replace it with your backed-up earlier DLL. Keep the old DLL outside the plugins directory to avoid loading both versions. Configuration can remain. To bypass the whole loader, set `enabled=false` under `[General]` in doorstop_config.ini. Removing this mod does not require restoring a save or replacing game binaries.

## Verification

manifest.json identifies the exact source commit, compatibility, acceptance state, and payload hashes/sizes. SHA256SUMS.txt covers every payload file and the manifest. The ZIP was extracted into a fresh directory and checked for unexpected paths and hash differences before delivery.

## Paint candidate

0.5.0 includes embedded original bucket/sprayer models and color textures. Run the source installer to generate and verify the additional private paint mesh cache. Paint purchases use the native shop and game currency; graffiti and paint amounts use exact-native-checkpoint sidecars in ProspicePaint beside the save. Preserve that folder in backups. Disable Paint.Enabled to stop paint integration. Native runtime/controller/visual acceptance remains pending; Blender renders are asset previews only.
