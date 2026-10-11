# Verified ordinary pickup/drop loop

Inspected and runtime-tested 2026-10-12 against GameAssembly SHA256 `511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D`. Addresses below are research evidence for this exact build, never patch targets or hardcoded offsets in the plugin. Runtime access uses generated IL2CPP bindings.

## Native control flow

| Stage | Verified method | Native RVA |
| --- | --- | --- |
| Interaction update | PlayerInteraction.Update() | 0x6B6CA0 |
| Use / Fire interaction loop | PlayerInteraction.bvr() | 0x6B6E10 |
| Ordinary pickup interaction | Pickupable.bov(PlayerInteraction) | 0x6B6280 |
| Acquire ordinary rigidbody | PlayerInteraction.bvs(Rigidbody, Vector3, Vector3, bool, Packable) | 0x6B78B0 |
| Shared release | PlayerInteraction.bvu(float) | 0x6B7E00 |
| Packable unpack/cleanup | Packable.bxg() | 0x6B5EF0 |
| Truck-bed operations | TruckBed.cxi(), cxj() | 0x45FF50, 0x460510 |

The first held-item branch in bvr reads Rewired Use and invokes bvu(1). Its second branch reads Fire and invokes bvu(5). The encrypted string helpers were called read-only at runtime and returned `Use` and `Fire`; Rewired independently reports action 31 = Use and 30 = Fire. These are verified rather than inferred from action labels alone. The existing X/Use mapping remains authoritative; the mod does not hardcode physical X as a second drop action.

Pickupable.bov calls bvs at RVA 0x6B6428. bvs clears highlighting/equipped action state, handles packable cleanup, records heldRigidbody and its original localScale in nlw, parents it to handTransform, applies hold offsets/angles and heldScale, sets isKinematic=true, detectCollisions=false and interpolation=None. PlayerHeldItem handles deployment ownership including winch anchors; it is not the ordinary-item ownership reference.

## Ownership and geometry references

The binding exposes PlayerInteraction.heldRigidbody, handTransform, nlw (original scale), nlv (camera), nlx (Rewired player), characterController, inputManager, interactionDistance, dropMask, maxDropAttempts, dropCheckRadius and nly (interaction debounce).

The plugin reads the active component's interactionDistance and dropMask; it does not invent a fixed reach. Actual numeric reach/mask are logged at the first preview in gameplay. No live PlayerInteraction was loaded when the startup inventory ran; a numeric value is deliberately not asserted from the synthetic fixture.

## Release behavior

bvu performs its existing BoxCast/OverlapBox/ComputePenetration checks and can return without releasing. On successful release it:

1. Removes the held rigidbody's transform parent and chooses a normal drop position.
2. Restores its saved local scale from nlw.
3. Sets isKinematic=false and detectCollisions=true.
4. Applies `camera.forward * argument + characterController.velocity * 0.95` using ForceMode.VelocityChange.
5. Sets interpolation=Interpolate and clears heldRigidbody.

The outer bvr sets a 0.5-second nly debounce after its normal release call. The mod mirrors that debounce only after a confirmed successful placement.

Runtime synthetic fixtures verified ownership clearing, parent/scale/physics restoration, native release refusal and post-physics forward velocities of 1 for ordinary drop, 5 for Fire/throw and 0 for placement. They did not simulate a physical X pickup or RT press.

## Placement commit

The mod re-solves the ray and collision target on the LT-release frame. If valid and still owned, it calls bvu(0) exactly once. For that synchronous call only, characterController refers to a temporary stationary controller so the original 0.95 movement contribution also becomes zero. A finally block restores the real reference. This avoids trying to cancel an already queued force by overwriting velocity.

After native ownership actually clears, the mod applies the preview position/rotation, inherits the support rigidbody's point velocity, clears angular velocity and wakes the item. If native release declines, the item stays held. No original collider flags, gravity policy or packable components are replaced. A second commit without ownership is refused.

Pickup already calls Packable.bxg for packed items. Placement leaves normal subsequent TruckBed behavior intact and does not force a joint or mark cargo packed. Automatic/interactive packing of rotated cargo on the actual truck remains a manual acceptance item; the existence of these methods is not a completed truck test.

## Input and renderer integration

