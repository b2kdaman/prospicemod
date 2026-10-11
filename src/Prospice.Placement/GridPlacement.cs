using UnityEngine;
namespace Prospice.Placement;

// Uses the held item's original-scale collider geometry at its current orientation.
internal readonly record struct ItemGrid(Vector3 Right, Vector3 Forward, float X, float Z, float StepX, float StepZ, int Columns, int Rows, int CellsX, int CellsZ, float BiasX, float BiasZ)
{
    internal const float Step = .0625f; // Calculated from the complete collider inventory; see docs/item-size-grid.md.
    internal static Quaternion Frame(Transform surface)
    {
        var right = Vector3.ProjectOnPlane(surface.right, Vector3.up).normalized;
        if (right.sqrMagnitude < .5f) right = Vector3.right;
        return Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up);
    }
    internal static ItemGrid Create(Preview preview, Collider surface)
    {
        var frame = Frame(surface.transform);
        var right = frame * Vector3.right;
        var forward = frame * Vector3.forward;
        float Min(Vector3 axis) => preview.Shapes.Min(c => PlacementSolver.Minimum(c, axis));
        float xmin = Min(right), xmax = -Min(-right), zmin = Min(forward), zmax = -Min(-forward);
        float sx = Step, sz = Step;
        int cellsX = Mathf.Max(1, Mathf.CeilToInt((xmax - xmin) / Step - .0001f)), cellsZ = Mathf.Max(1, Mathf.CeilToInt((zmax - zmin) / Step - .0001f));
        var box = surface.TryCast<BoxCollider>();
        var points = new List<Vector3>();
        for (int i = 0; i < 8; i++)
        {
            var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
            points.Add(box != null ? box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, sign)) : surface.bounds.center + Vector3.Scale(surface.bounds.extents, sign));
        }
        float loX = points.Min(p => Vector3.Dot(p, right)), hiX = points.Max(p => Vector3.Dot(p, right));
        float loZ = points.Min(p => Vector3.Dot(p, forward)), hiZ = points.Max(p => Vector3.Dot(p, forward));
        int columns = Mathf.FloorToInt((hiX - loX) / sx), rows = Mathf.FloorToInt((hiZ - loZ) / sz);
        return new(right, forward, loX + ((hiX - loX) - columns * sx) * .5f, loZ + ((hiZ - loZ) - rows * sz) * .5f,
            sx, sz, columns, rows, cellsX, cellsZ, (xmin + xmax) * .5f, (zmin + zmax) * .5f);
    }
    internal Vector3 Center(int x, int z) => Right * (X + (x + CellsX * .5f) * StepX) + Forward * (Z + (z + CellsZ * .5f) * StepZ);
    internal Vector3 Snap(Vector3 point)
    {
        if (Columns == 0 || Rows == 0) return Right * X + Forward * Z;
        return Center(Mathf.Clamp(Mathf.RoundToInt((Vector3.Dot(point, Right) - X) / StepX - CellsX * .5f), 0, Mathf.Max(0, Columns - CellsX)),
            Mathf.Clamp(Mathf.RoundToInt((Vector3.Dot(point, Forward) - Z) / StepZ - CellsZ * .5f), 0, Mathf.Max(0, Rows - CellsZ)));
    }
    internal Vector3 RootXZ(Vector3 center) => center - Right * BiasX - Forward * BiasZ;
}

