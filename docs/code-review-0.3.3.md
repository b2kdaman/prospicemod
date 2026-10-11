# Code review and cleanup - source candidate 0.3.3

Reviewed 2026-10-12 using the requested review-agent defect-first checklist. Target: initial implementation commit `8482cb0` and its current source, tests and build/release tooling. Read the native drop-loop and validation evidence before changing integration. The gameplay review covers input, state transitions, native hooks, commit, geometry, rendering and cleanup. Research tools were inspected statically; metadata recovery and game diagnostics were not executed.

The findings below describe the reviewed 0.3.2 code and are resolved in the working tree. Paths/lines refer to that base commit. No P0/P1 finding was established; native runtime gaps remain explicit.

## Resolved findings

### [P2] Use the configured neutral zone for rotation - src/Prospice.Placement/PlacementGate.cs:40

QuarterTurnInput hardcoded 0.18 while StickDeadZone was exposed as the left-stick setting. A controller resting at 0.25 with the setting raised to 0.30 could translate normally but could never rearm rotation. Rotation now receives the same configured neutral zone. Activation remains 0.55 by default; settings above it use dead zone + 0.01 so neutral and activation remain distinct. Six new checks cover drift, smaller zones, boundary rearming, full-range activation and repeat prevention.

### [P2] Roll back hooks after partial startup failure - src/Prospice.Placement/Plugin.cs:36

PatchAll can leave earlier hooks installed when a later patch or driver creation throws. The old load path had no rollback, allowing partial input suppression without the complete guards/watchdog. Load now enables placement only after both steps succeed, and attempts teardown before propagating the original failure. A readiness gate keeps incomplete startup inactive.

### [P2] Destroy the update driver on unload - src/Prospice.Placement/Plugin.cs:42

Unload removed Harmony hooks but discarded no driver: the MonoBehaviour created by AddComponent could continue Update and start configured diagnostics after the plugin reported successful unloading. The plugin now retains that component, disables/destroys only it (not its shared host), clears controller state and pending diagnostic fixtures, and removes its hooks. Cleanup errors are reported; unload returns false if a shutdown step fails. A failed diagnostic setup now also cleans force fixtures that may have been created before its first sample was queued.

### [P2] Continue restoring state when cleanup throws - src/Prospice.Placement/PlacementController.cs:165

Cancellation previously disposed the grid, preview and aiming state in one try block. A failure in an earlier operation skipped all later restorations while finally discarded the preview reference. Cleanup now detaches active state first and independently attempts those restorations, collecting errors for logging. The same helper protects plugin teardown so cleanup failure cannot skip unpatching. Three fault-injection checks verify ordering, continuing after multiple errors, and error reporting. This is best-effort cleanup; an individual Unity operation can still fail and is logged.

## Other cleanup

- Removed the unused RB/roll sample and obsolete standalone Hint accessor; controls retain their existing behavior.
- Moved the unchanged native release bridge into GameIntegration.cs so the state machine and game binding have separate files.
- Release packaging reads the passing count from the test run instead of always claiming 55 checks. It rejects missing/ambiguous summaries and nonpositive counts.
- Marked the metadata setup report's pre-implementation statements as historical and linked current native semantics. Updated source, installed and published version distinctions and itch.io publication notes.

## Component map

| File | Responsibility |
| --- | --- |
| Plugin.cs | Compatibility hash/configuration, transactional hook setup, driver lifecycle and native interception |
| GamepadInput.cs | Active Rewired template/device selection and physical gamepad samples |
| PlacementGate.cs | LT/cancel/drop precedence and neutral-separated stick turns |
| PlacementController.cs | Ownership, context, preview state and action suppression |
| GameIntegration.cs | Verified bvu(0) cleanup and applying pose after native ownership clears |
| GridOrientation.cs | Exact signed axes for 24 grid-relative orientations |
| PlacementSolver.cs | Surface selection, shape support functions and penetration checks |
| GridPlacement.cs / StorageFloorMath.cs | Common lattice, support seating, floor constraints, reach and sight |
| Preview.cs / TruckStorageGrid.cs | Mesh-only item preview and exposed truck surface hint |
| CarryHint.cs | Temporary edits to existing native prompt rows |
| Cleanup.cs | Attempt every independent cleanup action and retain errors |
| RuntimeChecks.cs | Opt-in native synthetic fixtures; disposable profile required |

## Validation and remaining acceptance

- **64 standalone checks passed**, including nine new input/cleanup regressions. They test shared production C# without Unity or a game launch.
- Release compilation against the local verified bindings: **0 warnings, 0 errors**.
- Reviewed the full cleanup diff and checked whitespace plus PowerShell/Python script syntax.
- Native startup-failure/unload callbacks, renderer restoration under Unity exceptions, changed dead-zone feel and controller interaction still require a disposable-profile runtime check. Existing floor physics/packing acceptance remains manual. No new native test result is claimed.
- No game launch, installation, save access, settings restoration, release upload, commit or push was performed for this review. The local DLL is a **0.3.3 candidate**; installed and published **0.3.2** artifacts are unchanged.

Before distributing 0.3.3, complete the relevant controller/runtime acceptance, commit the reviewed checkpoint, then use tools/prepare-release.ps1. This review did not bypass that script's clean-checkpoint gate.