- Rewired.et is this build's IGamepadTemplate interface; Rewired.ez supplies the real gamepad template GUID. Physical LT/RT, left stick, RB and B are read through that interface.
- Use is the existing action 31. Explicit ControllerType overloads select active controllers and detect keyboard/mouse takeover.
- The generated zero-argument GetLastActiveController binding collides with a generic overload and throws an AOT exception. The explicit-type overload was runtime-tested and is used instead.
- Harmony hooks PlayerInteraction.Update/OnDisable, FPActions.Update, FPInputManager.Update and FPController.dai. As of 0.2.1, native FPInputManager.Update runs normally to retain right-stick look, sensitivity, inversion and smoothing. Its generated OnJumpPressed field is muted only for that synchronous call and restored by a Harmony finalizer, including exception exits. The finalizer clears movement/sprint/jump fields while retaining LookInput. The verified native FPController.dai movement routine (RVA 0x474210) additionally clears horizontal components of ocp (velocity), ocs (desired velocity), and oct (air momentum), preserving vertical gravity.
- FPController.Update is a shared empty native body and must not be hooked. The walking guard uses nonempty dai; native LateUpdate camera handling is unpatched.
- The filled preview mirrors mesh transforms, with a baked mesh for skinned renderers. Enabled MeshRenderers use the Default world layer, transparent queue 3000 and ordinary depth testing before camera effects. It contains no cloned gameplay scripts or colliders. Renderer enabled flags are restored individually.
- Separate collision probes remain disabled between queries. This Unity build returned no penetration for a permanently disabled probe. Each probe is enabled only across the synchronous ComputePenetration call and disabled in finally, before a physics step can run. Runtime tests verify both overlap rejection and disabled post-query state.
- Primitive support points are geometric. Readable convex mesh vertices supply exact extremal offsets; unreadable meshes use conservative local bounds, which may leave extra clearance. Unsupported nonconvex item colliders are refused safely.

