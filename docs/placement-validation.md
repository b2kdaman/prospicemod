# Placement validation â€” 2026-10-12

Build: local playtest version 0.1.0. Final DLL SHA256: `DB338949AB92BF9335A1E3E85DC0FB91B6DFE9EE7F8570F6795700E9BE921B68`.

## Completed automated checks

- Release build: **0 warnings, 0 errors** with SDK 8.0.425.
- Unity-independent state tests: **28 passed**. Covers fresh LT requirement, 0.55/0.35 hysteresis, X/B/release precedence, neutral rearming after cancellation/release/drop, loss of context, symmetric dead zone and one-second rotation at 30/60/144 Hz.
- Tests executed inside the actual game: **27 passed**, with **0 BepInEx errors** in the final validation log. Covers Rewired explicit-type bindings, native release ownership/parent/scale/physics cleanup, mesh-only silhouette construction, individual renderer restoration, original-size support offset, transient collision probes, penetration rejection, surface raycasts, range, adjacent blockers, trigger exclusion, moving support translation, 59/61-degree slopes, shelf height, native refusal, successful commit and second-commit refusal.
- Native post-physics checks measured ordinary drop = forward velocity 1, RT throw = forward velocity 5, placement = velocity 0 on stationary synthetic fixtures with gravity disabled for the measurement. The placement gravity-preservation check is separate and passes with gravity enabled.
- The built plugin loads successfully on the matching game hash. Original encrypted metadata and GameAssembly were not changed.
- Removal/startup check passed: the game reached the main menu with zero plugins and zero BepInEx errors. The identical tested DLL was then reinstalled, Enabled=true and RunSelfTests=false.
- Final SHA256 checks confirmed GameAssembly.dll, original global-metadata.dat, demo.json and settings.json are unchanged from this turn's baseline.

Private evidence is under `local/placement-validation/`: `final-loader.log`, `player-final.log`, `final-result.json`, baseline/after hash manifests and removal logs. Earlier attempts are retained as diagnostics, not treated as passing releases. Two errors caught during development were fixed: hooking a shared empty native method, and a generated Rewired overload collision. Collision queries also required transiently enabled probes in this engine build.

The existing engine warning about Class::Init signature fallback is a known loader baseline. Earlier game logs contain IndoorsTrigger.OnEnable and GameManualButton.OnEnable exceptions; those are not newly attributed to this mod.

## Physical acceptance still required

No joystick was connected during automated validation. Synthetic Unity fixtures establish native cleanup and geometry behavior; they do not establish controller feel, visual quality in the world, every cargo prefab, or truck packing.

1. Start the game with a controller. Before holding LT, check ordinary X pickup/drop and RT throw.
2. Carry cargo, an artifact, a toolbox and an ordinary supply. Let LT return to neutral, then hold it: a cyan/red silhouette should replace the carried mesh.
3. Test right-stick yaw/pitch and RB+stick roll/pitch. The camera should stay steady; walking should still work. Check the displayed hint and transparency against different backgrounds.
4. Release LT over ground and shelves. Check stacked cargo and blocked positions. Red release and B must keep the item held and visible. Check simultaneous X and LT release causes one ordinary drop.
5. Check steep slopes, out-of-range targets, moving truck beds and normal packing after placement. Check collision fit with irregular/convex mesh items. Unreadable mesh bounds may leave conservative extra clearance.
6. Open menus, disconnect the controller, switch to keyboard/mouse, change scene and lose held ownership while previewing. Confirm restoration and a fresh LT press requirement.
7. Confirm winch anchors/deployment tools retain their normal actions and keyboard/mouse pickup/drop/throw remain unchanged.
8. Remove the plugin and repeat baseline controls. Startup removal is automated; actual removal gameplay remains a manual check.

The plugin logs the active interaction reach/drop mask at preview entry and the reason for declined release. Unexpected failures restore rendering and stop placement for that session. Unsupported item geometry only disables preview for that item until ownership changes.

## Carry hint update — 0.1.1

Added “Hold LT to place manually” to the native on-foot HUD, beneath the crosshair. A text-only TextMeshProUGUI component inherits the font, material, color and type styling from ObjectHighlighter.actions[0], under UIMaster.onFootUI. Existing prompt strings/components are not edited or cloned. Native OnFootUI.Update hides its normal roots during camera zoom; the new hint explicitly respects zoom, interface/menu state, ownership, preview state and the existing active-gamepad arbitration. A connected but never-used pad does not show the hint. Scene teardown, driver disable and plugin unload remove the label.

