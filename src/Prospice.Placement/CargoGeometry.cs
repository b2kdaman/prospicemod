using UnityEngine;
using Object = UnityEngine.Object;
using V2 = System.Numerics.Vector2;
namespace Prospice.Placement;

internal sealed class CargoFrame
{
    internal readonly Transform Transform;
    internal readonly Vector3 Origin, Right, Up, Forward, Min, Max;
    internal CargoFrame(BoxCollider marker)
    {
        Transform = marker.transform; Origin = Transform.position;
        Right = Transform.right; Up = Transform.up; Forward = Transform.forward;
        Min = Vector3.positiveInfinity; Max = Vector3.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            var p = Local(Transform.TransformPoint(marker.center + Vector3.Scale(marker.size * .5f,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            Min = Vector3.Min(Min, p); Max = Vector3.Max(Max, p);
        }
    }
    internal Vector3 Local(Vector3 p) { var d = p - Origin; return new(Vector3.Dot(d, Right), Vector3.Dot(d, Up), Vector3.Dot(d, Forward)); }
    internal Vector3 World(Vector3 p) => Origin + Right * p.x + Up * p.y + Forward * p.z;
    internal bool Inside(Vector3 p, float inset = .025f) => p.x >= Min.x + inset && p.x <= Max.x - inset && p.z >= Min.z + inset && p.z <= Max.z - inset;
}

internal readonly record struct CargoPaint(Renderer Renderer, int Slot, Vector2 UV, Vector3 Origin, Vector3 U, Vector3 V, Vector2 DU, Vector2 DV)
{
    internal Vector2 Map(Vector3 world) => UV + DU * Vector3.Dot(world - Origin, U) + DV * Vector3.Dot(world - Origin, V);
}
internal readonly record struct CargoAnchor(Vector3 Point, Vector3 Normal, CargoPaint Paint);

// Render-mesh probes are disabled outside a synchronous direct Raycast. They never
// participate in physics simulation and never clone a gameplay component.
internal sealed class CargoMeshQueries : IDisposable
{
    sealed record Probe(MeshCollider Collider, Mesh Mesh, Renderer Renderer);
    readonly List<Probe> probes = new();
    internal CargoMeshQueries(IEnumerable<MeshFilter> filters)
    {
        try
        {
            foreach (var filter in filters)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !filter.sharedMesh) continue;
                var mesh = CargoMeshCache.Readable(filter.sharedMesh);
                var go = new GameObject("Prospice.Cargo.SurfaceProbe"); go.layer = 2;
                go.SetActive(false);
                // A detached probe cannot change the source rigidbody's mass or inertia.
                go.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                go.transform.localScale = filter.transform.lossyScale;
                var collider = go.AddComponent<MeshCollider>(); collider.enabled = false;
                // No convex cooking: retain triangle surfaces and their texture coordinates.
                collider.sharedMesh = mesh; collider.convex = false;
                probes.Add(new(collider, mesh, renderer)); go.SetActive(true);
            }
        }
        catch { Dispose(); throw; }
    }
    internal bool Ray(Ray ray, float distance, out RaycastHit closest, out CargoPaint paint, bool texture = true)
    {
        closest = default; paint = default; float best = distance; bool found = false;
        foreach (var p in probes)
        {
            if (!p.Collider || !p.Renderer) continue;
            p.Collider.enabled = true;
            try
            {
                if (!p.Collider.Raycast(ray, out var hit, best)) continue;
                if (!texture) { best = hit.distance; closest = hit; found = true; continue; }
                int triangle = hit.triangleIndex, slot = 0;
                for (; slot < p.Mesh.subMeshCount - 1; slot++)
                {
                    int count = (int)p.Mesh.GetIndexCount(slot) / 3;
                    if (triangle < count) break;
                    triangle -= count;
                }
                var u = Vector3.ProjectOnPlane(p.Renderer.transform.right, hit.normal).normalized;
                if (u.sqrMagnitude < .1f) u = Vector3.ProjectOnPlane(p.Renderer.transform.forward, hit.normal).normalized;
                var v = Vector3.Cross(hit.normal, u).normalized;
                Vector2 Gradient(Vector3 axis)
                {
                    if (p.Collider.Raycast(new Ray(hit.point + axis * .005f + hit.normal * .02f, -hit.normal), out var neighbor, .04f))
                    {
                        var delta = neighbor.textureCoord - hit.textureCoord;
                        // Do not extrapolate across an atlas seam.
                        if (delta.sqrMagnitude < .01f) return delta / .005f;
                    }
                    return Vector2.zero;
                }
                best = hit.distance; closest = hit;
                paint = new(p.Renderer, slot, hit.textureCoord, hit.point, u, v, Gradient(u), Gradient(v)); found = true;
            }
            finally { p.Collider.enabled = false; }
        }
        return found;
    }
    public void Dispose()
    {
        foreach (var p in probes) if (p.Collider) { p.Collider.enabled = false; Object.Destroy(p.Collider.gameObject); }
        probes.Clear();
    }
}

