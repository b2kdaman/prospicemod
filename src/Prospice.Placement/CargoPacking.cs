using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using V2 = System.Numerics.Vector2;
namespace Prospice.Placement;

internal static class CargoPacking
{
    sealed class Secured
    {
        internal readonly Packable Item;
        internal readonly Rigidbody Body;
        internal readonly FixedJoint Joint;
        internal readonly CargoVisuals Visuals;
        internal readonly CargoMath.Load Load;
        internal readonly Vector3 Position;
        internal readonly Quaternion Rotation;
        internal Secured(CargoPlan plan, CargoFrame frame, FixedJoint joint, CargoVisuals visuals)
        {
            Item = plan.Item.Item; Body = plan.Item.Body; Joint = joint; Visuals = visuals;
            Load = new(plan.Item.Id, CargoGeometry.ProjectedCenter(plan.Item, frame), plan.Item.Contacts.ToArray());
            Position = frame.Transform.InverseTransformPoint(Body.position);
            Rotation = Quaternion.Inverse(frame.Transform.rotation) * Body.rotation;
        }
    }
    sealed class Storage
    {
        internal readonly TruckBed Bed;
        internal readonly List<Secured> Items = new();
        internal bool Unpacking;
        internal Storage(TruckBed bed) => Bed = bed;
    }
    static readonly Dictionary<int, Storage> storages = new();
    static readonly HashSet<int> warned = new();
    static float nextRefresh;

