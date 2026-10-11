# Agreed placement behavior

Status: source candidate 0.3.3; installed/published playtest 0.3.2. The new grid/orientation fixes pass standalone and offline checks; native integration evidence is from 0.3.0. Current in-game visual/controller acceptance remains manual.

## Controls and eligibility

- Ordinary carried cargo, artifacts, toolboxes, and supplies only; exclude winch anchors and other special deployment tools.
- Fresh LT press starts preview while an eligible item is held. Enter at 0.55; release below 0.35.
- Left stick controls yaw/pitch in 90-degree increments. One deliberate dominant-axis deflection makes one turn; neutral is required before the next. There is no roll modifier.
- Hold RT with the left stick to translate sideways relative to the entry camera and vertically in world Y. RT disables rotation while held. Retain the offset when RT is released; reset it when preview ends. Default translation speed 0.5 metres/second. The selected pose always seats the item bottom against actual support. On truck grids, ignore vertical offsets and anchor above the verified storage floor or stacked cargo; outside truck grids, vertical input chooses support height.
- Default stick dead zone 0.18; rotation triggers at 0.55 and requires return to 0.18 or below before another 90-degree step, independent of frame rate. StickDeadZone also configures rotation rearming; activation is max(0.55, StickDeadZone + 0.01), keeping a distinct threshold even for large neutral zones.
- LT release commits a valid placement; invalid release retains the held item.
- B cancels; X cancels preview and performs ordinary drop. X wins over simultaneous LT release.
- After cancellation/drop/invalid release, require neutral LT before rearming.
- Gamepad only. Preserve keyboard/mouse behavior, mapped Use/drop. Stop walking, sprint and jump input during preview, including horizontal inertia; retain gravity. Keep right-stick camera movement and its native sensitivity/inversion. Suppress competing aim/RB/throw behavior only during preview.

## Preview and placement

- Filled translucent item mesh: cyan valid, red invalid, rendered on the normal world layer before the game camera post-processing; no gameplay scripts or after-effects overlay on the preview.
- Raycast from player camera within the verified game interaction reach, ignoring the player, carried item, preview, and trigger-only colliders.
- One shared 0.0625 m grid step, calculated from all serialized ordinary-item collider sizes, applies to every item. Footprints span whole cells and yaw/pitch turns update the span without changing the step.
- During LT preview, a subtle additive cyan grid marks exposed physical truck storage inside native cargo-bed and rack markers, below scene effects. Covered/missing-support patches are omitted. The grid is advisory; exact item collision validation remains authoritative.
- Ground, shelves, other cargo, and truck beds are supported; reject surfaces steeper than 60 degrees from up.
- Begin horizontal at zero rotation relative to the target grid. Apply exact quarter-turns around grid Y and grid X, never carrying/camera axes. Recompute the grid heading as the target support moves; no roll control.
- Use collider geometry for surface offset and reject blocking overlaps. Revalidate the translated pose against camera reach, intervening geometry and the actual support beneath its destination.
- A selected storage grid must never search down from the preview height or fall back to ground beneath the truck. Query from above the storage marker through its bottom, check footprint support and constrain both mesh and collider geometry above floor planes. Unknown floors reject placement and hide the unsafe ghost. Revalidate on LT release. Normal camera reach and obstruction checks still apply.
- Hide the held item's renderers during preview; restore their exact previous states on exit.
- On commit, revalidate and reuse verified normal-drop cleanup exactly once, applying the preview pose without throwing impulse. Preserve gravity and normal truck-bed packing.
- Cancel on menus, controller loss, scene transitions, or lost ownership. Reuse the existing X interaction prompt rows and native button formatter for LT. No independent text label or IMGUI box. Do not change save formats.

## Acceptance checks once compatibility passes

Trace and verify ordinary X pickup/drop separately from RT throw before implementing placement. Exercise grid-relative yaw/pitch, LT release, B cancel, X cancellation precedence, invalid/out-of-range targets, slopes, cargo stacks, shelves, truck beds, and moving supports. Verify all rendering/input/physics states restore after every exit path. Check special tools and keyboard/mouse regressions. Compare runtime exceptions against the documented baseline. Verify disabling/removing the plugin restores normal play. Finish with a real-controller playtest.