This is an English discovery hint for the physical LT control, independent of remapped Aim labels. It adds no new gameplay hook. The 0.1.0 runtime results above remain historical; visual placement, font size and controller visibility for this update need an in-game controller check.

0.1.1 validation: release build passed with zero warnings/errors; all 28 existing input state checks passed. Installed after the user closed the game. The installer verified the game hash, backed up the previous DLL, and installed the built DLL. Installed SHA256 matches the build: `78536A6FBB9FB47B6369698EFCCC8195E148F637FB76F32A001E70BA6F38F1A5`. No runtime/visual test of the new label has been claimed.

## Native prompts and left-stick placement - 0.2.0 (2026-10-12)

Installed DLL SHA256: `1BDF69B99A5E61B60811FFA37C6E57990B0FD7C00C07A4F2E5C5135F2B9F887E`.

- Release build: zero warnings/errors. The existing 28 input-state tests pass.
- Final actual-game runtime suite: 39 checks passed, zero BepInEx errors. Covers the new native highlighter Harmony/layout path, editing existing TMP text without UI creation, restoration including newer native/localized strings, movement/jump/sprint/look consumption, horizontal inertia removal while preserving gravity, RT X/Y translation, destination-support reacquisition, reach/occlusion/penetration rejection, and the previous native drop/throw/physics/preview regressions.
- One formatter check was explicitly skipped: the hidden startup window had no last-active input device. Native bvv requires that reference; a connected controller alone is insufficient. The label/glyph appearance after real gamepad input remains manual acceptance. Static disassembly verifies the same native formatting helper used for X.
- Earlier formatter fixture attempts failed because main menu has no on-foot RewiredUtils singleton and because no device had activity. These are retained as diagnostic logs, not counted as passing checks. The final fixture supplies the on-foot service only when controller activity exists, restoring the original singleton in finally. Diagnostic mode runs in the background to prevent a hidden startup test from stalling.
- Installed DLL matches the tested build. Previous DLLs are backed up. Enabled=true, RunSelfTests=false, MoveMetersPerSecond=0.5. The owned validation process was closed.
- demo.json remained unchanged. Native startup rewrote Rewired assignments/calibration in settings.json. An existing private settings copy exactly matched the hash recorded before this turn; it was restored after the game closed, with the test-generated file backed up. Both user-data hashes now match the pre-test baseline. No gameplay save was restored.

Evidence: ignored `local/validation-020/verified-loader.log`, `player-acceptance.log`, `result.json`, and the pre-test hash manifest.

### Required controller check for 0.2.0

1. Carry a normal item using the gamepad. Check the existing X prompt panel now includes Hold LT / Place manually; there must be no separate carry label or IMGUI box.
2. Hold LT while walking: horizontal movement should stop immediately. Left stick rotates, RB + left stick rolls; RT + left stick moves sideways and up/down without rotating or throwing. Releasing RT resumes rotation. Neither preview stick should walk or turn the camera.
3. Check small/large items and the native row positioning near screen edges. Native labels are confined to the existing 250 x 30 pixel row masks. Check keyboard takeover and special tools restore original prompts.
4. Release LT at a valid translated pose; elevated poses settle under gravity. Check walls, below-floor motion, reach limits, shelves, stacks and moving truck beds. B/menus/controller loss restore normal controls and rendering. Simultaneous X and LT release must perform one vanilla action, with X precedence.
5. Check contextual native actions (such as using a carried can) retain their original labels and the appended LT hint fits. Validate native controller glyph/Hold formatting after controller activity, including remapped Use/Aim.

## Right-stick camera correction - 0.2.1 (2026-10-12)

Installed DLL SHA256: `AC30EAD8C421E14ECEB9E5ABAF37AE8A1031BC9364C0C4373F858EA2A7B9B780`.

This supersedes the earlier camera-suppression behavior. Right stick now retains native camera movement during placement. Left stick rotates, RB modifies roll, and RT + left stick positions sideways/up/down. Walking, sprinting and jumping remain blocked during preview. Native prompt rows remain unchanged from 0.2.0.