    // True means replacement handled this surface, including a deliberately empty result.
    internal static bool TryPack(TruckBed bed)
    {
        if (!Plugin.Ready || !Plugin.CargoBelts.Value || !bed || !bed.isActiveAndEnabled || !bed.bed || !bed.bedrb) return false;
        int id = bed.GetInstanceID();
        if (storages.TryGetValue(id, out var existing))
        {
            // Native can request packing repeatedly while driving. Do not duplicate joints.
            if (existing.Items.Count != 0) return true;
            storages.Remove(id);
        }
        // Do not take over joints belonging to an already running vanilla packing session.
        if (bed.packed || (bed.obb != null && bed.obb.Count != 0)) return false;
        var marker = bed.GetComponent<BoxCollider>();
        if (!marker || !marker.enabled || !marker.isTrigger) return false;
        var geometries = new Dictionary<int, CargoItemGeometry>();
        Storage? storage = null;
        try
        {
            using var surface = new CargoSurface(bed, marker);
            if (!surface.Verify()) throw new InvalidOperationException("Rendered deck and physical support could not be matched.");
            var held = new HashSet<int>();
            foreach (var interaction in Object.FindObjectsOfType<PlayerInteraction>())
                if (interaction.heldRigidbody) held.Add(interaction.heldRigidbody.GetInstanceID());
            var seen = new HashSet<int>();
            foreach (var item in Object.FindObjectsOfType<Packable>())
            {
                if (!item || item.packed || item.joint) continue;
                var body = item.GetComponent<Rigidbody>();
                if (!body || body == bed.bedrb || body.isKinematic || held.Contains(body.GetInstanceID()) || !seen.Add(body.GetInstanceID()) || body.GetComponents<Joint>().Length != 0) continue;
                var shapes = body.GetComponentsInChildren<Collider>().Where(c => c && c.enabled && !c.isTrigger && c.attachedRigidbody == body).ToArray();
                if (shapes.Length == 0) continue;
                var min = Vector3.positiveInfinity; var max = Vector3.negativeInfinity;
                foreach (var shape in shapes)
                {
                    var bounds = shape.bounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var point = surface.Frame.Local(bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1)==0?-1:1,(corner & 2)==0?-1:1,(corner & 4)==0?-1:1)));
                        min=Vector3.Min(min,point);max=Vector3.Max(max,point);
                    }
                }
                System.Numerics.Vector3 V(Vector3 v) => new(v.x,v.y,v.z);
                if (!CargoMath.InColumn(V(min), V(max), V(surface.Frame.Min), V(surface.Frame.Max))) continue;
                try
                {
                    var geometry = new CargoItemGeometry(item, body, surface.Frame);
                    geometries.Add(geometry.Id, geometry);
                }
                catch (Exception error)
                {
                    if (warned.Add(item.GetInstanceID())) Plugin.Instance.Log.LogWarning($"Cargo geometry unavailable for {item.name}: {error.Message}");
                }
            }
            foreach (var geometry in geometries.Values) CargoGeometry.Contacts(surface, geometry, geometries);
            var plans = new Dictionary<int, CargoPlan>();
            var loads = geometries.Values.OrderBy(i => i.Min.y).ThenBy(i => i.Id).Select(i =>
            {
                return new CargoMath.Load(i.Id, CargoGeometry.ProjectedCenter(i, surface.Frame), i.Contacts);
            }).ToArray();
            var order = CargoMath.Resolve(loads, (itemId, _) =>
            {
                var plan = CargoGeometry.Route(surface, geometries[itemId], geometries, plans);
                if (plan == null) return false;
                plans[itemId] = plan; return true;
            });
            storage = new Storage(bed); storages.Add(id, storage);
            bed.obb ??= new Il2CppSystem.Collections.Generic.List<Packable>();
            foreach (int itemId in order)
            {
                var plan = plans[itemId];
                var visuals = new CargoVisuals(plan, surface.Frame);
                FixedJoint? joint = null;
                try
                {
                    joint = plan.Item.Body.gameObject.AddComponent<FixedJoint>();
                    joint.connectedBody = bed.bedrb; joint.enablePreprocessing = false;
                    // Native packing leaves default infinite break thresholds and disables
                    // preprocessing; tipping explicitly invokes TruckBed.cxj instead.
                    plan.Item.Item.ropes = new Il2CppSystem.Collections.Generic.List<GameObject>();
                    plan.Item.Item.ropes.Add(visuals.Root);
                    plan.Item.Item.joint = joint; plan.Item.Item.packed = true;
                    storage.Items.Add(new(plan, surface.Frame, joint, visuals)); bed.obb.Add(plan.Item.Item);
                }
                catch
                {
                    visuals.Dispose();
                    if (joint) { joint!.breakForce = 0; joint.breakTorque = 0; Object.Destroy(joint); }
                    plan.Item.Item.packed = false; plan.Item.Item.joint = null; plan.Item.Item.ropes?.Clear();
                    throw;
                }
            }
            bed.packed = storage.Items.Count > 0;
            Plugin.Instance.Log.LogInfo($"Cargo belts: {storage.Items.Count}/{geometries.Count} supported loads secured on {bed.name}.");
            return true;
        }
        catch (Exception error)
        {
            if (storage != null) Clear(storage);
            storages.Remove(id);
            if (warned.Add(id)) Plugin.Instance.Log.LogWarning("Cargo replacement unavailable on " + bed.name + "; using native packing. " + error.Message);
            return false;
        }
        finally { foreach (var geometry in geometries.Values) geometry.Dispose(); }
    }

    static bool packingPair;
    internal static IEnumerable<TruckBed> Related(TruckBed bed)
    {
        yield return bed;
        foreach (var truck in Object.FindObjectsOfType<CarTipping>())
        {
            if (truck.truckBed != bed && truck.roofrack != bed) continue;
            var other = truck.truckBed == bed ? truck.roofrack : truck.truckBed;
            if (other && other != bed && other.isActiveAndEnabled) yield return other;
        }
    }
    internal static void PackOtherStorage(TruckBed bed)
    {
        if (packingPair || !Plugin.Ready || !Plugin.CargoBelts.Value || !bed) return;
        packingPair = true;
        try
        {
            foreach (var other in Related(bed).Where(b => b != bed).GroupBy(b => b.GetInstanceID()).Select(g => g.First()))
                other.cxi();
        }
        finally { packingPair = false; }
    }

    internal static bool ReleaseItem(Packable item)
    {
        if (!item) return false;
        foreach (var storage in storages.Values)
        {
            var secured = storage.Items.FirstOrDefault(s => s.Item == item);
            if (secured == null) continue;
            Release(storage, secured);
            if (!storage.Unpacking) { RemoveUnsupported(storage); Sync(storage); }
            return true;
        }
        return false;
    }
    static void Release(Storage storage, Secured secured)
    {
        storage.Items.Remove(secured);
        if (secured.Joint)
        {
            // Match native cleanup's immediate low break thresholds before deferred destruction.
            secured.Joint.breakForce = .1f; secured.Joint.breakTorque = .1f; Object.Destroy(secured.Joint);
        }
        if (secured.Item)
        {
            secured.Item.packed = false;
            if (secured.Item.joint == secured.Joint) secured.Item.joint = null;
            secured.Item.ropes?.Clear();
        }
        secured.Visuals.Dispose();
    }
    static void RemoveUnsupported(Storage storage)
    {
        var valid = CargoMath.Resolve(storage.Items.Select(s => s.Load).ToArray(), (_, _) => true).ToHashSet();
        foreach (var item in storage.Items.ToArray()) if (!valid.Contains(item.Load.Id)) Release(storage, item);
    }
    static void Sync(Storage storage)
    {
        if (!storage.Bed || storage.Unpacking) return;
        storage.Bed.obb ??= new Il2CppSystem.Collections.Generic.List<Packable>();
        storage.Bed.obb.Clear();
        foreach (var item in storage.Items) if (item.Item) storage.Bed.obb.Add(item.Item);
        storage.Bed.packed = storage.Items.Count > 0;
    }
    internal static void BeforeUnpack(TruckBed bed)
    {
        if (bed && storages.TryGetValue(bed.GetInstanceID(), out var storage)) storage.Unpacking = true;
    }
    internal static void AfterUnpack(TruckBed bed)
    {
        if (!bed || !storages.TryGetValue(bed.GetInstanceID(), out var storage)) return;
        storage.Unpacking = false; Clear(storage); storages.Remove(bed.GetInstanceID());
    }
    internal static void AfterTipping(CarTipping tipping)
    {
        // Native FixedUpdate checks truckBed.packed again after cxj has cleared it,
        // accidentally skipping roofrack. Apply the same native angle/timer policy
        // to our owned roof cargo only, without changing native warnings or audio.
        if (!tipping || tipping.angle <= 45 || !tipping.forceResetter || tipping.forceResetter.checkTimer > 0 ||
            !tipping.roofrackGO || !tipping.roofrackGO.activeInHierarchy || !tipping.roofrack) return;
        if (storages.ContainsKey(tipping.roofrack.GetInstanceID()) && tipping.roofrack.packed) tipping.roofrack.cxj();
    }
    static void Clear(Storage storage)
    {
        foreach (var item in storage.Items.ToArray()) Release(storage, item);
        storage.Unpacking = false; Sync(storage);
    }
    internal static void Update()
    {
        if (!Plugin.CargoBelts.Value) { Shutdown(); return; }
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .25f;
        foreach (var pair in storages.ToArray())
        {
            var storage = pair.Value;
            try
            {
                if (!storage.Bed || !storage.Bed.isActiveAndEnabled || !storage.Bed.bedrb)
                { Clear(storage); storages.Remove(pair.Key); continue; }
                foreach (var item in storage.Items.ToArray())
                {
                    var t = storage.Bed.transform;
                    if (!item.Item || !item.Body || !item.Joint || !item.Item.packed || item.Body.isKinematic ||
                        Vector3.Distance(t.InverseTransformPoint(item.Body.position), item.Position) > .04f ||
                        Quaternion.Angle(Quaternion.Inverse(t.rotation) * item.Body.rotation, item.Rotation) > 5)
                        Release(storage, item);
                    else item.Visuals.RefreshPaint();
                }
                RemoveUnsupported(storage); Sync(storage);
            }
            catch (Exception error)
            {
                Plugin.Instance.Log.LogWarning("Cargo lifecycle cleanup: " + error.Message);
                Clear(storage); storages.Remove(pair.Key);
            }
        }
    }
    internal static void Shutdown()
    {
        foreach (var storage in storages.Values.ToArray()) Clear(storage);
        storages.Clear(); warned.Clear(); nextRefresh = 0; CargoMeshCache.Clear();
    }
}

