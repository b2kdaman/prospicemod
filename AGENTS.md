# Agent instructions

## Scope

- Repository: `C:\git\prospicemod`; game: `E:\SteamLibrary\steamapps\common\Prospice Demo`.
- This is a separate mod source/research repository, not a Unity source project. Inspect Git status and preserve unrelated changes.
- Read `docs/verified-drop-loop.md`, `docs/placement-validation.md`, and `docs/metadata-workaround.md` before changing integration. Earlier installation/compatibility reports are historical.
- Current placement controls use left-stick 90-degree yaw/pitch steps (neutral between turns), RT for positioning only, a shared 6.25 cm grid and bottom-to-support snapping, and no walking during preview. There is no roll modifier. Both independent text overlays are removed; reuse native interaction rows.
- Preserve the agreed controls in `docs/placement-spec.md`: LT release places, B cancels, X/mapped Use drops, and right stick retains camera look.

## Verified implementation

- Local playtest plugin 0.3.2 (storage-floor anchoring and mesh/collider floor guards, plus exact grid-relative turns, common-step snapping and filled scene preview) is implemented in `src/Prospice.Placement` and installed in the game's BepInEx/plugins folder.
- Unity 2022.3.62f2, Windows x64 IL2CPP; BepInEx 6.0.0-be.788+5b766a3; Il2CppInterop 1.5.3.0; local SDK 8.0.425; plugin target net6.0.
- Original metadata is GMEV-wrapped. Decoded metadata v31 was recovered read-only; the loader points at the ignored local copy. Do not replace the original game metadata.
- Use generated bindings, verified native semantics and the exact game hash. Never patch guessed addresses or field offsets. Reverify after updates.
- PlayerInteraction.heldRigidbody owns ordinary items. bvs acquires them; bvu(1) drops, bvu(5) throws. Placement invokes bvu(0) with a temporary stationary velocity reference, restores that reference in finally, and applies the preview pose only after ownership clears.
- Do not hook FPController.Update: it shares an empty native body with unrelated methods. The walking guard hooks verified nonempty dai. Native LateUpdate remains unpatched so the right stick turns the camera.
- Do not use the generated zero-argument Rewired GetLastActiveController binding; it collides with a generic overload. Explicit ControllerType overloads were runtime-tested.
- Visible preview geometry contains meshes only. Separate shape probes must be disabled between synchronous penetration queries, including exceptional exits. Never clone gameplay scripts.

## Build and validation

- Source candidate 0.3.3 is reviewed but not installed/published. It honors configured rotation neutral zones, gates partial startup, and cleans up its driver/diagnostics on unload. 64 standalone checks pass; native lifecycle/controller validation is still pending. See `docs/code-review-0.3.3.md`.

- `tools/prepare-release.ps1` builds/tests/packages a clean committed checkpoint, verifies the ZIP round trip and keeps artifacts ignored. Never include game payloads, recovered metadata, generated bindings or user data in releases.
- `tools/build-placement.ps1` builds the plugin; `tools/test-placement.ps1` runs the state tests.
- `tools/install-placement.ps1` verifies the game hash and backs up any replaced plugin. `tools/remove-placement.ps1` archives only this plugin. Both require the game closed.
- Current 0.3.2: 55 standalone checks pass, clean build, installed with game closed. Storage floor queries start above the marker and span its bottom; preview Y offsets do not affect storage seating. Mesh bounds and collider extrema constrain floor contact. No game launch/native runtime test; save files untouched.
- Historical 0.3.1: 48 standalone input/orientation checks and 1,617 offline bed-floor samples pass; clean build. No game launch or native runtime test for 0.3.1. Preview starts horizontal at grid zero; signed unit axes preserve exact quarter turns. Cargo grid clears the authored mesh/collider discrepancy; rack clearance remains unchanged.
- Historical 0.3.0 evidence: 35 state tests and 55 native synthetic checks pass; zero build warnings/errors and zero errors in the final loader/player logs. Shared grid calculation scans 46 records / 16 distinct collider sizes with no unresolved meshes. Physical controller feel and storage-grid appearance remain manual acceptance.
- Physical gamepad feel, full preview workflow, every item prefab and actual truck-bed packing remain manual acceptance. Do not claim synthetic fixtures are a controller playtest.
- Diagnostics.RunSelfTests uses temporary fixtures, but the surrounding game can process menu input and reset/write saves. Keep it false. Never launch automated diagnostics against the live user profile; require an independently verified disposable profile/data directory and complete file backups first. Hidden windows and custom log paths do not isolate saves.
- Existing IndoorsTrigger/GameManualButton exceptions and the loader Class::Init warning are baseline findings. Compare logs rather than attributing all warnings to the plugin.

## Data and deployment discipline

- Keep game binaries, assets, decoded metadata, generated bindings, dependency downloads, saves and logs out of Git. Use ignored local/ or artifacts/.
- Original binaries and save formats must remain unchanged. User data is under `%USERPROFILE%\AppData\LocalLow\CodeWakers\Project Prospice`.
- Never restore a save merely because its hash changed; the user may have played meanwhile. Preserve user progress. On 2026-10-12, diagnostics launched against the live profile allowed a menu OnSubmit to invoke MainMenu.ForceNewGame. The user reported lost progress; the native save_prereset.json was validated and restored with both files backed up in local/save-recovery-20261011-215829. Do not repeat live-profile test launches.
- Only stop a validation process you launched, after checking its PID and executable path. If its log shows gameplay was entered, treat it as potentially taken over by the user and request closure before further replacement. Wait for exit before replacing a DLL.
- Accept game paths as parameters, handle spaces, back up replacements and keep README build/install/removal instructions current.
- Asset inspection remains read-only through `tools/inspect_game.py`; use its pinned `tools/requirements-analysis.txt` dependencies in ignored local/python-deps.