- Release build: zero warnings/errors; all 28 state tests pass.
- Actual-game synthetic suite: 43 passes, zero skips, zero BepInEx errors. Added checks preserve look input while clearing movement, invoke native camera LateUpdate with that look input, and confirm jump callback muting/restoration including exceptional exit cleanup.
- Native input Update now runs normally, retaining sensitivity, inversion, smoothing and look sampling. A scoped jump callback mute prevents native synchronous jumping; the Harmony finalizer restores the callback and clears locomotion. The previous camera LateUpdate suppression patch is removed.
- Native prompt formatting ran with a real last-active mouse reference and returned Use=[E], placement=Hold[LT]. This verifies the native helper execution, not physical controller glyph appearance. One joystick was connected; hardware controls were not exercised.
- Drop/throw/placement velocities, geometry, rendering restoration and native prompt layout regressions pass. The tested DLL matches the installed DLL; Enabled=true and RunSelfTests=false.
- The owned validation process was closed. A fresh settings backup was captured before launch; startup changes were backed up and the exact pre-test settings restored. settings.json and demo.json hashes match the fresh baseline. No gameplay save was restored.

Evidence: ignored `local/validation-021/verified-loader.log`, `player.log`, `state-tests.log`, `result.json`, and before/after user-data hash manifests.

### Controller acceptance for the current build

Carry an ordinary item and hold LT. Move the right stick: the camera should turn with the game's normal sensitivity/inversion while the silhouette follows the aimed surface. Move the left stick: rotate the item without walking; hold RB for roll, or RT for sideways/up/down positioning. Check the existing LT and X prompt rows, valid release, B cancellation, X drop, menu/device cancellation, and that normal movement resumes afterward. The broader physical placement checklist above still applies except for its historical control bindings: use left stick for the item, retain right-stick camera, and expect walking to stop.
## Hollow preview outline - 0.2.2 (2026-10-12)

Installed DLL SHA256: `DC89443D5E7E3CE2E085D90428E0534B2CF62CE9ACBECFC13CEED3224173E970`.

- Replaced the filled ghost with a hollow, two-pixel silhouette outline. Valid remains cyan and invalid remains red. No input or solver behavior changed.
- Release build passed with zero warnings/errors. Final actual-game suite: 49 passes, zero skips and zero BepInEx errors. Player log has no exception/error matches.
- Six added checks verify disabled solid renderers, GPU-rendered transparent centers/backgrounds and thin borders in both colors, exact status color families, and render-target resizing. Exported cyan/red PNGs were visually inspected and show only the item contour. Existing restoration, native drop/throw/placement, collision, movement and prompt checks also passed.
- Both raster passes use a private depth/stencil texture. The final build additionally restores GUI matrices/tint and active graphics target around compositing. Outline resources are disposed with the preview. The 28 state tests last passed for 0.2.1; the state/input code was not changed in this update.
- Installed DLL matches the tested build. Enabled=true; RunSelfTests=false. The owned validation process was closed. Fresh pre-test settings were restored after backing up startup changes; settings.json and demo.json match their pre-test hashes. No gameplay save was restored.

Evidence: ignored `local/validation-022/result.json`, `verified-loader.log`, `player.log`, both `placement-outline-*.png` captures, and user-data hash manifests. The initial successful render test is retained separately; the final log covers the graphics-state cleanup revision.

Physical acceptance: hold LT over several surfaces and turn the camera/item. Confirm the hollow border is legible at the actual game resolution, cyan/red changes correctly, the native HUD remains intact, and release/cancel restores the carried renderer. The border deliberately remains visible over blocked surfaces. Automated GPU fixtures are not an in-world controller playtest.
## Filled preview before scene effects - 0.2.3 (2026-10-12)

Installed DLL SHA256: `67560B7C99A8163648435E738825BBDBD768745F146C06353BD2EC03F9299777`.

