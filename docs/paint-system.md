# Paint candidate 0.5.0

Candidate 0.5.0 was installed on 2026-10-12 with the game closed. Source and installed DLL SHA256 match: D78961B324488B9E17C6D79C7BE791BA6932A060B3C0C02AEE790152B27FD0AF. Native gameplay acceptance remains pending. Save backup: game/save-backups/before-paint-install-20261012-142822-7935 (51 files hash-verified). Previous plugin: local/install-backups/20261012-142913-4659. RunSelfTests remains false.

## Equipment and controls

The airbrush is available automatically in inventory with an empty 250 ml tank. Only paint buckets are sold: 25 credits for one liter, in eight colors. Tap D-pad right to cycle empty -> camera -> airbrush -> empty. No purchase or pickup is needed. B/Escape stows the inventory airbrush. Existing airbrush paint, including mixtures, is preserved when migrating a legacy physical sprayer.

Hold left mouse/RT while looking at a bucket to draw paint gradually at 50 ml/s. Different colors blend by volume. Releasing or looking away stops the transfer; release before spraying after looking away. Away from a bucket, RT/left mouse sprays. Hold Use for two seconds away from buckets to discard the tank contents. Native rows show remaining bucket/tank percentages and the tank color.

Inventory airbrush visuals are separate from the native hand slot: empty hands can carry other objects, and the inventory tool does not invoke native physical pickup/drop. Other carried cargo, winch use, placement, menus, focus loss and control loss disarm tool cycling. Selecting the airbrush requires a fresh fire release before spraying. Bucket models/colliders remain twice the initial dimensions, with no handle. Spray size is the original size.

The optional Inventory, Stowed and MixedRgb sidecar fields preserve ownership and paint. Old sidecars remain readable. Missing inventory airbrushes are initialized without altering native save schema or charging currency.

## Verified native camera calls

Read-only disassembly against GameAssembly SHA256 511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D verifies FPActions.bva (0x6AA540) calls scanner and camera teardown; bve (0x6AA6A0) clears camera zoom/overlay and resets currentItem; bvd (0x6AA650) activates the native camera anchor and sets currentItem to Camera. These addresses are evidence only. Implementation calls generated bindings and does not write offsets. The airbrush model uses heldItemTransform and the native camera model layer.

## Assets and persistence

Original Blender assets are embedded in the DLL. The bucket has no wire handle or rivets; its collider matches the shortened body. The sprayer uses a hard-surface steel housing, reinforced front plate, fasteners and metal reservoir. Renders are Blender previews, not game screenshots.

Graffiti uses separate persistent overlay tiles and the private hash-bound world mesh cache. Save sidecars under ProspicePaint beside the native save are keyed by the exact native JSON hash. Back up both. Native save data and sidecars are separate writes, so crash recovery across their write boundary still requires runtime acceptance. Game-derived geometry must stay out of source control and releases.

## Validation and remaining acceptance

Clean build; 269 standalone checks, including 14 single-tap/state-cycle checks and a stowage archive round trip. Original mesh/texture validation and the 546-mesh private cache validation pass. These checks do not prove controller feel, native camera photos, shop purchasing, surface projection, tree swaps, equipment restoration, or save/load behavior. Verify those in an unlocked, backed-up gameplay session before release. Keep RunSelfTests false against the live user profile.

## Gradual filling and color mixing

Pick up the airbrush, aim at a bucket, and hold left mouse (RT on a controller). This works while carrying or equipping it. Transfer is 50 ml/s, capped by remaining bucket paint and the 250 ml tank capacity. Release or look away to stop. After looking away during a fill, release before spraying or throwing, preventing accidental paint use or item throws. Native interaction rows show both percentages and the tank color; mixtures display their RGB hex value.

Different bucket colors blend in proportion to the paint volumes already in the tank and added during the hold. This is a volume-weighted RGB approximation, not a physical pigment simulation. Mixture channels retain floating-point precision until rendering, making the result independent of frame rate. Spraying leaves the ratio unchanged, and draining the tank clears it. Optional MixedRgb channels round-trip through the sidecar; older single-color saves remain readable. Painted overlay pixels use the mixed color directly.

