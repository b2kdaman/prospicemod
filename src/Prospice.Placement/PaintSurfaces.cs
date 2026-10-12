using UnityEngine;
using Object=UnityEngine.Object;
namespace Prospice.Placement;
internal static class PaintSurfaces
{
    internal sealed class Surface
    {
        internal bool Pending;
        internal string Key=""; internal Transform? Root; internal MeshFilter[] Filters=Array.Empty<MeshFilter>();
        internal Matrix4x4 MetricToWorld=>Root?Root!.localToWorldMatrix*Matrix4x4.Scale(Divide(Vector3.one,Root.lossyScale)):Matrix4x4.identity;
        internal int Signature=>Filters.Aggregate(Root?Root!.GetInstanceID():0,(h,f)=>HashCode.Combine(h,f?f.GetInstanceID():0));
        internal Vector3 ToMetric(Vector3 world)=>Root?Vector3.Scale(Root!.InverseTransformPoint(world),Root.lossyScale):world;
        internal Vector3 ToWorld(Vector3 point)=>Root?Root!.TransformPoint(Divide(point,Root.lossyScale)):point;
        internal Vector3 ToLocal(Vector3 point)=>Root?Divide(point,Root!.lossyScale):point;
        static Vector3 Divide(Vector3 a,Vector3 b)=>new(a.x/b.x,a.y/b.y,a.z/b.z);
        internal bool OwnsPrimitive(Collider collider)
        {
            if(!collider||collider.TryCast<MeshCollider>()!=null)return false;
            var t=collider.transform;
            return Filters.Any(f=>f&&(f.transform==t||f.transform.IsChildOf(t)||t.IsChildOf(f.transform)||
                f.transform.parent&&f.transform.parent==t.parent));
        }
        internal IEnumerable<(Vector3 a,Vector3 b,Vector3 c)> Triangles(Bounds? region=null)
        {
            Pending=false;
            Bounds? worldRegion=null;
            if(region.HasValue)
            {
                var box=region.Value;var world=new Bounds(ToWorld(box.center),Vector3.zero);
                for(int corner=0;corner<8;corner++)world.Encapsulate(ToWorld(box.center+Vector3.Scale(box.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1))));
                worldRegion=world;
            }
            foreach(var f in Filters)
            {
                if(!Receiver(f))continue;
                var renderer=f.GetComponent<MeshRenderer>();if(!renderer || !renderer.enabled || worldRegion.HasValue&&!renderer.bounds.Intersects(worldRegion.Value))continue;
                var mats=renderer.sharedMaterials;int start=renderer.isPartOfStaticBatch?renderer.subMeshStartIndex:0;
                PaintMeshCache.Geometry? data;
                try{data=PaintMeshCache.Get(f.sharedMesh,start,mats.Length);}catch(Exception e){WarnOnce(f.sharedMesh.name,e.Message);continue;}
                if(data==null){Pending=true;continue;}
                var opaque=mats.Select(Opaque).ToArray();
                var rawToMetric=MetricToWorld.inverse*renderer.localToWorldMatrix;var metricToRaw=rawToMetric.inverse;
                Vector3 Point(int i)=>rawToMetric.MultiplyPoint3x4(data.Vertices[i]);
                var min=Vector3.negativeInfinity;var max=Vector3.positiveInfinity;
                if(region.HasValue)
                {
                    min=Vector3.positiveInfinity;max=Vector3.negativeInfinity;var r=region.Value;
                    for(int corner=0;corner<8;corner++){var q=r.center+Vector3.Scale(r.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));var raw=metricToRaw.MultiplyPoint3x4(q);min=Vector3.Min(min,raw);max=Vector3.Max(max,raw);}
                }
                foreach(var triangle in data.Index.Query(new(min.x,min.y,min.z),new(max.x,max.y,max.z),start,mats.Length))
                {
                    int slot=triangle.Slot-start;if(!opaque[slot])continue;
                    yield return(Point(triangle.A),Point(triangle.B),Point(triangle.C));
                }
            }
        }
    }
    // Reflection helpers can be enabled opaque meshes but are not visible scenery.
    // In this game a wide thin cylinder follows the reflection probe above the floor.
    static bool Receiver(MeshFilter f)
    {
        if(!f || !f.gameObject.activeInHierarchy || !f.sharedMesh || f.gameObject.layer==5 ||
            f.name.StartsWith("Prospice.") || f.GetComponentInParent<PlayerInteraction>() ||
            f.GetComponentInParent<Animator>() || f.GetComponentInParent<ReflectionProbe>())return false;
        var renderer=f.GetComponent<MeshRenderer>();
        return renderer && renderer.enabled && !renderer.forceRenderingOff &&
            renderer.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
    }
    static bool Opaque(Material m)=>m && m.renderQueue<2450 && !m.IsKeywordEnabled("_ALPHATEST_ON") && !m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") &&
        !m.name.Contains("water",StringComparison.OrdinalIgnoreCase) && !m.name.Contains("glass",StringComparison.OrdinalIgnoreCase) && !m.name.Contains("leaf",StringComparison.OrdinalIgnoreCase) && !m.name.Contains("leaves",StringComparison.OrdinalIgnoreCase);
    static readonly HashSet<string> warned=new();
    static void WarnOnce(string key,string error){if(warned.Add(key))Plugin.Instance.Log.LogWarning("Paint surface unavailable: "+error);}
    internal static readonly Dictionary<string,Surface> All=new();
    static float nextScan;internal static int Generation;
    static MeshFilter[]? scanning;static int scanAt,scanFrame=-1;
    static readonly Dictionary<string,List<MeshFilter>> scanGroups=new();static readonly Dictionary<string,Transform?> scanRoots=new();
    internal static bool Preparing=>scanning!=null;
    internal static string PathOf(Transform t)
    {
        string path="";var current=t;
        while(current){path="/"+current.name+"["+current.GetSiblingIndex()+"]"+path;current=current.parent;}
        return t.gameObject.scene.path+path;
    }
    internal static Transform? Canonical(Transform t)
    {
        var physics=t.GetComponentInParent<SwappablePhysicsObject>();if(physics && physics.parent)return physics.parent.transform;
        var swap=t.GetComponentInParent<SwappableObject>();if(swap)return swap.transform;
        var rb=t.GetComponentInParent<Rigidbody>();return rb?rb.transform:null;
    }
    internal static void Scan(bool force=false)
    {
        if(scanFrame==Time.frameCount)return;scanFrame=Time.frameCount;
        if(scanning==null)
        {
            if(!force&&Time.unscaledTime<nextScan)return;
            scanning=Object.FindObjectsOfType<MeshFilter>().ToArray();scanAt=0;scanGroups.Clear();scanRoots.Clear();
        }
        var groups=scanGroups;var roots=scanRoots;var timer=System.Diagnostics.Stopwatch.StartNew();
        while(scanAt<scanning.Length)
        {
            if(timer.Elapsed.TotalMilliseconds>=2) return;
            var f=scanning[scanAt++];
            if(!Receiver(f))continue;
            var renderer=f.GetComponent<MeshRenderer>();if(!renderer || !renderer.enabled || !renderer.sharedMaterials.Any(Opaque))continue;
            var canonical=Canonical(f.transform);string key=PathOf(canonical?canonical!:f.transform);
            var physics=f.GetComponentInParent<SwappablePhysicsObject>();Transform? active=physics?physics.transform:canonical;
            if(!groups.TryGetValue(key,out var list)){groups[key]=list=new();roots[key]=active;}list.Add(f);
        }
        scanning=null;nextScan=Time.unscaledTime+3;
        bool changed=All.Count!=groups.Count||groups.Any(p=>!All.TryGetValue(p.Key,out var old)||old.Root!=roots[p.Key]||!old.Filters.Select(f=>f?f.GetInstanceID():0).SequenceEqual(p.Value.Select(f=>f.GetInstanceID())));
        if(!changed)return;Generation++;
        All.Clear();foreach(var pair in groups)All[pair.Key]=new(){Key=pair.Key,Root=roots[pair.Key],Filters=pair.Value.ToArray()};
    }
    static void RegisterHit(Collider collider,Ray ray)
    {
        // The directly aimed object need not wait for the whole scene scan to finish.
        var filters=collider.GetComponentsInChildren<MeshFilter>();
        if(filters.Length==0&&collider.transform.parent)filters=collider.transform.parent.GetComponents<MeshFilter>();
        foreach(var f in filters)
        {
            if(!Receiver(f))continue;
            var renderer=f.GetComponent<MeshRenderer>();if(!renderer||!renderer.enabled||!renderer.sharedMaterials.Any(Opaque)||!renderer.bounds.IntersectRay(ray,out float distance)||distance>Plugin.PaintReach.Value)continue;
            var canonical=Canonical(f.transform);string key=PathOf(canonical?canonical!:f.transform);
            var physics=f.GetComponentInParent<SwappablePhysicsObject>();Transform? active=physics?physics.transform:canonical;
            if(All.TryGetValue(key,out var old)&&old.Root==active&&old.Filters.Any(x=>x==f))continue;
            var list=All.TryGetValue(key,out old)&&old.Root==active?old.Filters.Concat(new[]{f}).Distinct().ToArray():new[]{f};
            All[key]=new(){Key=key,Root=active,Filters=list};Generation++;
        }
    }
    internal static bool Hit(PlayerInteraction p,out Surface? surface,out Vector3 point,out Vector3 normal)
    {
        surface=null;point=normal=default;Scan();var ray=new Ray(p.nlv.transform.position,p.nlv.transform.forward);float physical=Plugin.PaintReach.Value;Collider? obstruction=null;
        foreach(var h in Physics.RaycastAll(ray,physical,~0,QueryTriggerInteraction.Ignore))
            if(h.collider && !PaintTool.Ignore(h.collider)&&h.distance<physical){physical=h.distance;obstruction=h.collider;}
        float best=Plugin.PaintReach.Value;
        if(obstruction)RegisterHit(obstruction!,ray);
        var candidates=new List<(Surface surface,float entry,float limit)>();float pendingEntry=float.PositiveInfinity;
        foreach(var candidate in All.Values)
        {
            float limit=Plugin.PaintReach.Value;float entry=float.PositiveInfinity;
            foreach(var f in candidate.Filters)if(f&&f.GetComponent<MeshRenderer>().bounds.IntersectRay(ray,out float d)&&d<=limit)entry=Mathf.Min(entry,d);
            if(!float.IsFinite(entry))continue;
            limit=obstruction&&candidate.OwnsPrimitive(obstruction!)?Plugin.PaintReach.Value:physical+.08f;
            if(entry<=limit)candidates.Add((candidate,entry,limit));
        }
        foreach(var choice in candidates.OrderBy(c=>c.entry))
        {
            var candidate=choice.surface;float limit=Mathf.Min(best,choice.limit);if(choice.entry>limit)continue;
            var localRay=new Ray(candidate.ToMetric(ray.origin),(candidate.ToMetric(ray.origin+ray.direction)-candidate.ToMetric(ray.origin)).normalized);
            var bounds=new Bounds(localRay.origin,Vector3.zero);bounds.Encapsulate(localRay.GetPoint(limit));bounds.Expand(.001f);
            foreach(var (a,b,c) in candidate.Triangles(bounds))
            {
                if(!Intersect(localRay,a,b,c,out float d) || !PaintRules.NearerHit(d,best,limit))continue;
                best=d;surface=candidate;point=localRay.GetPoint(d);normal=Vector3.Cross(b-a,c-a).normalized;if(Vector3.Dot(normal,localRay.direction)>0)normal=-normal;
            }
            if(candidate.Pending)pendingEntry=Mathf.Min(pendingEntry,choice.entry);
        }
        if(pendingEntry<best){surface=null;return false;}
        return surface!=null && best<=Plugin.PaintReach.Value;
    }
    internal static bool Intersect(Ray ray,Vector3 a,Vector3 b,Vector3 c,out float distance)
    {
        distance=0;var e1=b-a;var e2=c-a;var h=Vector3.Cross(ray.direction,e2);float det=Vector3.Dot(e1,h);
        if(Mathf.Abs(det)<.0000001f)return false;var s=ray.origin-a;float u=Vector3.Dot(s,h)/det;if(u<0||u>1)return false;
        var q=Vector3.Cross(s,e1);float v=Vector3.Dot(ray.direction,q)/det;if(v<0||u+v>1)return false;
        distance=Vector3.Dot(e2,q)/det;return distance>=0;
    }
    internal static Vector3 V(float[] a)=>new(a[0],a[1],a[2]);
    internal static float[] A(Vector3 a)=>new[]{a.x,a.y,a.z};
    internal static void Clear(){All.Clear();warned.Clear();nextScan=0;scanning=null;scanAt=0;scanFrame=-1;scanGroups.Clear();scanRoots.Clear();PaintMeshCache.Clear();}
}