- Restored the previous filled translucent preview with cyan/red status colors and opacity 0.32. Removed OutlinePreview and the driver's OnGUI compositing callback. Normal world-layer MeshRenderers and transparent queue 3000 now send the preview through the scene camera before effects, with ordinary depth occlusion.
- Release build: zero warnings/errors. Actual-game synthetic suite: 47 passes, zero skips, zero BepInEx errors and no player-log exception/error matches. Four rendering checks replace the six obsolete outline checks: enabled world renderers, both material status colors/alpha/queue/depth, and absence of OnGUI compositing. Existing placement, input, renderer-restoration and physics checks pass.
- Read-only runtime pipeline inspection found transparentMask=-1 on all three URP world renderer assets; the UI renderer accepts only layer 5 (mask 32), excluding the preview's Default layer 0. Noise and BegottenFX run at event 500, after ordinary transparent rendering; DrawUIAfterPostFX runs at event 600. The preview no longer bypasses these scene effects. Native UI rendering and effect configuration were not altered.
- Installed DLL matches the tested build. Enabled=true and RunSelfTests=false. The owned validation process was closed; fresh pre-test settings were restored after backing up startup changes, and both settings.json and demo.json match the baseline. No gameplay save was restored.

Evidence: ignored `local/validation-023/result.json`, `verified-loader.log` (including renderer/feature settings), `player.log` and user-data hashes. The 28 state tests last passed in 0.2.1; state/input logic remains unchanged. Actual stylization and legibility while carrying items require an in-world controller check; this run verified pipeline placement and synthetic integration, not a gameplay screenshot.
## Storage grid and quarter-turn snapping - 0.3.0 (2026-10-12)

Installed DLL SHA256: `FF65BE485B97BFBF1D92EFE96B6FC4F2E49944512E7030EFEC86FCBC14093AF9`.

- A shared 0.0625 m grid, selected by the reproducible full item inventory in `docs/item-size-grid.md`, applies to all items. 46 serialized ordinary-item records yield 16 distinct collider sizes; no mesh references remain unresolved. The full measured table is in `docs/item-dimensions.csv`.
- Left stick makes one 90-degree yaw or pitch turn per deliberate deflection, requiring neutral between turns. Roll control is removed. RT is positioning-only; right stick remains native camera look. Footprints span whole grid cells. The preview seats its bottom against actual ground, shelves or objects, including when selecting a support height with RT; it does not remain hovering.
- TruckBed components identify the cargo-bed/rack markers. A faint cyan additive grid follows their physical exposed support, omits covered/missing patches, and renders on the world layer before effects. Meshes follow marker transforms between refreshes. The visual surface hint is conservative and does not replace exact held-item collision validation. ShowTruckStorageGrid controls this optional hint.
- Release build: zero warnings/errors. All 35 state tests and 55 native checks passed on the final installed DLL. New checks cover deliberate single quarter-turns at different frame rates, common step across differing/rotated dimensions, whole-cell footprint spans, bottom contact, no hovering after vertical offsets, stacked-object support, and free/occupied/missing truck grid surfaces. Existing drop/throw/placement, input arbitration, native prompts and cleanup regressions pass. Final loader/player logs have no errors or exception matches.
- Config: Enabled=true, RunSelfTests=false, ShowTruckStorageGrid=true. Final DLL equals the build. Test processes are closed.
- Settings were restored from this turn's fresh backup; only controller assignment/calibration keys differed. The gameplay save changed during the first validation launch: its player log shows entry into GameWorld and a native save. It was deliberately preserved, not restored or overwritten. Therefore the aggregate userDataUnchanged result is false; later validation launches stayed at the main menu. No controller-playtest claim is inferred from that first launch.

Evidence: ignored `local/validation-030/` contains the final `result.json`, loader/player logs, state-test output and user-data hash manifests; earlier logs are retained separately. `local/item-measurements/` contains scan details and the candidate-step calculation.

Manual acceptance: while carrying an item, hold LT near the truck bed and roof rack. Inspect subtle grid brightness under current effects, exclusion around cargo, alignment during truck movement, and controller feel. Use neutral-separated yaw/pitch deflections, RT positioning, stacked objects, edges/slopes, B, X and LT release. Item-specific prefabs and actual truck packing still require gameplay acceptance.
## Save-reset incident and recovery - 2026-10-12

The user reported lost progress after 0.3.0 validation. The earlier assumption that preserving the changed save preserved progress was wrong. `local/validation-030/initial-player.log` explicitly shows `MainMenu.ForceNewGame` invoked through `UnityEngine.UI.Button.OnSubmit` and Rewired's UI event module, followed by native save/reset operations. The test process ran against the live user-data profile and could accept menu input. A hidden launch and a separate log file did not provide data isolation.