internal sealed class CargoItemGeometry : IDisposable
{
    internal readonly Packable Item;
    internal readonly Rigidbody Body;
    internal readonly Collider[] Shapes;
    internal readonly CargoMeshQueries Meshes;
    internal readonly int Id;
    internal readonly Vector3 Min, Max, Center;
    internal readonly List<CargoMath.Contact> Contacts = new();
    internal CargoItemGeometry(Packable item, Rigidbody body, CargoFrame frame)
    {
        Item = item; Body = body; Id = item.GetInstanceID();
        Shapes = body.GetComponentsInChildren<Collider>().Where(c => c && c.enabled && !c.isTrigger && c.attachedRigidbody == body).ToArray();
        if (Shapes.Length == 0) throw new InvalidOperationException("Cargo has no physical shapes.");
        float Lo(Vector3 axis) => Shapes.Min(c => PlacementSolver.Minimum(c, axis)) - Vector3.Dot(frame.Origin, axis);
        float Hi(Vector3 axis) => -Shapes.Min(c => PlacementSolver.Minimum(c, -axis)) - Vector3.Dot(frame.Origin, axis);
        Min = new(Lo(frame.Right), Lo(frame.Up), Lo(frame.Forward));
        Max = new(Hi(frame.Right), Hi(frame.Up), Hi(frame.Forward)); Center = (Min + Max) * .5f;
        Meshes = new CargoMeshQueries(body.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponentInParent<Rigidbody>() == body));
    }
    internal bool PhysicalRay(Ray ray, float length, out RaycastHit best)
    {
        best = default; bool found = false;
        foreach (var shape in Shapes)
            if (shape && shape.enabled && shape.Raycast(ray, out var hit, length)) { best = hit; length = hit.distance; found = true; }
        return found;
    }
    internal bool Top(CargoFrame frame, float x, float z, out float y)
    {
        var ray = new Ray(frame.World(new(x, Max.y + .5f, z)), -frame.Up);
        float distance = Max.y - Min.y + 1;
        bool found = PhysicalRay(ray, distance, out var hit); y = found ? frame.Local(hit.point).y : float.NegativeInfinity;
        if (Meshes.Ray(ray, distance, out hit, out _, false)) { y = Mathf.Max(y, frame.Local(hit.point).y); found = true; }
        return found;
    }
    public void Dispose() => Meshes.Dispose();
}