Validation includes timed fills at 30/60/144 Hz, conservation of volume, full tanks, empty buckets, invalid durations, mixed orange from red/yellow, stable mixture during spraying, draining, and mixed-color save round trips. Native held-click behavior and visual acceptance still require an in-game session.

The physical bucket model and collision box are doubled in all dimensions. Spray size is unchanged (2 cm diameter at 10 cm range, 30 cm at 2 m). Paint capacity and consumption are unchanged. The airbrush is free; buckets cost 25.

## Automatic inventory and freeze remediation (pending runtime acceptance)

The airbrush is now granted automatically on load with an empty tank, and has no shop row. Existing saved airbrush paint is retained when migrating the first legacy airbrush into the built-in inventory. Other legacy items are preserved. The inventory model never needs native rigidbody pickup/drop; empty hands actually frees interactions. Only paint buckets are purchased. B/Escape stows the inventory airbrush.

The reported objectCam warning has a native PlayerInteraction.bvs stack in Player.log. It is not proof that the generated bucket preview caused the warning. The built-in airbrush avoids this physical pickup path; the native objectCam warning itself has not been reproduced or claimed fixed.

Painting performance changes: file reads and triangle indexing run in managed background tasks (two pending meshes maximum), including for large static batches. A single oversized mesh remains cached instead of rebuilding on every hit. Full scene enumeration is processed in 2 ms slices; unchanged topology retains geometry patches. Overlay streaming creates at most four new patch visuals per update and retains unchanged visuals/pages. Atlas upload is batched once per dirty page, and pixel arithmetic avoids per-pixel Unity interop. Visibility sampling uses reusable nonallocating ray buffers and 8x8 pixel groups instead of 2x2 groups. Stroke interpolation is capped at four dabs, with hit queries at the 30 Hz paint cadence. Initial surface preparation consumes no paint. Coarser visibility and capped interpolation need visual acceptance near thin occluders and rapid movements.

275 standalone checks and a clean build pass. These are not a measurement of runtime frame time; the reported 10-20 second freeze still requires an in-game recheck on this build.

Airbrush UI visibility: tank/mix/refill/spray rows are only written while equipped. Mode changes restore prior rows first, preventing stale tool UI after stowing. Unequipped bucket inspection retains bucket percentage and pickup only. The viewmodel stays on the native camera held-tool anchor/layer, with its local offset moved from (0.18, -0.18, 0.36) to (0.12, -0.16, 0.22) metres; FOV and camera mode remain native. Exact in-game framing still needs visual acceptance.

## Surface coverage and spray intensity update

Observed runtime log warnings rejected Cube, Plane and Cylinder cache entries and reported no Sphere entry. Bounded readable meshes (up to 65,536 vertices) now copy their actual vertices/indices on the Unity thread and build their index asynchronously, bypassing incompatible private-cache identities. Unreadable meshes still require the matching cache.

A coarse primitive collider belonging to a candidate surface no longer clips that surface's mesh query at the collider's front plane or self-occludes its own paint pixels. Mesh colliders and unrelated blockers retain occlusion. Opposite triangle winding is accepted for hits and patch generation, with the patch normal oriented toward the painted side. These changes address identified causes; buildings/tree/large-surface acceptance remains a runtime check.

Physical spray width retains the original distance curve (2 cm diameter at 0.1 m, 30 cm at 2 m, continuing to grow to 3 m), independent of the doubled physical bucket. Opacity no longer has the inverse-area penalty that made distant spray faint. The center reaches over 99% coverage after 0.1 seconds; softer edges still depend on distance. Consumption stays unchanged. 277 checks pass, including monotonic near/far spread and stronger coverage. The equipped-only UI and closer first-person model are included.

## Nearest-surface latency correction

Triangle hit selection now compares every candidate with the current nearest distance, preventing later triangles in one mesh from replacing a closer hit. Candidates are visited from nearest bounds entry outward. A pending foreground candidate defers painting instead of selecting a loaded surface behind it; pending state is local to the current probe.

