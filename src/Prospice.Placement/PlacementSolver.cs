using UnityEngine;
namespace Prospice.Placement;

internal readonly record struct PlacementTarget(bool Valid, Vector3 Position, Quaternion Rotation, Collider? Support, string Reason);
internal static class PlacementSolver
{
    const float Skin = .005f;
    public static PlacementTarget Solve(Preview preview, Rigidbody item, Transform player, Camera camera, float reach, int mask, Quaternion rotation, Vector3 offset = default)
    {
        var origin = camera.transform.position;
        var direction = camera.transform.forward;
        var nearest = FindSurface(preview, item, player, camera, reach, mask);
        if (nearest == null)
        {
            var end = origin + direction * reach;
            preview.Pose(end, rotation);
            return new(false, end, rotation, null, "No surface in reach");
        }
        var surface = nearest.Value;
        preview.Pose(Vector3.zero, rotation);
        float minimum = float.PositiveInfinity;
        foreach (var shape in preview.Shapes) minimum = Mathf.Min(minimum, Minimum(shape, surface.normal));
        var position = surface.point + surface.normal * (Skin - minimum) + offset;
        preview.Pose(position, rotation);
        if (Vector3.Dot(surface.normal, Vector3.up) < .5f)
            return new(false, position, rotation, surface.collider, "Surface steeper than 60 degrees");
        var bounds = BoundsOf(preview.Shapes);
        if (Vector3.Distance(origin, bounds.center) > reach + Skin)
            return new(false, position, rotation, surface.collider, "Out of reach");
        // Manual X/Y offsets must not bypass intervening walls or place beyond the camera reach.
        var sight = bounds.center - origin;
        foreach (var hit in Physics.RaycastAll(origin, sight.normalized, sight.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!Ignore(hit.collider, item, player, preview))
                return new(false, position, rotation, surface.collider, "View blocked");
        if (offset.sqrMagnitude > .000001f)
        {
            // Reacquire the support below the translated object, so truck velocity is inherited
            // from the actual destination. Raised placements settle under normal gravity.
            RaycastHit? below = null;
            var bottom = new Vector3(bounds.center.x, bounds.min.y + Skin, bounds.center.z);
            foreach (var hit in Physics.RaycastAll(bottom, Vector3.down, reach, mask, QueryTriggerInteraction.Ignore))
            {
                if (Ignore(hit.collider, item, player, preview)) continue;
                if (below == null || hit.distance < below.Value.distance) below = hit;
            }
            if (below == null) return new(false, position, rotation, null, "No support below");
            surface = below.Value;
            if (Vector3.Dot(surface.normal, Vector3.up) < .5f)
                return new(false, position, rotation, surface.collider, "Surface steeper than 60 degrees");
        }
        foreach (var other in Physics.OverlapBox(bounds.center, bounds.extents + Vector3.one * Skin, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            if (Ignore(other, item, player, preview)) continue;
            foreach (var shape in preview.Shapes)
                if (Penetrates(shape, other, out var depth) && depth > .001f)
                    return new(false, position, rotation, surface.collider, "Blocked");
        }
        return new(true, position, rotation, surface.collider, "Release LT to place");
    }
    internal static RaycastHit? FindSurface(Preview preview, Rigidbody item, Transform player, Camera camera, float reach, int mask)
    {
        RaycastHit? nearest = null;
        foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, reach, mask, QueryTriggerInteraction.Ignore))
        {
            if (Ignore(hit.collider, item, player, preview)) continue;
            if (nearest == null || hit.distance < nearest.Value.distance) nearest = hit;
        }
        return nearest;
    }
    // In this Unity build a disabled collider has no query geometry. Enable only across
    // this synchronous query, then disable in finally, before any physics simulation can run.
    internal static bool Penetrates(Collider shape, Collider other, out float depth)
    {
        shape.enabled = true;
        try { return Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
            other, other.transform.position, other.transform.rotation, out _, out depth); }
        finally { shape.enabled = false; }
    }
    static bool Ignore(Collider c, Rigidbody item, Transform player, Preview preview) => !c || c.isTrigger ||
        c.attachedRigidbody == item || c.transform.IsChildOf(item.transform) || c.transform.IsChildOf(player) || c.transform.IsChildOf(preview.Root.transform);

    // Exact support functions for primitives. Convex mesh vertices have the same extrema as their hull.
    internal static float Minimum(Collider c, Vector3 direction)
    {
        var t = c.transform;
        var box = c.TryCast<BoxCollider>();
        if (box != null) return BoxMinimum(t, box.center, box.size * .5f, direction);
        var sphere = c.TryCast<SphereCollider>();
        if (sphere != null)
        {
            var s = t.lossyScale;
            return Vector3.Dot(t.TransformPoint(sphere.center), direction) - sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
        var capsule = c.TryCast<CapsuleCollider>();
        if (capsule != null)
        {
            var scale = t.lossyScale;
            var axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
            float length = Mathf.Abs(scale[capsule.direction]);
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale[(capsule.direction + 1) % 3]), Mathf.Abs(scale[(capsule.direction + 2) % 3]));
            float segment = Mathf.Max(0, capsule.height * length * .5f - radius);
            return Vector3.Dot(t.TransformPoint(capsule.center), direction) - Mathf.Abs(Vector3.Dot(t.rotation * axis, direction)) * segment - radius;
        }
        var mesh = c.Cast<MeshCollider>().sharedMesh;
        if (!mesh.isReadable) return BoxMinimum(t, mesh.bounds.center, mesh.bounds.extents, direction);
        float min = float.PositiveInfinity;
        foreach (var vertex in mesh.vertices) min = Mathf.Min(min, Vector3.Dot(t.TransformPoint(vertex), direction));
        return min;
    }
    static float BoxMinimum(Transform t, Vector3 center, Vector3 extents, Vector3 direction) => Vector3.Dot(t.TransformPoint(center), direction)
        - Mathf.Abs(Vector3.Dot(t.TransformVector(Vector3.right * extents.x), direction))
        - Mathf.Abs(Vector3.Dot(t.TransformVector(Vector3.up * extents.y), direction))
        - Mathf.Abs(Vector3.Dot(t.TransformVector(Vector3.forward * extents.z), direction));
    internal static Bounds BoundsOf(List<Collider> shapes)
    {
        var min = Vector3.one * float.PositiveInfinity;
        var max = Vector3.one * float.NegativeInfinity;
        foreach (var shape in shapes)
            for (int i = 0; i < 3; i++)
            {
                var axis = Vector3.zero; axis[i] = 1;
                min[i] = Mathf.Min(min[i], Minimum(shape, axis));
                max[i] = Mathf.Max(max[i], -Minimum(shape, -axis));
            }
        return new Bounds((min + max) * .5f, max - min);
    }
}
