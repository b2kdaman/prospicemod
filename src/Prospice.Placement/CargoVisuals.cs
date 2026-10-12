using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
namespace Prospice.Placement;

internal sealed class CargoVisuals : IDisposable
{
    internal readonly GameObject Root;
    readonly List<Mesh> meshes = new();
    readonly List<(MeshRenderer renderer, CargoPaint paint, Material material)> hardware = new();
    readonly Material webbing;
    readonly MaterialPropertyBlock properties = new();
    internal CargoVisuals(CargoPlan plan, CargoFrame frame)
    {
        Root = new GameObject("Prospice.Cargo.Belts"); Root.SetActive(false);
        Root.transform.SetPositionAndRotation(frame.Origin, Quaternion.LookRotation(frame.Forward, frame.Up));
        Root.transform.SetParent(frame.Transform, true);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (!shader) shader = Shader.Find("Standard");
        if (!shader) { Object.Destroy(Root); throw new InvalidOperationException("No compatible lit belt shader."); }
        webbing = new Material(shader) { name = "Prospice.Cargo.DarkGrayWebbing" };
        try
        {
            var gray = new Color(.16f, .17f, .18f, 1);
            webbing.SetColor("_BaseColor", gray); webbing.SetColor("_Color", gray);
            webbing.SetFloat("_Metallic", 0); webbing.SetFloat("_Smoothness", .12f); webbing.SetFloat("_Glossiness", .12f);
            webbing.SetFloat("_Surface", 0); webbing.SetFloat("_Cull", 2); webbing.renderQueue = 2000;
            foreach (var route in plan.Routes)
            {
                var ribbon = new Builder(Root.transform);
                ribbon.Ribbon(route.Points, route.WidthAxis, plan.Width, .002f);
                Draw("Flat strap", ribbon, webbing);
                Anchor(route.Start, route.WidthAxis, plan.Width);
                Anchor(route.End, route.WidthAxis, plan.Width);
            }
            var keeper = new Builder(Root.transform, plan.Paint);
            float r = plan.Width * 1.05f, bar = Mathf.Max(.004f, plan.Width * .14f);
            var center = plan.Crossing;
            // Four slim bars form a rectangular keeper over the crossing.
            keeper.Box(center + frame.Right * r, frame.Right, frame.Up, frame.Forward, new(bar, .006f, 2 * r + bar));
            keeper.Box(center - frame.Right * r, frame.Right, frame.Up, frame.Forward, new(bar, .006f, 2 * r + bar));
            keeper.Box(center + frame.Forward * r, frame.Right, frame.Up, frame.Forward, new(2 * r, .006f, bar));
            keeper.Box(center - frame.Forward * r, frame.Right, frame.Up, frame.Forward, new(2 * r, .006f, bar));
            Painted("Crossing keeper", keeper, plan.Paint);
            RefreshPaint(); Root.SetActive(true);
        }
        catch { Dispose(); throw; }
    }
    void Anchor(CargoAnchor anchor, Vector3 widthAxis, float width)
    {
        var up = anchor.Normal; var right = Vector3.ProjectOnPlane(widthAxis, up).normalized;
        var forward = Vector3.Cross(right, up).normalized;
        float radius = Mathf.Max(.015f, width * .7f), bar = .004f;
        var center = anchor.Point + up * .004f;
        var builder = new Builder(Root.transform, anchor.Paint);
        builder.Box(center, right, up, forward, new(radius * 2.7f, .006f, .025f));
        var ringCenter = center + up * .009f;
        Vector3 previous = ringCenter - right * radius;
        // A straight base and eight curved segments form a low-profile D ring.
        builder.Beam(previous, ringCenter + right * radius, forward, bar);
        for (int i = 1; i <= 8; i++)
        {
            float angle = Mathf.PI - i * Mathf.PI / 8;
            var next = ringCenter + right * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius);
            builder.Beam(previous, next, forward, bar); previous = next;
        }
        for (int side = -1; side <= 1; side += 2)
            builder.Box(center + right * (side * radius * 1.05f) + up * .004f, right, up, forward, new(.005f, .003f, .005f));
        Painted("Deck anchor", builder, anchor.Paint);
    }
    void Painted(string name, Builder builder, CargoPaint paint)
    {
        var materials = paint.Renderer.sharedMaterials;
        if (paint.Slot >= materials.Length || !materials[paint.Slot]) throw new InvalidOperationException("Deck paint material is unavailable.");
        var material = new Material(materials[paint.Slot]) { name = "Prospice.Cargo.DeckPaint" };
        if (material.HasProperty("_Bend")) material.SetFloat("_Bend", 0);
        try { hardware.Add((Draw(name, builder, material), paint, material)); }
        catch { Object.Destroy(material); throw; }
    }
    MeshRenderer Draw(string name, Builder builder, Material material)
    {
        var mesh = builder.Mesh(name); meshes.Add(mesh);
        var go = new GameObject(name); go.layer = 0; go.transform.SetParent(Root.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        return renderer;
    }
    internal void RefreshPaint()
    {
        foreach (var (renderer, paint, material) in hardware)
        {
            if (!renderer || !paint.Renderer) continue;
            var materials = paint.Renderer.sharedMaterials;
            if (paint.Slot >= materials.Length || !materials[paint.Slot]) continue;
            material.shader = materials[paint.Slot].shader; material.CopyPropertiesFromMaterial(materials[paint.Slot]);
            if (material.HasProperty("_Bend")) material.SetFloat("_Bend", 0);
            properties.Clear(); paint.Renderer.GetPropertyBlock(properties);
            properties.SetFloat("_Bend", 0); renderer.SetPropertyBlock(properties);
            properties.Clear(); paint.Renderer.GetPropertyBlock(properties, paint.Slot);
            if (!properties.isEmpty) { properties.SetFloat("_Bend", 0); renderer.SetPropertyBlock(properties, 0); }
            else renderer.SetPropertyBlock(null, 0);
        }
    }
    public void Dispose()
    {
        if (Root) { Root.SetActive(false); Object.Destroy(Root); }
        foreach (var mesh in meshes) if (mesh) Object.Destroy(mesh);
        meshes.Clear(); foreach (var h in hardware) if (h.material) Object.Destroy(h.material);
        hardware.Clear(); if (webbing) Object.Destroy(webbing);
    }
    sealed class Builder
    {
        readonly Transform root; readonly CargoPaint? paint;
        readonly List<Vector3> vertices = new(); readonly List<Vector2> uv = new(); readonly List<int> indices = new();
        internal Builder(Transform root, CargoPaint? paint = null) { this.root = root; this.paint = paint; }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int n = vertices.Count;
            foreach (var p in new[] { a, b, c, d }) { vertices.Add(root.InverseTransformPoint(p)); uv.Add(paint?.Map(p) ?? Vector2.zero); }
            indices.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
        }
        internal void Box(Vector3 center, Vector3 right, Vector3 up, Vector3 forward, Vector3 size)
        {
            var x = right * size.x * .5f; var y = up * size.y * .5f; var z = forward * size.z * .5f;
            Quad(center-x+y-z, center-x+y+z, center+x+y+z, center+x+y-z);
            Quad(center-x-y+z, center-x-y-z, center+x-y-z, center+x-y+z);
            Quad(center+x-y-z, center+x+y-z, center+x+y+z, center+x-y+z);
            Quad(center-x-y+z, center-x+y+z, center-x+y-z, center-x-y-z);
            Quad(center-x-y-z, center-x+y-z, center+x+y-z, center+x-y-z);
            Quad(center+x-y+z, center+x+y+z, center-x+y+z, center-x-y+z);
        }
        internal void Beam(Vector3 a, Vector3 b, Vector3 widthAxis, float thickness)
        {
            var axis = (b - a).normalized; var normal = Vector3.Cross(axis, widthAxis).normalized;
            Box((a+b)*.5f, widthAxis, normal, axis, new(thickness, thickness, Vector3.Distance(a,b) + thickness*.3f));
        }
        internal void Ribbon(Vector3[] points, Vector3 side, float width, float thickness)
        {
            var normals = new Vector3[points.Length];
            var sides = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var before = (points[i] - points[Mathf.Max(0, i-1)]).normalized;
                var after = (points[Mathf.Min(points.Length-1, i+1)] - points[i]).normalized;
                var tangent = (before + after).normalized;
                sides[i] = Vector3.ProjectOnPlane(side, tangent).normalized * (width * .5f);
                normals[i] = Vector3.Cross(sides[i], tangent).normalized * (thickness * .5f);
            }
            for (int i = 1; i < points.Length; i++)
            {
                var a=points[i-1]; var b=points[i]; var na=normals[i-1]; var nb=normals[i];
                var sa=sides[i-1]; var sb=sides[i];
                Quad(a-sa+na, a+sa+na, b+sb+nb, b-sb+nb);
                Quad(a+sa-na, a-sa-na, b-sb-nb, b+sb-nb);
                Quad(a-sa-na, a-sa+na, b-sb+nb, b-sb-nb);
                Quad(a+sa+na, a+sa-na, b+sb-nb, b+sb+nb);
            }
            Quad(points[0]-sides[0]-normals[0], points[0]+sides[0]-normals[0], points[0]+sides[0]+normals[0], points[0]-sides[0]+normals[0]);
            int last=points.Length-1;
            Quad(points[last]+sides[last]-normals[last], points[last]-sides[last]-normals[last], points[last]-sides[last]+normals[last], points[last]+sides[last]+normals[last]);
        }
        internal Mesh Mesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices.ToArray(); mesh.uv = uv.ToArray(); mesh.triangles = indices.ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (paint.HasValue) mesh.RecalculateTangents();
            return mesh;
        }
    }
}
