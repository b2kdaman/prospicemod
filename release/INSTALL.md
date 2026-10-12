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

Cargo belts and graffiti require private mesh caches generated from your own installed game. Follow the source repository setup instructions and pinned analysis dependencies, then run tools/build-cargo-cache.py and tools/check-cargo-cache.py, plus tools/build-paint-cache.py and tools/check-paint.py, each with --game-dir pointing at your installation. The source installer also generates and verifies both caches. Caches stay in BepInEx/cache and must not be redistributed. Copying the DLL alone does not set up these features; ordinary placement does not require the caches.

Defaults: Cargo.EnableFlatBelts=true, Enabled=true, ShowTruckStorageGrid=true, TriggerEnter=0.55, TriggerRelease=0.35, StickDeadZone=0.18, MoveMetersPerSecond=0.5. The ZIP includes no user configuration, so upgrades do not overwrite preferences.

## Controls

Carry ordinary cargo, artifacts, toolboxes or supplies. Return LT to neutral, then hold LT to preview. Left stick rotates in 90-degree grid-relative steps; return it to neutral between turns. Right stick retains camera look. RT + left stick positions the item; on truck grids vertical offset is ignored and the item seats above verified floor/cargo support. Release LT to place a cyan preview. A red preview remains held. B cancels; X/mapped Use performs the ordinary drop. Walking is stopped during preview. Winch anchors and deployment tools retain native behavior.

While on foot and empty-handed, look at an exposed cargo deck or roof rack within interaction reach. The native panel shows **Secure cargo** and its button. Right-stick click (**R3**) is preferred; enabled player and system mappings are checked, with RB, LB, Y and D-pad directions as fallbacks. If all candidates are assigned, the action stays hidden. Release and press the displayed button to re-secure the targeted surface, including newly added loads. Blocked views, carried items, menus and vehicle control suppress the action. Existing belts are released and rebuilt on the next frame so deferred joint destruction cannot exclude old cargo.

## Airbrush and paint

Single taps of D-pad right cycle empty hands, camera and airbrush. The airbrush is free and available immediately; only paint buckets are purchased. Eight bucket colors cost 25 SP each and contain 1 litre. The sprayer starts empty and holds 250 ml.

With the airbrush equipped, aim at a bucket and hold RT or left mouse to refill gradually. Drawing from different colors mixes the paint already in the tank. Aim at a paintable surface and hold the same input to spray. Near surfaces receive smaller marks; distant surfaces receive larger, softer marks. Hold LB and move the right stick up/down for opacity or left/right for brush size; no stick click is needed. Release LB to resume normal look/spray. Hold Use for two seconds to empty the tank. B/Escape stows the tool.

The equipped HUD shows tank level, opacity and brush size. Its indicator follows the loaded/mixed color and turns gray when empty. Spraying plays a synthesized hiss. Looking at a bucket shows its remaining paint. Original bucket and hard-surface sprayer assets are embedded in the DLL.

## Changes and limits

0.5.0 adds paint buckets, gradual refill and color mixing, persistent graffiti, distance-dependent spray, controller adjustments, equipped-only HUD, first-person sprayer, spray audio and live color indication. Cargo packing uses contact-supported loads with crossed flat belts, gravity-projected deck anchors and painted hardware on both roof racks and truck beds, including supported stacks.

Placement retains a faint cyan storage grid, cyan/red previews, 6.25 cm spacing, exact quarter turns and support/overlap guards. Unsupported or blocked loads remain loose. Private geometry caches are required for the additional cargo/paint features.

Automated check counts and the exact source commit are recorded in manifest.json. Full native runtime, controller, visual and audio acceptance remains pending for this playtest. Check bed/rack belts, stacks, moving supports, equipment framing, graffiti on world surfaces and controls in normal play. Offline mesh checks and asset renders are not controller or visual acceptance.

Paint amounts and graffiti are saved separately in ProspicePaint beside the native save, keyed to its exact checkpoint. Back up that folder together with the native save; the native save schema remains unchanged. Set Paint.Enabled=false to disable paint integration.

## Remove or roll back

Close Prospice, then remove only `BepInEx/plugins/Prospice.Placement.dll`, or replace it with your backed-up earlier DLL. Keep the old DLL outside the plugins directory to avoid loading both versions. Configuration can remain. To bypass the whole loader, set `enabled=false` under `[General]` in doorstop_config.ini. Removing this mod does not require restoring a save or replacing game binaries.

## Verification

manifest.json identifies the exact source commit, compatibility, acceptance state, and payload hashes/sizes. SHA256SUMS.txt covers every payload file and the manifest. The ZIP was extracted into a fresh directory and checked for unexpected paths and hash differences before delivery.