See the official [Rewired template documentation](https://guavaman.com/projects/rewired/docs/ControllerTemplates.html) and [Unity penetration-query API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Physics.ComputePenetration.html). This build's observed behavior and generated bindings determine implementation details.

Private evidence: `local/drop-analysis/` contains API listings and disassembly; `local/placement-validation/` contains runtime logs and baseline hashes. These generated/proprietary artifacts remain ignored by Git.

## Native prompt integration (0.2.0)

ObjectHighlighter.Update (RVA 0x7FC840) projects objectRenderer collider bounds into highlightRect. With no target it moves the rectangle off-screen; attaching a separate label beneath the crosshair does not reuse this workflow. The 0.2.0 plugin instead edits the existing actions[0..1]/inputs[0..1] TMP components. The serialized row masks are 250 x 30 pixels, with the second row 35 pixels below the first. Labels are kept short. There is no cloned/added UI component.

PlayerInteraction.bvv(string, bool), RVA 0x6B8900, supplies native bracket/Hold formatting via RewiredUtils.crs(string), RVA 0x819C00. Use remains mapped; LT stays the physical control even if Aim was remapped. The isolated formatter check runs only when a controller has reported activity; bvv requires a non-null last-active controller. Hidden startup without activity skips this check and leaves visual/gamepad acceptance pending. The main-menu test supplies the RewiredUtils class/player reference normally created in the on-foot scene and restores the previous singleton in finally.

For ordinary held items, the native layout pass receives the held collider temporarily; a Harmony finalizer restores the original objectRenderer reference. Existing contextual target labels are preserved. Only text last written by this mod is restored on cleanup, so a newer native/localized string is not overwritten. Keyboard/mouse, ineligible items, menus, scene changes and plugin unload release the prompt edits. Both prior independent text overlays are removed.

### Right-stick camera correction (0.2.1)

FPInputManager.Update (RVA 0x476590) samples movement, then camera axes with native device sensitivity/inversion, then jump/sprint state. At RVA 0x476A62 it checks JumpInput and calls the generated OnJumpPressed delegate if present. Suppressing the whole method, as 0.2.0 did, also suppresses camera input. Version 0.2.1 instead temporarily mutes that delegate around the native call and clears only locomotion input afterward. Its finalizer restores the original delegate even if native polling throws. The FPController.LateUpdate patch is removed so native camera rotation runs unchanged. Walking inertia remains blocked by the verified dai hook.

### Hollow outline renderer (0.2.2)

The preview keeps the same cyan/red status RGB values and replaces filled surfaces with a two-pixel silhouette border. OutlinePreview draws all item submeshes into a private 24-bit depth/stencil ARGB32 target using the shipped UI/Default shader. Eight projection offsets build an expanded silhouette; the unshifted silhouette removes its interior through stencil masking. A uniform-color quad paints only the remaining border, so item textures and vertex colors cannot tint the status color. No CPU mesh access is needed; non-readable meshes remain supported. The result is an outline of the combined visible geometry, not triangle wireframe.

Only the outline texture is composited during GUI repaint. This adds no text, box or interaction prompt. Render targets, matrices and GUI tint are restored afterward; private targets, materials, command buffers and baked meshes are released at cancellation. The target is recreated on viewport size changes. The outline draws over scene occluders so blocked poses can still be seen in red. Actual placement reach/occlusion/collision checks remain authoritative.
### Filled scene preview (0.2.3)

Supersedes the 0.2.2 outline. The preview again renders actual filled meshes with the original cyan/red RGB and 0.32 opacity. Default world layer 0 places those meshes in the scene renderer, rather than the first-person/UI/after-effects layers. Collision probes remain separately disabled on Ignore Raycast layer 2. The material uses transparent queue 3000, SrcAlpha/OneMinusSrcAlpha, no depth writes and LessEqual depth testing. No GUI callback, private outline target, stencil pass or final-image compositing remains. Camera effects process the preview with the scene; the mod does not alter global effect settings or native UI rendering.
Runtime 0.2.3 inspection confirmed all URP world renderer assets include layer 0 in their transparent masks; the UI renderer only accepts layer 5. Noise and BegottenFX are scheduled at event 500, and DrawUIAfterPostFX at 600. The world preview is drawn before those stages. Full serialized read-only renderer/feature observations are preserved in local/validation-023/verified-loader.log.

### Storage markers and common grid (0.3.0)

Read-only serialized inspection located native TruckBed on level3 Rack (component 10473) and Bed (11519). Both have a unit BoxCollider trigger on the component object; their transforms define storage boundaries. TruckBed.bed for the cargo area references CargoBed (transform 3789), whose physical BoxCollider is separate from the storage trigger. The rack has its own bed/bedrb references. Runtime uses these generated component properties and live colliders; no field offsets, prefab path IDs or hardcoded dimensions enter the gameplay implementation.

TruckStorageGrid reads those markers at preview entry. It samples actual non-trigger truck support, ignores the held item/player, rejects cargo or missing support, and builds softly feathered additive cyan mesh lines on world layer 0. Mesh transforms follow their storage marker and refresh occupancy every 0.35 seconds. Depth testing, no shadows and queue 2999 put it before the preview/effects. It adds no physics or gameplay components and disposes its meshes/material on every preview exit.

GridPlacement wraps the existing geometry solver with a single shared 6.25 cm lattice, calculated from the complete serialized ordinary-item collider inventory. The current rotated collider footprint determines how many cells are occupied, not the grid step. A downward support query seats the item bottom against physical support; reach, sight and penetration are revalidated after snapping. Ground/other objects receive a local surface lattice even though only native truck storage gets a visible grid. The visual truck hint indicates exposed surface area; the full item preview remains the fit authority. See docs/item-size-grid.md for measurement limitations and the explicit candidate-selection criteria.
### Grid frame and cargo-floor correction (0.3.1)

The ordinary release/drop integration is unchanged. The camera hit selects the grid frame before item geometry is evaluated; ItemGrid.Frame supplies the same horizontal axes to the visible lattice, footprint snapping and rotation. Preview entry resets to grid identity. GridOrientation stores signed unit vectors and applies quarter turns about grid X/Y, supporting all 24 cube orientations without accumulated drift or inherited carrying angles. The current support frame is recomputed on each solve, including release.

Read-only asset inspection found CargoBed's floor collider top at local Y=1.262413725, while the rendered floor is Y=1.279 at scale 1.5: a 24.872 mm difference. The previous 12 mm grid lift was below the visible floor. Cargo grid clearance is now 40 mm above collision support (15.128 mm above the visible flat floor); native rack clearance stays 12 mm. Native TruckBed.bed points to a separate assembly for cargo and to its own marker for the rack, so this distinction uses verified object references rather than names/offsets. Depth testing, covered-patch rejection, world rendering before effects and physical item seating remain unchanged.

`tools/check-storage-floor.py` checks 1,617 interior floor samples against the shipped mesh triangles offline. Every old grid point was buried; every revised point clears the floor. This establishes the geometry correction, not visual acceptance around cargo, arches, rails or a moving truck. No game was launched for these checks.

The final 0.3.1 grid core alpha is 0.09, reduced from 0.18 at the user's request; additive cyan color and feathered edges remain unchanged.

### Storage-floor anchoring (0.3.2)

SolveAligned selects the native storage marker before solving item geometry. If the normal interaction ray cannot reach the floor, a ray/marker-bottom-plane intersection may still identify a nearby storage grid; this does not grant placement outside normal reach or through an intervening surface. Grid X/Z snapping ignores the requested Y offset.

SolveStorage queries from above the storage volume through its bottom, using marker bounds instead of the preview position or an interaction-distance-length downward ray. Nine footprint samples require actual truck floor references, preventing fallback to ground below the truck; storage-height filtering separates the selected bed/rack from other truck geometry. Raised cargo support can lift the pose but cannot lower it through the floor. Full collider support functions plus transformed mesh bounds constrain the whole item above every sampled floor plane. Missing all floor samples hides the ghost; partial support, oversized items, overlaps, obstructed sight and out-of-range poses remain invalid. Release recomputes this solution before the unchanged native drop cleanup.

Mesh bounds are conservative for unreadable models. These are preview and release guards; native gravity, later rigidbody simulation and packing remain unmodified. No live runtime validation was performed for this version.
