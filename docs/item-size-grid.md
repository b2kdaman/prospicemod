# Shared grid calculation

The selected grid step is **0.0625 m (6.25 cm)** for every item. It never changes with the held item. Items occupy a whole number of cells; yaw/pitch quarter-turns update their footprint dimensions.

`tools/measure-items.py` reads every serialized asset in this installed build's `data.unity3d`, resolves local and external mesh references, finds ordinary Pickupable/Packable rigidbodies, excludes special deployment components, and measures enabled non-trigger collider bounds in the item's authored root scale. It applies nested collider transforms. Mesh/curved-shape bounds are conservative; runtime placement still uses actual collider geometry. No game assets or metadata are modified.

The scan found **46 item records**, **16 distinct XYZ size profiles**, and **zero unresolved mesh references**. Dimensions range from approximately 0.223 to 2.58 metres. Repeated lost-cargo instances and differently named items with the same size count only once when choosing the step. The separate addressable bundles in this build contain localization assets.

For each candidate step s and each measured dimension d, calculate the fractional footprint padding `(ceil(d / s) * s - d) / d`. Choose the coarsest tested step with mean padding at most 5% and worst padding at most 25%. These thresholds are an explicit implementation choice to balance useful grid spacing against wasted space; there is no exact common divisor for the irregular collider sizes.

| Candidate | Mean padding | Worst padding | Meets thresholds |
| --- | ---: | ---: | --- |
| 5 cm | 3.26% | 18.58% | Yes |
| **6.25 cm** | **3.83%** | **23.52%** | **Yes, coarsest** |
| 10 cm | 7.65% | 34.53% | No |
| 12.5 cm | 6.74% | 48.22% | No |
| 15 cm | 11.42% | 43.31% | No |
| 20 cm | 14.91% | 79.37% | No |
| 25 cm | 15.14% | 97.63% | No |

The common 0.375, 0.75 and 1.5 m dimensions span exactly 6, 12 and 24 cells. A 0.223 m fuel-can thickness spans 4 cells (0.25 m); its collision geometry remains unchanged.

[All measured item records](item-dimensions.csv) include the source asset, name, XYZ dimensions, bounds-approximation flag and unresolved-reference status. Private detailed scan output and candidate calculations are in `local/item-measurements/`. Re-run after a game update; do not treat these static bounds as proof that every prefab has been controller-playtested or that runtime deformations cannot change a footprint.

The surface lattice is centered within each support/storage boundary. The item footprint is rounded up to complete cells, then its collider center is aligned to that span. Exact collision and reach checks still reject invalid fits. For truck highlighting, 25 cm sampling patches are subdivided into the same 6.25 cm visible grid; patches covered by cargo or missing physical truck support are omitted. This conservative surface indication is separate from full held-item fit validation.
