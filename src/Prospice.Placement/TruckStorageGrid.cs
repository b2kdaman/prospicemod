using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
namespace Prospice.Placement;

internal sealed class TruckStorageGrid : IDisposable
{
    internal const float CargoSurfaceClearance = .04f;
    readonly List<TruckBed> beds = new();
    readonly List<(GameObject root, Mesh mesh)> drawings = new();
    readonly Material material;
    float refresh;
    bool failed;
    internal IReadOnlyList<TruckBed> Beds => beds;
    internal TruckStorageGrid()
    {
        beds.AddRange(Object.FindObjectsOfType<TruckBed>());
        material = new Material(Shader.Find("Hidden/Internal-Colored"));
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetInt("_DstBlend", (int)BlendMode.One);
        material.SetInt("_ZWrite", 0); material.SetInt("_ZTest", (int)CompareFunction.LessEqual); material.SetInt("_Cull", (int)CullMode.Off);
        material.SetColor("_Color", Color.white); material.renderQueue = 2999;
    }
    internal static BoxCollider? MarkerFor(IReadOnlyList<TruckBed> beds, Collider surface, Vector3 point)
    {
        foreach (var bed in beds)
        {
            if (!bed || !bed.isActiveAndEnabled || !bed.bed || !bed.bedrb) continue;
            var box = bed.GetComponent<BoxCollider>();
            if (!box || !box.enabled || !box.isTrigger) continue;
            var p = box.transform.InverseTransformPoint(point) - box.center;
            if (Mathf.Abs(p.x) > box.size.x * .5f + .03f || Mathf.Abs(p.z) > box.size.z * .5f + .03f) continue;
            if (surface.attachedRigidbody == bed.bedrb || surface.transform.IsChildOf(bed.bed) ||
                (surface.GetComponentInParent<Packable>() && Mathf.Abs(p.y) <= box.size.y * .5f + 2)) return box;
        }
        return null;
    }
    internal static TruckBed? BedFor(IReadOnlyList<TruckBed> beds, BoxCollider marker) =>
        beds.FirstOrDefault(b => b && b.isActiveAndEnabled && b.bed && b.bedrb && b.GetComponent<BoxCollider>() == marker);
    internal static float SurfaceClearance(TruckBed bed, BoxCollider marker) => bed.bed == marker.transform ? .012f : CargoSurfaceClearance;
    internal static BoxCollider? AimedMarker(IReadOnlyList<TruckBed> beds, Camera camera, float reach, out Vector3 point)
    {
        point = default; BoxCollider? selected = null; float nearest = float.PositiveInfinity;
        var ray = new Ray(camera.transform.position, camera.transform.forward);
        foreach (var bed in beds)
        {
            if (!bed || !bed.isActiveAndEnabled || !bed.bed || !bed.bedrb) continue;
            var marker = bed.GetComponent<BoxCollider>();
            if (!marker || !marker.enabled || !marker.isTrigger || Vector3.Distance(marker.bounds.ClosestPoint(ray.origin), ray.origin) > reach) continue;
            var bottom = marker.transform.TransformPoint(marker.center - Vector3.up * marker.size.y * .5f);
            var plane = new Plane(marker.transform.up, bottom);
            if (!plane.Raycast(ray, out var distance) || distance >= nearest) continue;
            var hit = ray.GetPoint(distance);
            var local = marker.transform.InverseTransformPoint(hit) - marker.center;
            if (Mathf.Abs(local.x) > marker.size.x * .5f || Mathf.Abs(local.z) > marker.size.z * .5f) continue;
            selected = marker; nearest = distance; point = hit;
        }
        return selected;
    }
    internal void Update(Preview preview, Rigidbody item, Transform player, Camera camera, float reach)
    {
        if (failed || !Plugin.StorageGrid.Value) { Clear(); return; }
        // Geometry is parented to its native storage marker, so motion between refreshes stays attached.
        if (Time.unscaledTime < refresh) return;
        refresh = Time.unscaledTime + .35f;
        var pose = preview.Root.transform.position; var rotation = preview.Root.transform.rotation;
        try
        {
            Clear();
            foreach (var bed in beds)
            {
                if (!bed || !bed.isActiveAndEnabled || !bed.bed || !bed.bedrb) continue;
                var marker = bed.GetComponent<BoxCollider>();
                if (!marker || !marker.enabled || !marker.isTrigger || Vector3.Distance(marker.bounds.ClosestPoint(camera.transform.position), camera.transform.position) > reach) continue;
                var root = new GameObject("Prospice.Placement.StorageGrid"); root.layer = 0;
                root.transform.SetParent(marker.transform, false);
                var mesh = new Mesh { name = "Placement.FreeStorage", indexFormat = IndexFormat.UInt32 };
                drawings.Add((root, mesh));
                Build(mesh, marker, bed.bed, bed.bedrb, item, player);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
        }
        catch (Exception e) { failed = true; Clear(); Plugin.Instance.Log.LogWarning("Storage grid disabled for this preview: " + e.Message); }
        finally { preview.Pose(pose, rotation); }
    }
    // Probe 25 cm patches, then draw the shared 6.25 cm subdivisions in each exposed patch.
    // Sampling conservative patches avoids thousands of full item-geometry queries per frame.
    internal static int Build(Mesh mesh, BoxCollider marker, Transform bed, Rigidbody bedBody, Rigidbody item, Transform player)
    {
        var vertices = new List<Vector3>(); var colors = new List<Color>(); var indices = new List<int>();
        var t = marker.transform;
        // Use exactly the same horizontal world projection and centered lattice as ItemGrid.
        var up = Vector3.up;
        var frame = ItemGrid.Frame(t);
        var right = frame * Vector3.right;
        var forward = frame * Vector3.forward;
        var corners = new List<Vector3>();
        for (int i = 0; i < 8; i++)
            corners.Add(t.TransformPoint(marker.center + Vector3.Scale(marker.size * .5f,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        float loX = corners.Min(p => Vector3.Dot(p, right)), hiX = corners.Max(p => Vector3.Dot(p, right));
        float loZ = corners.Min(p => Vector3.Dot(p, forward)), hiZ = corners.Max(p => Vector3.Dot(p, forward));
        float width = hiX - loX, length = hiZ - loZ;
        int nx = Mathf.FloorToInt(width / ItemGrid.Step), nz = Mathf.FloorToInt(length / ItemGrid.Step);
        if (nx < 1 || nz < 1 || nx > 160 || nz > 200) { mesh.Clear(); return 0; }
        var corner = right * (loX + (width - nx * ItemGrid.Step) * .5f) + forward * (loZ + (length - nz * ItemGrid.Step) * .5f);
        int patches = 0;
        bool Sample(Vector3 p, out Vector3 floor)
        {
            float top = marker.bounds.max.y + .3f;
            var from = new Vector3(p.x, top, p.z);
            floor = default;
            foreach (var inside in Physics.OverlapSphere(from, .003f, ~0, QueryTriggerInteraction.Ignore))
                if (!GridPlacement.Ignore(inside, item, player)) return false;
            var hit = GridPlacement.Below(from, marker.bounds.size.y + .8f, item, player);
            floor = default;
            if (hit == null || hit.Value.normal.y < .5f) return false;
            var c = hit.Value.collider;
            if (c.GetComponentInParent<Packable>() || c.GetComponentInParent<Pickupable>()) return false;
            if (c.attachedRigidbody != bedBody && !c.transform.IsChildOf(bed)) return false;
            // The shipped cargo-bed mesh is 24.88 mm above its floor collider.
            // Four centimetres clears that render surface by ~15 mm; ordinary depth testing
            // still hides the grid behind cargo and rails. This is visual clearance only.
            // Native rack uses its own marker as bed; the cargo marker references the
            // separate CargoBed assembly. Preserve the rack's existing 12 mm clearance.
            float clearance = bed == marker.transform ? .012f : CargoSurfaceClearance;
            floor = hit.Value.point + hit.Value.normal * clearance;
            return true;
        }
        void Ribbon(Vector3 a, Vector3 b)
        {
            var side = Vector3.Cross(up, (b - a).normalized);
            var offsets = new[] { -.008f, -.002f, .002f, .008f };
            var alpha = new[] { 0f, .09f, .09f, 0f };
            int first = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(t.InverseTransformPoint(a + side * offsets[i])); vertices.Add(t.InverseTransformPoint(b + side * offsets[i]));
                var color = new Color(0, 1.5f, 1.3f, alpha[i]); colors.Add(color); colors.Add(color);
            }
            for (int i = 0; i < 3; i++)
            {
                int q = first + i * 2;
                indices.AddRange(new[] { q, q + 1, q + 3, q, q + 3, q + 2 });
            }
        }
        for (int x = 0; x < nx; x += 4) for (int z = 0; z < nz; z += 4)
        {
            int dx = Math.Min(4, nx - x), dz = Math.Min(4, nz - z);
            var a = corner + right * (x * ItemGrid.Step) + forward * (z * ItemGrid.Step);
            var b = a + right * (dx * ItemGrid.Step); var c = a + forward * (dz * ItemGrid.Step); var d = b + forward * (dz * ItemGrid.Step);
            var mid = (a + d) * .5f;
            // Inset samples prevent rails immediately outside the storage boundary from rejecting it.
            if (!Sample(mid, out var fm) || !Sample(Vector3.Lerp(a, mid, .04f), out var fa) ||
                !Sample(Vector3.Lerp(b, mid, .04f), out var fb) || !Sample(Vector3.Lerp(c, mid, .04f), out var fc) || !Sample(Vector3.Lerp(d, mid, .04f), out var fd)) continue;
            if (new[] { fa.y, fb.y, fc.y, fd.y }.Any(y => Mathf.Abs(y - fm.y) > .18f)) continue;
            patches++;
            // Preserve the exact common lattice; heights follow the physical support plane.
            a.y = fa.y; b.y = fb.y; c.y = fc.y; d.y = fd.y;
            for (int i = 0; i <= dx; i++) Ribbon(Vector3.Lerp(a, b, (float)i / dx), Vector3.Lerp(c, d, (float)i / dx));
            for (int i = 0; i <= dz; i++) Ribbon(Vector3.Lerp(a, c, (float)i / dz), Vector3.Lerp(b, d, (float)i / dz));
        }
        mesh.Clear(); mesh.vertices = vertices.ToArray(); mesh.colors = colors.ToArray(); mesh.triangles = indices.ToArray(); mesh.RecalculateBounds();
        return patches;
    }
    void Clear()
    {
        foreach (var entry in drawings) { if (entry.root) { entry.root.SetActive(false); Object.Destroy(entry.root); } if (entry.mesh) Object.Destroy(entry.mesh); }
        drawings.Clear();
    }
    public void Dispose() { Clear(); beds.Clear(); Object.Destroy(material); }
}
