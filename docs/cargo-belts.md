# Cargo belts — Phase 2 (0.4.1)

## Behavior

Vehicle entry now runs contact-based packing for the cargo bed and roof rack. Eligible, non-held Packable rigidbodies are deduplicated across their enabled non-trigger colliders. A sampled support polygon must contain the gravity-projected center of mass; stable stacks resolve from the deck upward. Four reachable anchors are required before creating the item's fixed joint. Unsupported loads, blocked routes and unreachable overhangs stay loose.

Two matte dark-gray ribbons cross perpendicularly at the item center. Their taut upper envelopes clear sampled collider and render geometry, including underlying supported cargo. The descending sections follow actual `Physics.gravity`: rays from the outer shoulders must reach an upward-facing deck surface inside the storage footprint. They do not aim toward arbitrary nearest points or use the deck normal as gravity. A small center keeper, D-ring-style loops, mounting plates and bolt heads are procedural primitive geometry with no gameplay colliders.

Hardware reuses the selected deck material's texture region using triangle UVs and local UV gradients. Material properties and property blocks refresh every 0.25 seconds, while `_Bend` is disabled on the hardware's private material copies to prevent the truck's deformation shader from distorting connector geometry. The 35 mm-wide, 2 mm-thick ribbons narrow for small cargo (12 mm minimum). Both crossings share a height with 3 mm separation between straps.

Packing preserves cargo poses and uses a FixedJoint directly to the storage rigidbody. Native infinite break thresholds and disabled preprocessing are retained. Native pickup, exit, unloading, disable and tipping triggers release cargo; removing a necessary support releases its dependent loads. Runtime checks also release a load if its joint disappears or its pose moves materially away from the attachment pose. Changes to supported geometry require a new packing cycle. Generated resources are destroyed on release and plugin shutdown.

## Manual secure (0.4.1)

While on foot and empty-handed, look at an exposed cargo deck or roof rack within interaction reach. The native panel shows **Secure cargo** and its button. Right-stick click (**R3**) is preferred; enabled player and system mappings are checked, with RB, LB, Y and D-pad directions as fallbacks. If all candidates are assigned, the action stays hidden. Release and press the displayed button to re-secure the targeted surface, including newly added loads. Blocked views, carried items, menus and vehicle control suppress the action. Existing belts are released and rebuilt on the next frame so deferred joint destruction cannot exclude old cargo.

The same gravity/support solver used by vehicle entry handles manual secure. No controller mappings are rewritten. The input gate requires neutral after aim, target, controller or binding changes and permits one rebuild per fresh press.

## Integration evidence

Verified by read-only native disassembly for GameAssembly SHA256 `511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D`:

- `TruckBed.cxi`, RVA `0x45FF50`: overlap/tag selection; create FixedJoint; set connectedBody; set enablePreprocessing=false; call CargoRopeWrapper.ctx with one collider; register Packable in obb and set packed.
- `TruckBed.cxj`, RVA `0x460510`: clear the bed flag, enumerate obb, invoke bxg for packed items, then clear the list. Managed hooks must not mutate obb during this enumeration.
- `Packable.bxg`, RVA `0x6B5EF0`: clear packed, set joint breakForce/breakTorque to 0.1, destroy the joint and rope objects, clear references.
- `TruckBed.OnDisable`, RVA `0x45FEC0`: tail-call cxj; no separate disable detour is needed.
- `CarTipping.FixedUpdate`, RVA `0x83F570`: release above 45 degrees while forceResetter.checkTimer <= 0. Native code rechecks truckBed.packed after clearing it and can skip the roof rack. A postfix applies the same policy to owned replacement roof cargo only.

Runtime integration uses generated methods and fields, never these addresses. Existing game-hash gating precedes all hook installation.

## Private surface cache

The shipped Bed mesh is non-readable and its visible flat floor sits about 24.88 mm above the physical collider. The source installer runs `tools/build-cargo-cache.py` and `tools/check-cargo-cache.py` before replacing the DLL. They read installed assets without modifying them and generate `BepInEx/cache/prospice-cargo-511B68EBDE54.bin`.

The cache contains geometry/UVs only, excludes static world batches, and is private game-derived data. Never commit or redistribute it. Textures remain the game's live textures. Runtime lookup verifies the file version/game hash and matches meshes by name, vertex count, submesh counts and bounds. Query meshes are created lazily; detached query colliders are disabled except during direct synchronous raycasts and never join gameplay rigidbodies.

Missing/mismatched cache data or unavailable deck geometry produces one warning per surface and falls back to native packing. Set `[Cargo] EnableFlatBelts = false` to release managed attachments and use native packing on the next entry. Source installation requires Python and the repository's pinned analysis dependencies. Removing the DLL leaves the inert private cache, which can optionally be removed while the game is closed.

## Validation

- Release build: zero warnings and errors.
- 112 standalone checks pass: 64 existing placement/lifecycle checks, 28 cargo checks and 20 manual-secure input/button-selection checks. Cargo coverage includes gravity direction, tilted planes, degenerate rays, contact polygons, rotated/compound support, overhang, stacks, support removal, blocked routes, dependency cycles, repeated solver cycles, and 100 randomized envelope profiles.
- Private cache: 386 unambiguous meshes, 50,307,979 bytes; all coordinates finite, triangle indices and file lengths valid.
- 561 rays through the actual authored Bed triangles across three gravity-relative attitudes hit the visible floor and return varying UV coordinates. The floor/collider discrepancy matches the prior inspection.

These are offline checks. They do not establish Unity hook execution, runtime mesh cooking, in-game render appearance, controller feel or vehicle-driving acceptance. No game was launched and no save was read or written for these tests.

Manual acceptance: with a verified disposable profile, test small supplies, irregular objects, rotated crates, stable/unstable stacks, dense loads, both storage surfaces, tilted trucks and patterned skins. Check manual secure on both storage surfaces, newly added cargo, repeated presses, held-button aim entry, remapped/fully occupied controllers, obstruction and prompt restoration. Check vehicle entry/exit, repeated packing, pickup of a stack support, tipping above 45 degrees, scene changes and disabling the feature. Confirm no clipped or floating hardware, stale skin colors, orphaned visuals or duplicate joints. Keep Diagnostics.RunSelfTests=false on the live profile.

## Local installation

Installed with the game closed on 2026-10-12. Installed DLL SHA256 matches the build: `3B1DFD793157D2B3C2E13CEF3CC2B70B9E6883174FE1CACC50EDF5DA6DDDBFDF`. Previous DLL backed up under `local/install-backups/20261012-111956-0572`. Existing configuration was preserved and RunSelfTests remains false. No release was packaged or published.