[HarmonyPatch(typeof(TruckBed), nameof(TruckBed.cxi))]
internal static class CargoPackPatch
{
    [HarmonyPrefix] static bool Prefix(TruckBed __instance) => !CargoPacking.TryPack(__instance);
    [HarmonyPostfix] static void Postfix(TruckBed __instance) => CargoPacking.PackOtherStorage(__instance);
}
[HarmonyPatch(typeof(TruckBed), nameof(TruckBed.cxj))]
internal static class CargoUnpackPatch
{
    [HarmonyPrefix] static void Prefix(TruckBed __instance) => CargoPacking.BeforeUnpack(__instance);
    [HarmonyFinalizer] static void Finalizer(TruckBed __instance) => CargoPacking.AfterUnpack(__instance);
}
[HarmonyPatch(typeof(Packable), nameof(Packable.bxg))]
internal static class CargoReleasePatch
{
    [HarmonyPrefix] static bool Prefix(Packable __instance) => !CargoPacking.ReleaseItem(__instance);
}
[HarmonyPatch(typeof(CarTipping), nameof(CarTipping.FixedUpdate))]
internal static class CargoTippingPatch
{
    [HarmonyPostfix] static void Postfix(CarTipping __instance) => CargoPacking.AfterTipping(__instance);
}