internal static class GridPlacement
{
    internal static bool Ignore(Collider c, Rigidbody item, Transform player) => !c || c.isTrigger || c.attachedRigidbody == item || c.transform.IsChildOf(item.transform) || c.transform.IsChildOf(player);
    internal static RaycastHit? Below(Vector3 from, float distance, Rigidbody item, Transform player)
    {
        RaycastHit? nearest = null;
        foreach (var hit in Physics.RaycastAll(from, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
            if (!Ignore(hit.collider, item, player) && (nearest == null || hit.distance < nearest.Value.distance)) nearest = hit;
        return nearest;
    }
    internal static Vector3 Seat(Preview preview, Vector3 rootXZ, RaycastHit support)
    {
        var n = support.normal;
        float min = preview.Shapes.Min(c => PlacementSolver.Minimum(c, n));
        rootXZ.y = (Vector3.Dot(support.point, n) + .005f - min - Vector3.Dot(rootXZ, n)) / n.y;
        return rootXZ;
    }
    internal static bool Clear(Preview preview, Rigidbody item, Transform player)
    {
        var bounds = PlacementSolver.BoundsOf(preview.Shapes);
        foreach (var other in Physics.OverlapBox(bounds.center, bounds.extents + Vector3.one * .005f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            if (Ignore(other, item, player)) continue;
            foreach (var shape in preview.Shapes)
                if (PlacementSolver.Penetrates(shape, other, out var depth) && depth > .001f) return false;
        }
        return true;
    }
    internal static PlacementTarget SolveAligned(Preview preview, Rigidbody item, Transform player, Camera camera, float reach,
        int mask, GridOrientation orientation, Vector3 offset, IReadOnlyList<TruckBed> beds)
    {
        // Resolve the lattice from the camera hit before using item geometry; carrying angles and
        // off-centre item pivots must not choose the grid's zero heading.
        preview.Root.SetActive(true);
        var hit = PlacementSolver.FindSurface(preview, item, player, camera, reach, mask);
        BoxCollider? marker = hit != null ? TruckStorageGrid.MarkerFor(beds, hit.Value.collider, hit.Value.point) : null;
        var aimed = hit != null ? hit.Value.point : Vector3.zero;
        if (!marker)
        {
            var projected = TruckStorageGrid.AimedMarker(beds, camera, reach, out var point);
            // A nearer unrelated surface must not activate a grid behind it.
            if (projected && (hit == null || Vector3.Distance(camera.transform.position, point) <= hit.Value.distance + .05f))
            { marker = projected; aimed = point; }
        }
        Collider? lattice = marker ? marker : hit?.collider;
        var f = orientation.Forward; var u = orientation.Up;
        var local = Quaternion.LookRotation(new Vector3(f.X, f.Y, f.Z), new Vector3(u.X, u.Y, u.Z));
        var rotation = (lattice ? ItemGrid.Frame(lattice!.transform) : Quaternion.identity) * local;
        if (marker)
        {
            var bed = TruckStorageGrid.BedFor(beds, marker!);
            if (bed != null) return SolveStorage(preview, item, player, camera, reach, rotation, aimed + offset, marker!, bed);
        }
        return Solve(preview, item, player, camera, reach, mask, rotation, offset, beds, lattice);
    }
    static PlacementTarget SolveStorage(Preview preview, Rigidbody item, Transform player, Camera camera, float reach,
        Quaternion rotation, Vector3 desired, BoxCollider marker, TruckBed bed)
    {
        preview.Pose(Vector3.zero, rotation);
        var grid = ItemGrid.Create(preview, marker);
        var center = grid.Snap(desired); // Y is deliberately ignored: storage always seats from above.
        var root = grid.RootXZ(center);
        var range = StorageFloorMath.QueryRange(marker.bounds.min.y, marker.bounds.max.y, reach);
        var floors = new List<RaycastHit>();
        var supports = new List<RaycastHit>();
        bool complete = true;
        // Probe the centre, corners and edge midpoints of the cell footprint. Never seek a
        // floor below the preview: a negative RT offset must not select ground under the truck.
        foreach (float x in new[] { -.499f, 0f, .499f }) foreach (float z in new[] { -.499f, 0f, .499f })
        {
            var p = center + grid.Right * (x * grid.CellsX * ItemGrid.Step) + grid.Forward * (z * grid.CellsZ * ItemGrid.Step);
            var from = new Vector3(p.x, range.top, p.z);
            RaycastHit? floor = null, support = null;
            foreach (var h in Physics.RaycastAll(from, Vector3.down, range.distance, ~0, QueryTriggerInteraction.Ignore))
            {
                var c = h.collider;
                if (Ignore(c, item, player) || h.normal.y < .5f) continue;
                bool inStorageHeight = h.point.y >= marker.bounds.min.y - .5f && h.point.y <= marker.bounds.max.y + .3f;
                bool cargo = c.GetComponentInParent<Packable>() || c.GetComponentInParent<Pickupable>();
                if ((inStorageHeight || cargo) && (support == null || h.distance < support.Value.distance)) support = h;
                if (inStorageHeight && (c.attachedRigidbody == bed.bedrb || c.transform.IsChildOf(bed.bed)) &&
                    !c.GetComponentInParent<Packable>() && !c.GetComponentInParent<Pickupable>() &&
                    (floor == null || h.distance < floor.Value.distance)) floor = h;
            }
            if (floor == null) { complete = false; continue; }
            floors.Add(floor.Value);
            if (support != null) supports.Add(support.Value);
        }
        if (floors.Count == 0)
        {
            // No verified truck bottom: do not show a ghost below it or fall back to ground.
            preview.Root.SetActive(false);
            return new(false, root, rotation, null, "No verified storage floor");
        }
        float Required(RaycastHit h, float clearance)
        {
            var n = h.normal; var p = h.point;
            return StorageFloorMath.RootHeight(n.x, n.y, n.z, p.x, p.y, p.z,
                preview.MinimumIncludingMesh(n), root.x, root.z, clearance + .005f);
        }
        float y = float.NegativeInfinity;
        var actualSupport = floors[0].collider;
        float lift = TruckStorageGrid.SurfaceClearance(bed, marker);
        foreach (var floor in floors) y = Mathf.Max(y, Required(floor, lift));
        foreach (var support in supports)
        {
            float needed = Required(support, .005f);
            if (needed > y) { y = needed; actualSupport = support.collider; }
        }
        root.y = y;
        preview.Pose(root, rotation);
        // Check the posed visible mesh AND collision geometry against every floor plane.
        bool above = floors.All(h => preview.MinimumIncludingMesh(h.normal) >= Vector3.Dot(h.point, h.normal) + lift + .004f);
        bool fits = grid.Columns >= grid.CellsX && grid.Rows >= grid.CellsZ;
        string reason = !complete ? "Storage floor does not cover item footprint" : !fits ? "Item footprint exceeds storage area" : !above ? "Item crosses storage floor" : "Snapped to storage bottom";
        bool valid = complete && fits && above && float.IsFinite(y);
        var bounds = PlacementSolver.BoundsOf(preview.Shapes);
        var sight = bounds.center - camera.transform.position;
        if (sight.magnitude > reach + .005f) { valid = false; reason = "Out of reach"; }
        foreach (var h in Physics.RaycastAll(camera.transform.position, sight.normalized, sight.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!Ignore(h.collider, item, player)) { valid = false; reason = "View blocked"; break; }
        if (!Clear(preview, item, player)) { valid = false; reason = "Blocked"; }
        return new(valid, root, rotation, actualSupport, reason);
    }
    internal static PlacementTarget Solve(Preview preview, Rigidbody item, Transform player, Camera camera, float reach, int mask, Quaternion rotation, Vector3 offset, IReadOnlyList<TruckBed> beds, Collider? lattice = null)
    {
        var raw = PlacementSolver.Solve(preview, item, player, camera, reach, mask, rotation);
        if (!raw.Support || raw.Reason == "Surface steeper than 60 degrees") return raw;
        var rawBounds = PlacementSolver.BoundsOf(preview.Shapes);
        var desired = new Vector3(rawBounds.center.x, rawBounds.min.y, rawBounds.center.z) + offset;
        preview.Pose(Vector3.zero, rotation);
        // When alignment already chose a lattice, use that exact storage marker for fit
        // checks too; off-centre geometry must not silently switch coordinate frames.
        var marker = lattice ? (lattice!.isTrigger ? lattice.TryCast<BoxCollider>() : null)
            : TruckStorageGrid.MarkerFor(beds, raw.Support!, desired);
        var layout = ItemGrid.Create(preview, lattice ? lattice! : marker ? marker! : raw.Support!);
        if (marker && (layout.Columns < layout.CellsX || layout.Rows < layout.CellsZ))
        {
            preview.Pose(raw.Position, rotation);
            return raw with { Valid = false, Reason = "Item footprint exceeds storage area" };
        }
        var center = layout.Snap(desired);
        var below = Below(new Vector3(center.x, desired.y + .08f, center.z), reach, item, player);
        if (below == null || below.Value.normal.y < .5f)
        {
            preview.Pose(raw.Position, rotation);
            return raw with { Valid = false, Reason = "No suitable snapped support" };
        }
        var surface = below.Value;
        var position = Seat(preview, layout.RootXZ(center), surface);
        preview.Pose(position, rotation);
        var bounds = PlacementSolver.BoundsOf(preview.Shapes);
        var sight = bounds.center - camera.transform.position;
        if (sight.magnitude > reach + .005f) return new(false, position, rotation, surface.collider, "Out of reach");
        foreach (var hit in Physics.RaycastAll(camera.transform.position, sight.normalized, sight.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!Ignore(hit.collider, item, player)) return new(false, position, rotation, surface.collider, "View blocked");
        return new(Clear(preview, item, player), position, rotation, surface.collider, "Snapped to grid and support");
    }
}