internal sealed class CargoSurface : IDisposable
{
    internal readonly TruckBed Bed;
    internal readonly CargoFrame Frame;
    readonly CargoMeshQueries meshes;
    internal CargoSurface(TruckBed bed, BoxCollider marker)
    {
        Bed = bed; Frame = new(marker);
        meshes = new CargoMeshQueries(bed.bedrb.GetComponentsInChildren<MeshFilter>()
            .Where(f => !f.GetComponentInParent<Packable>()));
    }
    internal bool Owns(Collider c) => c && !c.isTrigger && !c.GetComponentInParent<Packable>() &&
        (c.attachedRigidbody == Bed.bedrb || c.transform.IsChildOf(Bed.bed));
    internal bool Floor(float x, float z, out CargoAnchor anchor)
    {
        anchor = default;
        var ray = new Ray(Frame.World(new(x, Frame.Max.y + .25f, z)), -Frame.Up);
        var hits = Physics.RaycastAll(ray, Frame.Max.y - Frame.Min.y + .75f, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit? physical = null;
        foreach (var hit in hits)
        {
            var p = Frame.Local(hit.point);
            if (!Owns(hit.collider) || !Frame.Inside(p) || Vector3.Dot(hit.normal, Frame.Up) < .75f ||
                p.y < Frame.Min.y - .2f || p.y > Frame.Max.y || (physical.HasValue && hit.distance >= physical.Value.distance)) continue;
            physical = hit;
        }
        if (!physical.HasValue) return false;
        // Query close to the physical support so roof/cab panels cannot masquerade as the deck.
        var support = physical.Value;
        if (!meshes.Ray(new Ray(support.point + Frame.Up * .12f, -Frame.Up), .24f, out var visible, out var paint) ||
            Vector3.Dot(visible.normal, Frame.Up) < .75f) return false;
        anchor = new(visible.point, visible.normal, paint); return true;
    }
    internal bool Verify()
    {
        if (Physics.gravity.sqrMagnitude < .001f || Vector3.Dot(Physics.gravity.normalized, Frame.Up) > -.25f) return false;
        var f = Frame;
        for (int x = 1; x <= 3; x++) for (int z = 1; z <= 3; z++)
            if (Floor(Mathf.Lerp(f.Min.x, f.Max.x, x / 4f), Mathf.Lerp(f.Min.z, f.Max.z, z / 4f), out _)) return true;
        return false;
    }
    internal bool GravityAnchor(Vector3 shoulder, out CargoAnchor anchor)
    {
        anchor = default;
        var gravity = Physics.gravity.normalized;
        System.Numerics.Vector3 V(Vector3 v) => new(v.x, v.y, v.z);
        if (!CargoMath.ProjectGravity(V(shoulder), V(gravity), V(Frame.World(new(0, Frame.Min.y - .25f, 0))), V(Frame.Up), out var projected)) return false;
        float distance = System.Numerics.Vector3.Distance(V(shoulder), projected);
        RaycastHit? first = null;
        foreach (var hit in Physics.RaycastAll(shoulder, gravity, distance, ~0, QueryTriggerInteraction.Ignore))
            if (!first.HasValue || hit.distance < first.Value.distance) first = hit;
        if (!first.HasValue || !Owns(first.Value.collider)) return false;
        var physical = first.Value; var local = Frame.Local(physical.point);
        if (!Frame.Inside(local) || local.y < Frame.Min.y - .2f || local.y > Frame.Max.y || Vector3.Dot(physical.normal, Frame.Up) < .75f) return false;
        if (!meshes.Ray(new Ray(physical.point - gravity * .12f, gravity), .24f, out var visible, out var paint) ||
            Vector3.Dot(visible.normal, Frame.Up) < .75f || !Frame.Inside(Frame.Local(visible.point))) return false;
        anchor = new(visible.point, visible.normal, paint); return true;
    }
    public void Dispose() => meshes.Dispose();
}

internal sealed record CargoRoute(Vector3[] Points, Vector3 WidthAxis, CargoAnchor Start, CargoAnchor End);
internal sealed record CargoPlan(CargoItemGeometry Item, CargoRoute[] Routes, Vector3 Crossing, CargoPaint Paint, float Width, int[] Supports);

internal static class CargoGeometry
{
    internal const float ContactTolerance = .04f;
    internal static void Contacts(CargoSurface surface, CargoItemGeometry item, IReadOnlyDictionary<int, CargoItemGeometry> items)
    {
        var f = surface.Frame; var lo = item.Min; var hi = item.Max;
        var bodyIds = items.Values.ToDictionary(i => i.Body.GetInstanceID(), i => i.Id);
        for (int ix = 0; ix <= 12; ix++) for (int iz = 0; iz <= 12; iz++)
        {
            float x = Mathf.Lerp(lo.x, hi.x, .02f + ix * .96f / 12), z = Mathf.Lerp(lo.z, hi.z, .02f + iz * .96f / 12);
            if (!item.PhysicalRay(new Ray(f.World(new(x, lo.y - .1f, z)), f.Up), hi.y - lo.y + .2f, out var bottom)) continue;
            RaycastHit? nearest = null; int supportId = -1;
            foreach (var hit in Physics.RaycastAll(bottom.point + f.Up * ContactTolerance, -f.Up, ContactTolerance * 2, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider || hit.collider.attachedRigidbody == item.Body || (nearest.HasValue && hit.distance >= nearest.Value.distance)) continue;
                int id = -1;
                if (surface.Owns(hit.collider) && f.Inside(f.Local(hit.point), 0)) id = 0;
                else if (hit.collider.attachedRigidbody && bodyIds.TryGetValue(hit.collider.attachedRigidbody.GetInstanceID(), out int other) &&
                    items[other].Center.y < item.Center.y - .001f) id = other;
                // Even unqualified supports occlude qualified surfaces behind them.
                nearest = hit; supportId = Vector3.Dot(hit.normal, f.Up) >= .5f ? id : -1;
            }
            if (nearest.HasValue && supportId >= 0) item.Contacts.Add(new(supportId, new(x, z), f.Local(nearest.Value.point).y));
        }
    }
    internal static V2 ProjectedCenter(CargoItemGeometry item, CargoFrame frame)
    {
        var com = frame.Local(item.Body.worldCenterOfMass);
        var g = Physics.gravity;
        var gravity = new System.Numerics.Vector3(Vector3.Dot(g, frame.Right), Vector3.Dot(g, frame.Up), Vector3.Dot(g, frame.Forward));
        float floor = item.Contacts.Count > 0 ? item.Contacts.Average(c => c.Height) : item.Min.y;
        if (!CargoMath.ProjectGravity(new(com.x, com.y, com.z), gravity, new(0, floor, 0), System.Numerics.Vector3.UnitY, out var hit))
            return new(float.NaN, float.NaN);
        return new(hit.X, hit.Z);
    }