Private mesh preparation is keyed by mesh and renderer submesh range. Unrelated index groups are skipped on disk and excluded from BVH construction, while original slot numbers are preserved. Region queries skip nonintersecting renderers before requesting geometry. Partial geometry patches are not cached. The directly aimed collider can register its receiving mesh before the background scene scan completes. Equipped idle aiming prewarms the receiving surface at most ten times per second without consuming paint.

286 standalone checks pass, including nearest-hit order, occluder distance, invalid distances, and partial static-batch ownership. Plugin builds with zero warnings/errors. First-stroke latency and nearest visible surface selection still require native gameplay acceptance; no live-profile diagnostic launch was performed.

## Floating floor-paint correction

The user's paint checkpoint contained 30 tiles on BlankSystems/--GAMEMANAGER--/Reflection Probe/Cylinder. Read-only authored-scene inspection confirmed this is a 100 x 0.01 x 100 scaled primitive under an actual ReflectionProbe, not floor geometry. Surface eligibility now excludes reflection-probe descendants, forced-hidden renderers and shadow-only renderers in scene discovery, direct-hit registration and triangle generation. Existing helper tiles remain in sidecars but cannot resolve to a streamed surface, so they are hidden without deleting paint or changing save data. Normal scenery and movable cargo remain eligible. Renderer.localToWorldMatrix remains in use for static-batch coordinates, consistent with Unity's renderer API documentation.

The correction requires a restart; physical floor-paint acceptance remains a gameplay check.

## LB + right-stick brush controls

While the airbrush is equipped, hold LB and tilt the right stick: up/down increases/decreases alpha (5–100%), right/left increases/decreases brush size (25–200%). No stick click is required. The raw physical stick ignores camera inversion and uses a 25% dead zone. Native HUD shows both percentages. Adjustment blocks spraying, bucket refill and camera look; release fire before spraying again. Ordinary camera look resumes after LB is released. Defaults are 100% for both; settings remain during this session, including stowing, and reset on plugin restart. Size multiplies the existing distance-dependent spread; alpha scales paint deposition, so repeated passes can build opacity. Paint usage remains unchanged.

Build succeeds without warnings/errors; 300 standalone checks pass, including bounds, drift, invalid input and adjustment rate at 30/60/144 Hz. Native controller feel remains a gameplay acceptance check. No game is launched during installation.

## Camera-like airbrush framing and compact HUD

The airbrush now uses the native camera model's parent and camera-relative depth (clamped to 0.18–0.35 m), with a lower-right camera-space position and 1.5x model scale compensated for parent scale. This avoids adding offsets to the generic physical pickup anchor. Inventory cycling no longer calls native physical pickup. HUD rows are shortened to Airbrush/tank %, Spray or Refill/mix, and A/S percentages with short device-specific inputs. Mix hex strings and long combined control descriptions are removed from the constrained native rows. Hold Use still empties the tank. The model framing and native text layout require visual acceptance in game; offline checks do not establish screenshot parity.

## Active view camera and stable native label layout

The prior camera-model-parent approach could inherit the camera tool's disabled anchor. The viewmodel now attaches directly to ObjectHighlighter.mainCamera, uses a depth beyond its near clip, and selects a layer included by that camera's culling mask. Model-only geometry remains separate from inventory physics.

PaintHudLayout temporarily reparents the existing native title/action/input labels to their canvas and positions them in fixed, separate lower-right rows, hiding the target marker while equipped. It restores original parent, sibling order, anchors, size, pose, alignment, wrapping and active state before native Update, on tool reset and context loss. Unequipped target UI retains native positioning. No new text overlays or canvas are created. Build is clean and 300 offline checks pass; actual rendering and ultrawide layout still need gameplay verification.

## Spray sound and live color indicator

Equipped spray has an original synthesized high-frequency noise loop with short volume ramps, routed through the native held-tool mixer group. It is silent while refilling, adjusting, out of paint, preparing an unavailable surface, stowed or paused. The loop source and clip are destroyed with the viewmodel. Side-panel indicators use a per-renderer property block with the tank's actual RGB mixture and a white base texture, replacing the fixed red texture. Empty tanks show neutral gray. Shared bucket materials are unchanged. Native audio/visual acceptance remains pending.
