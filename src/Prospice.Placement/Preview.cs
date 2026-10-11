using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
namespace Prospice.Placement;

internal sealed class UnsupportedItemException : Exception
{
    public UnsupportedItemException(string message) : base(message) { }
}

internal sealed class Preview : IDisposable
{
    public readonly GameObject Root;
    public readonly GameObject ProbeRoot;
    public readonly List<Collider> Shapes = new();
    readonly List<(Renderer renderer, bool enabled)> originals = new();
    readonly List<Mesh> baked = new();
    readonly List<MeshFilter> visibleMeshes = new();
    readonly Dictionary<int, Transform> transforms = new();
    readonly Dictionary<int, Transform> probes = new();
    readonly Transform sourceRoot;
    readonly Material material;
    public Preview(Rigidbody item, Vector3 originalScale)
    {
        sourceRoot = item.transform;
        Root = new GameObject("Prospice.Placement.Preview");
        Root.layer = 0; // Default world layer: rendered by the scene camera before its effects.
        ProbeRoot = new GameObject("Prospice.Placement.Queries"); ProbeRoot.layer = 2;
        probes[sourceRoot.GetInstanceID()] = ProbeRoot.transform;
        transforms[sourceRoot.GetInstanceID()] = Root.transform;
        var shader = Shader.Find("Hidden/Internal-Colored");
        if (!shader || !shader.isSupported) { Object.Destroy(Root); Object.Destroy(ProbeRoot); throw new InvalidOperationException("Scene preview shader unavailable."); }
        material = new Material(shader);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.renderQueue = (int)RenderQueue.Transparent;
        Colorize(true);
        try
        {
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                originals.Add((renderer, renderer.enabled));
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Mesh? mesh = null;
                var skin = renderer.TryCast<SkinnedMeshRenderer>();
                if (skin != null) { mesh = new Mesh(); skin.BakeMesh(mesh); baked.Add(mesh); }
                else mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || !mesh) continue;
                var target = Mirror(renderer.transform);
                var filter = target.gameObject.AddComponent<MeshFilter>(); filter.sharedMesh = mesh; visibleMeshes.Add(filter);
                var copy = target.gameObject.AddComponent<MeshRenderer>();
                copy.sharedMaterials = Enumerable.Repeat(material, mesh.subMeshCount).ToArray();
                copy.shadowCastingMode = ShadowCastingMode.Off;
                copy.receiveShadows = false;
            }
            foreach (var collider in item.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy || collider.attachedRigidbody != item) continue;
                var target = Mirror(collider.transform, true).gameObject;
                Collider clone;
                var box = collider.TryCast<BoxCollider>();
                var sphere = collider.TryCast<SphereCollider>();
                var capsule = collider.TryCast<CapsuleCollider>();
                var mesh = collider.TryCast<MeshCollider>();
                if (box != null) { var c = target.AddComponent<BoxCollider>(); c.center = box.center; c.size = box.size; clone = c; }
                else if (sphere != null) { var c = target.AddComponent<SphereCollider>(); c.center = sphere.center; c.radius = sphere.radius; clone = c; }
                else if (capsule != null) { var c = target.AddComponent<CapsuleCollider>(); c.center = capsule.center; c.radius = capsule.radius; c.height = capsule.height; c.direction = capsule.direction; clone = c; }
                else if (mesh != null && mesh.convex && mesh.sharedMesh) { var c = target.AddComponent<MeshCollider>(); c.sharedMesh = mesh.sharedMesh; c.convex = true; clone = c; }
                else throw new UnsupportedItemException("Unsupported collider geometry; placement remains disabled for this item.");
                // Query probes remain disabled between synchronous queries; no physics step sees them.
                clone.enabled = false;
                Shapes.Add(clone);
            }
            if (Shapes.Count == 0 || Root.GetComponentsInChildren<MeshRenderer>().Length == 0)
                throw new UnsupportedItemException("Item has no supported collision/render geometry.");
            Root.transform.localScale = originalScale;
            Root.transform.rotation = item.rotation;
            ProbeRoot.transform.localScale = originalScale; ProbeRoot.transform.rotation = item.rotation;
            foreach (var entry in originals) if (entry.renderer) entry.renderer.enabled = false;
        }
        catch { Dispose(); throw; }
    }
    Transform Mirror(Transform source, bool geometry = false)
    {
        var map = geometry ? probes : transforms;
        if (map.TryGetValue(source.GetInstanceID(), out var result)) return result;
        var parent = Mirror(source.parent, geometry);
        var go = new GameObject("Mesh"); go.layer = geometry ? 2 : 0;
        result = go.transform; result.SetParent(parent, false);
        result.localPosition = source.localPosition; result.localRotation = source.localRotation; result.localScale = source.localScale;
        map[source.GetInstanceID()] = result;
        return result;
    }
    internal float MinimumIncludingMesh(Vector3 axis)
    {
        float min = Shapes.Min(c => PlacementSolver.Minimum(c, axis));
        // Mesh local bounds remain readable even for meshes with CPU vertices stripped.
        foreach (var filter in visibleMeshes)
        {
            var bounds = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                min = Mathf.Min(min, Vector3.Dot(filter.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, sign)), axis));
            }
        }
        return min;
    }
    public void Colorize(bool valid) => material.SetColor("_Color", valid ? new Color(0, 1, 1, .32f) : new Color(1, .12f, .1f, .32f));
    public void Pose(Vector3 position, Quaternion rotation)
    {
        Root.transform.SetPositionAndRotation(position, rotation);
        ProbeRoot.transform.SetPositionAndRotation(position, rotation);
    }
    public void Dispose()
    {
        foreach (var entry in originals) if (entry.renderer) entry.renderer.enabled = entry.enabled;
        originals.Clear();
        if (Root) { Root.SetActive(false); Object.Destroy(Root); }
        if (ProbeRoot) { ProbeRoot.SetActive(false); Object.Destroy(ProbeRoot); }
        if (material) Object.Destroy(material);
        foreach (var mesh in baked) if (mesh) Object.Destroy(mesh);
        baked.Clear();
    }
}
