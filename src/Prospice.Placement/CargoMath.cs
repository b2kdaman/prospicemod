using System.Numerics;
namespace Prospice.Placement;

// Pure geometry shared by runtime packing and the standalone regression suite.
internal static class CargoMath
{
    // The storage trigger's ceiling is not a cargo-height limit. Support tracing
    // decides whether an overlapping load above the deck belongs to this storage.
    internal static bool InColumn(Vector3 min, Vector3 max, Vector3 deckMin, Vector3 deckMax) =>
        max.X >= deckMin.X && min.X <= deckMax.X && max.Z >= deckMin.Z && min.Z <= deckMax.Z && max.Y >= deckMin.Y - .04f;

    internal static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    internal static List<Vector2> Hull(IEnumerable<Vector2> points)
    {
        var p = points.Where(p => float.IsFinite(p.X) && float.IsFinite(p.Y)).Distinct()
            .OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
        if (p.Length < 3) return p.ToList();
        var result = new List<Vector2>();
        foreach (var q in p)
        {
            while (result.Count >= 2 && Cross(result[^2], result[^1], q) <= 0) result.RemoveAt(result.Count - 1);
            result.Add(q);
        }
        int lower = result.Count;
        for (int i = p.Length - 2; i >= 0; i--)
        {
            while (result.Count > lower && Cross(result[^2], result[^1], p[i]) <= 0) result.RemoveAt(result.Count - 1);
            result.Add(p[i]);
        }
        result.RemoveAt(result.Count - 1);
        return result;
    }

    internal static bool Supported(IEnumerable<Vector2> contacts, Vector2 center, float margin = .002f)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y)) return false;
        var h = Hull(contacts);
        if (h.Count < 3) return false;
        for (int i = 0; i < h.Count; i++)
        {
            var a = h[i]; var b = h[(i + 1) % h.Count];
            if (Cross(a, b, center) < margin * Vector2.Distance(a, b)) return false;
        }
        return true;
    }

    // A taut strap follows the upper convex envelope of a vertical section.
    // Keeping endpoints in the input makes both ends terminate exactly at anchors.
    internal static List<Vector2> UpperEnvelope(IEnumerable<Vector2> samples)
    {
        var p = samples.GroupBy(v => v.X).Select(g => new Vector2(g.Key, g.Max(v => v.Y)))
            .OrderBy(v => v.X).ToArray();
        var h = new List<Vector2>();
        foreach (var q in p)
        {
            while (h.Count >= 2 && Cross(h[^2], h[^1], q) >= 0) h.RemoveAt(h.Count - 1);
            h.Add(q);
        }
        return h;
    }

    internal static float Height(IReadOnlyList<Vector2> path, float x)
    {
        for (int i = 1; i < path.Count; i++)
            if (x <= path[i].X)
            {
                var a = path[i - 1]; var b = path[i];
                return a.Y + (b.Y - a.Y) * Math.Clamp((x - a.X) / (b.X - a.X), 0, 1);
            }
        return path[^1].Y;
    }

    internal static bool ProjectGravity(Vector3 point, Vector3 gravity, Vector3 planePoint, Vector3 normal, out Vector3 hit)
    {
        hit = default;
        if (!float.IsFinite(gravity.LengthSquared()) || gravity.LengthSquared() < .00001f) return false;
        gravity = Vector3.Normalize(gravity);
        float slope = Vector3.Dot(gravity, normal);
        if (!float.IsFinite(slope) || slope >= -.00001f) return false;
        float distance = Vector3.Dot(planePoint - point, normal) / slope;
        if (!float.IsFinite(distance) || distance < 0) return false;
        hit = point + gravity * distance; return true;
    }

    internal readonly record struct Contact(int Support, Vector2 Point, float Height = 0);
    internal sealed record Load(int Id, Vector2 Center, IReadOnlyList<Contact> Contacts);
    // ID 0 represents verified deck contact. No island or dependency cycle can seed packing.
    internal static List<int> Resolve(IReadOnlyList<Load> loads, Func<int, IReadOnlySet<int>, bool> route)
    {
        var accepted = new HashSet<int> { 0 }; var result = new List<int>();
        bool changed;
        do
        {
            changed = false;
            foreach (var load in loads)
            {
                if (accepted.Contains(load.Id) || !Supported(load.Contacts.Where(c => accepted.Contains(c.Support)).Select(c => c.Point), load.Center)) continue;
                if (!route(load.Id, accepted)) continue;
                accepted.Add(load.Id); result.Add(load.Id); changed = true;
            }
        } while (changed);
        return result;
    }
}