The active demo.json was a fresh level-1 game. The native save_prereset.json contained the previous level-2 progress, tutorials, stash, collected items and upgrades. With the game closed, all current top-level user-data files and logs were copied to `local/save-recovery-20261011-215829`. The pre-reset save was parsed/validated, copied to demo.json, and the restored file was verified byte-for-byte against the recovery source (SHA256 E125E3A62008440B31843F7BEA65F31718CBE1D9C79395DA108AE54755705C57). The reset save and native backup remain preserved. The game was not launched for recovery verification; in-game confirmation is left to the user.

No further automated game launch may use the live player profile. Future native diagnostics require a verified disposable profile/data path and complete file backups, not merely hashes. RunSelfTests remains false. Synthetic fixture success does not establish save safety for the surrounding game process.
## Cargo-floor grid and exact grid orientation - 0.3.1 (2026-10-12)

- Corrected cargo-grid depth: the rendered bed floor sits 24.872 mm above its collider. Cargo grid lift is now 40 mm, clearing the floor by 15.128 mm. Roof-rack lift remains unchanged at 12 mm. Depth testing and rendering before scene effects remain intact.
- Reduced cyan grid core alpha from 0.18 to 0.09 (half strength), retaining the feathered additive glow and existing color.
- Preview begins horizontal at grid zero. Signed unit axes provide exact 90-degree turns around grid X/Y instead of rotating from the carried item angle around camera axes. Rotation, footprint and displayed lattice share one frame; the frame follows the current target heading, including on release.
- 48 standalone input/orientation checks passed, including 10,000 mixed turns through all 24 cube orientations and several truck headings. Read-only mesh analysis checked 1,617 interior bed-floor samples: the old grid was buried at every point and the revised grid clears every point. These do not simulate rails, arches, occupied patches or a controller playtest.
- Final release build: zero warnings/errors. The last change after the checks only halves visual grid alpha; the final build and installation were reverified. Installer backed up the previous DLL and verified the game hash with Prospice closed. Installed DLL equals the final build.
- No game launch, runtime test, save read/write or settings restoration was performed. RunSelfTests remains false. Current in-game visual/controller acceptance is pending; previous native evidence is historical 0.3.0 evidence.

Manual check: hold LT over an exposed bed floor and the roof rack; verify the faint grid, flat zero pose and square quarter-turns. Rotate the camera and repeat turns; then check RT positioning, cargo stacks, B, X and release. Item contact uses the unchanged physical collider seating; the grid clearance is visual only.
Final installed SHA256: `B36BE9C9E0E357F4818CB9503FBE7BD612A388E6C1239932411DAB5752D6756B`.


## Storage-floor anchoring - 0.3.2 (2026-10-12)

- A selected truck grid anchors the preview to verified floor/cargo support irrespective of requested vertical offset. The downward query spans marker geometry from above, rather than starting at the preview and stopping after interaction reach. Normal camera reach remains a release restriction.
- Centre/corner/edge footprint queries require truck floor support. Full mesh bounds and collider extrema are raised above all sampled floor planes, including cargo-bed visual clearance. No verified floor means a hidden invalid ghost and no drop; partial support or blocking geometry rejects release. The release frame solves again before ordinary cleanup.
- Final standalone suite: 55 checks pass. Added coverage includes query span with very short/long reach, geometry on either side of an off-centre pivot, all 24 orientations above a tilted floor, and rejection of unsafe vertical floor planes. These are numerical/input checks, not Unity physics or controller acceptance.
- Final build: zero warnings/errors. User closed Prospice; installer verified game hash, backed up the old DLL and installed 0.3.2. Built and installed SHA256: 663408818BA70813184E34EBAA5CB8F61621ABA71F64CF663EF3F1EB93FA5DBF.
- No game launch, save access or settings restoration. RunSelfTests=false, Enabled=true, ShowTruckStorageGrid=true. Grid alpha remains 0.09. Native runtime/visual verification remains pending.

Manual acceptance: aim at cargo bed and roof grid; hold RT and push vertical offsets far above/below the floor. Rotate through quarter turns and try large/off-centre items, tilted trucks, partial edges, cargo stacks and out-of-reach grid points. Preview must remain above the floor; invalid releases retain ownership. Confirm placement and subsequent native physics in-game. No claim is made that these guards replace the game's post-release collision simulation.