    internal static CargoPlan? Route(CargoSurface surface, CargoItemGeometry item,
        IReadOnlyDictionary<int, CargoItemGeometry> items, IReadOnlyDictionary<int, CargoPlan> packed)
    {
        var f = surface.Frame;
        var supports = item.Contacts.Where(c => c.Support == 0 || packed.ContainsKey(c.Support)).Select(c => c.Support).Distinct().ToArray();
        var envelope = new HashSet<int> { item.Id };
        void Include(int id) { if (id == 0 || !envelope.Add(id)) return; foreach (int parent in packed[id].Supports) Include(parent); }
        foreach (int id in supports) Include(id);
        float width = Mathf.Clamp(Mathf.Min(item.Max.x - item.Min.x, item.Max.z - item.Min.z) * .12f, .012f, .035f);
        var routes = new CargoRoute[2];
        var sections = new List<V2>[2];
        float centerHeight = float.NegativeInfinity;
        if (!item.Top(f, item.Center.x, item.Center.z, out centerHeight)) return null;
        // Build both sections before routing so their center heights agree on irregular loads.
        for (int axis = 0; axis < 2; axis++)
        {
            bool alongX = axis == 0;
            float center = alongX ? item.Center.x : item.Center.z, cross = alongX ? item.Center.z : item.Center.x;
            var section = new List<V2>();
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (int id in envelope)
            {
                var shape = items[id];
                float crossMin = alongX ? shape.Min.z : shape.Min.x, crossMax = alongX ? shape.Max.z : shape.Max.x;
                if (cross + width * .5f < crossMin || cross - width * .5f > crossMax) continue;
                float a = alongX ? shape.Min.x : shape.Min.z, b = alongX ? shape.Max.x : shape.Max.z;
                int steps = Mathf.Clamp(Mathf.CeilToInt((b - a) / .015f), 8, 160);
                for (int s = 0; s <= steps; s++)
                {
                    float t = Mathf.Lerp(a, b, .001f + .998f * s / steps);
                    for (int edge = -1; edge <= 1; edge++)
                    {
                        float q = cross + edge * width * .5f;
                        if (!shape.Top(f, alongX ? t : q, alongX ? q : t, out var y)) continue;
                        section.Add(new(t, y + .004f)); min = Mathf.Min(min, t); max = Mathf.Max(max, t);
                    }
                }
            }
            if (section.Count == 0) return null;
            sections[axis] = section;
            centerHeight = Mathf.Max(centerHeight, CargoMath.Height(CargoMath.UpperEnvelope(section), center));
        }
        for (int axis = 0; axis < 2; axis++)
        {
            bool alongX = axis == 0;
            float center = alongX ? item.Center.x : item.Center.z, cross = alongX ? item.Center.z : item.Center.x;
            var section = sections[axis].Select(p => new V2(p.X, p.Y + axis * .003f)).ToList();
            float min = section.Min(p => p.X), max = section.Max(p => p.X);
            section.Add(new(center, centerHeight + axis * .003f));
            var hull = CargoMath.UpperEnvelope(section);
            CargoRoute? chosen = null;
            // Gravity projects the two outer shoulders onto the actual deck. A small
            // outward allowance clears the item's sides; longer horizontal detours are rejected.
            foreach (float gap in new[] { .02f, .035f, .05f, .075f, .1f })
            {
                float a = min - gap, b = max + gap;
                Vector3 World(V2 p) => f.World(alongX ? new Vector3(p.X, p.Y, cross) : new Vector3(cross, p.Y, p.X));
                var left = World(new(a, hull[0].Y)); var right = World(new(b, hull[^1].Y));
                if (!surface.GravityAnchor(left, out var start) || !surface.GravityAnchor(right, out var end)) continue;
                var path = new List<Vector3> { start.Point - Physics.gravity.normalized * .016f, left };
                path.AddRange(hull.Select(World)); path.Add(right); path.Add(end.Point - Physics.gravity.normalized * .016f);
                var points = path.ToArray();
                if (!Clear(points, alongX ? f.Forward : f.Right, width, surface, item)) continue;
                chosen = new(points, alongX ? f.Forward : f.Right, start, end); break;
            }
            if (chosen == null) return null;
            routes[axis] = chosen;
        }
        if (!surface.Floor(item.Center.x, item.Center.z, out var centerPaint)) return null;
        return new(item, routes, f.World(new(item.Center.x, centerHeight + .009f, item.Center.z)), centerPaint.Paint, width, supports);
    }
    static bool Clear(Vector3[] points, Vector3 widthAxis, float width, CargoSurface surface, CargoItemGeometry item)
    {
        for (int i = 1; i < points.Length; i++)
        {
            var direction = points[i] - points[i - 1]; float length = direction.magnitude;
            if (length < .0001f) continue;
            for (int edge = -1; edge <= 1; edge++)
            {
                var start = points[i - 1] + widthAxis * (edge * width * .5f);
                foreach (var hit in Physics.RaycastAll(start, direction / length, length, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.collider) continue;
                    // Contacts within a strap's thickness at a cargo seam are allowed.
                    if (hit.collider.attachedRigidbody == item.Body && hit.distance < .006f) continue;
                    if (hit.distance > .006f && hit.distance < length - .006f) return false;
                }
            }
        }
        return true;
    }
}
