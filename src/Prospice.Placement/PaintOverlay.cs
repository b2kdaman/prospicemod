using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace Prospice.Placement;
// Each half-metre surface patch owns a UV-independent texture. Patches are packed
// into shared 1024-square atlases (64 patches/page), streamed within 45 metres.
internal static class PaintOverlay
{
    const int Size=128,AtlasSize=1024,PerPage=64,MaxPages=16;
    const float Cell=.5f,Depth=.05f;
    sealed class Page { internal Texture2D Texture=null!;internal Material Material=null!;internal int Used;internal Stack<int> Free=new();internal bool Dirty; }
    sealed class Visual { internal GameObject Object=null!;internal Mesh Mesh=null!;internal Page Page=null!;internal int Slot;internal Transform? Root;internal int Signature; }
    static readonly Dictionary<string,PaintTileState> tiles=new();
    static readonly Dictionary<string,Visual> visuals=new();
    static readonly List<Page> pages=new();
    static readonly RaycastHit[] visibilityHits=new RaycastHit[64];
    static float nextStream;static bool rebuild;
    static readonly Dictionary<string,Patch> geometryCache=new();static int generation=-1;
    internal static string Status="";
    static void Axes(int face,out Vector3 u,out Vector3 v,out Vector3 n)
    { int axis=face/2; n=axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward;if(face%2==1)n=-n;u=axis==0?Vector3.forward:Vector3.right;v=Vector3.Cross(n,u); }
    static int Face(Vector3 n){var a=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));int axis=a.x>a.y&&a.x>a.z?0:a.y>a.z?1:2;return axis*2+(n[axis]<0?1:0);}
    static Vector3 Center(PaintTileState t){Axes(t.Face,out var u,out var v,out var n);return u*((t.X+.5f)*Cell)+v*((t.Y+.5f)*Cell)+n*((t.Z+.5f)*Depth);}
    internal static void Bind(IEnumerable<PaintTileState> data){Clear();foreach(var tile in data)tiles.Add(tile.Key,tile);rebuild=true;}
    internal static List<PaintTileState> Snapshot()=>tiles.Values.ToList();
    internal static bool Dab(PaintSurfaces.Surface surface,Vector3 point,Vector3 normal,Vector3 eye,float radius,byte[] color,double seconds)
    {
        var toWorld=surface.MetricToWorld;
        int face=Face(normal);Axes(face,out var u,out var v,out var n);
        float px=Vector3.Dot(point,u),py=Vector3.Dot(point,v),pz=Vector3.Dot(point,n);
        int z=(int)MathF.Floor(pz/Depth);bool changed=false;
        for(int x=(int)MathF.Floor((px-radius)/Cell);x<=(int)MathF.Floor((px+radius)/Cell);x++)
        for(int y=(int)MathF.Floor((py-radius)/Cell);y<=(int)MathF.Floor((py+radius)/Cell);y++)
        {
            // Cover neighbouring depth slices on curved trunks and oblique panels.
            for(int layer=(int)MathF.Floor((pz-radius)/Depth);layer<=(int)MathF.Floor((pz+radius)/Depth);layer++)
            {
                var descriptor=new PaintTileState{Surface=surface.Key,Face=face,X=x,Y=y,Z=layer};
                var center=Center(descriptor);var geometry=Geometry(surface,descriptor);
                if(geometry.vertices.Count==0)continue;
                if(!tiles.TryGetValue(descriptor.Key,out var tile))
                {
                    if(tiles.Count>=8192){Status="Paint storage full";return changed;}
                    tile=descriptor;tile.Center=PaintSurfaces.A(toWorld.MultiplyPoint3x4(center));
                }
                byte[] rgb=color;float softness=PaintRules.Softness(Vector3.Distance(eye,toWorld.MultiplyPoint3x4(point)));
                bool dirty=false;var visibility=new Dictionary<int,bool>();
                // Rasterize only actual, front-facing receiving triangles. Visibility rays
                // prevent a projected stroke reaching through another physical surface.
                for(int t=0;t<geometry.vertices.Count;t+=3)
                {
                    var a=geometry.metric[t];var b=geometry.metric[t+1];var c=geometry.metric[t+2];
                    Vector2 UV(Vector3 q)=>new((Vector3.Dot(q,u)-x*Cell)/Cell,(Vector3.Dot(q,v)-y*Cell)/Cell);
                    var aa=UV(a);var bb=UV(b);var cc=UV(c);float area=Cross(bb-aa,cc-aa);if(Mathf.Abs(area)<1e-8f)continue;
                    int minX=Math.Clamp((int)(Mathf.Min(aa.x,Mathf.Min(bb.x,cc.x))*Size),0,Size-1),maxX=Math.Clamp((int)MathF.Ceiling(Mathf.Max(aa.x,Mathf.Max(bb.x,cc.x))*Size),0,Size-1);
                    int minY=Math.Clamp((int)(Mathf.Min(aa.y,Mathf.Min(bb.y,cc.y))*Size),0,Size-1),maxY=Math.Clamp((int)MathF.Ceiling(Mathf.Max(aa.y,Mathf.Max(bb.y,cc.y))*Size),0,Size-1);
                    for(int iy=minY;iy<=maxY;iy++)for(int ix=minX;ix<=maxX;ix++)
                    {
                        float qx=(ix+.5f)/Size-aa.x,qy=(iy+.5f)/Size-aa.y;
                        float wb=(qx*(cc.y-aa.y)-qy*(cc.x-aa.x))/area,wc=((bb.x-aa.x)*qy-(bb.y-aa.y)*qx)/area,wa=1-wb-wc;
                        if(wa<0||wb<0||wc<0)continue;
                        float sx=a.x*wa+b.x*wb+c.x*wc,sy=a.y*wa+b.y*wb+c.y*wc,sz=a.z*wa+b.z*wb+c.z*wc;
                        float dx=sx-point.x,dy=sy-point.y,dz=sz-point.z,r=MathF.Sqrt(dx*dx+dy*dy+dz*dz)/radius;if(r>=1)continue;
                        int visibilityKey=(iy/8)*(Size/8)+ix/8;
                        if(!visibility.TryGetValue(visibilityKey,out bool visible))
                        {
                            var world=toWorld.MultiplyPoint3x4(new Vector3(sx,sy,sz));var direction=world-eye;float distance=direction.magnitude;
                            int hits=Physics.RaycastNonAlloc(eye,direction.normalized,visibilityHits,Mathf.Max(0,distance-.04f),~(1<<2),QueryTriggerInteraction.Ignore);
                            visible=hits<visibilityHits.Length;
                            for(int h=0;visible&&h<hits;h++)if(visibilityHits[h].collider&&!PaintTool.Ignore(visibilityHits[h].collider)&&!surface.OwnsPrimitive(visibilityHits[h].collider))visible=false;
                            visibility[visibilityKey]=visible;
                        }
                        if(!visible)continue;
                        float alpha=PaintRules.Coverage(r,softness,seconds,radius);if(alpha<=0)continue;
                        if(tile.Pixels.Length==0)tile.Pixels=new byte[Size*Size*4];
                        PaintRules.Blend(tile.Pixels,(iy*Size+ix)*4,rgb,alpha);dirty=changed=true;
                    }
                }
                if(dirty && !tiles.ContainsKey(tile.Key)){tiles.Add(tile.Key,tile);rebuild=true;}
                if(dirty && visuals.TryGetValue(tile.Key,out var visual))Upload(tile,visual,false);
            }
        }
        if(changed)PaintSession.Dirty();return changed;
    }
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    internal static void Stream(Vector3 eye)
    {
        foreach(var dirty in pages.Where(p=>p.Dirty)){dirty.Texture.Apply(false,false);dirty.Dirty=false;}
        if(Time.unscaledTime<nextStream && !rebuild)return;nextStream=Time.unscaledTime+1;
        PaintSurfaces.Scan();
        var wanted=tiles.Values.Where(t=>PaintSurfaces.All.TryGetValue(t.Surface,out var s) && Vector3.Distance(s.ToWorld(Center(t)),eye)<45).OrderBy(t=>Vector3.SqrMagnitude(PaintSurfaces.All[t.Surface].ToWorld(Center(t))-eye)).Take(MaxPages*PerPage).ToArray();
        var keys=wanted.Select(t=>t.Key).ToHashSet();
        foreach(var pair in visuals.ToArray())
        {
            if(keys.Contains(pair.Key)&&PaintSurfaces.All.TryGetValue(tiles[pair.Key].Surface,out var current)&&current.Root==pair.Value.Root&&current.Signature==pair.Value.Signature)continue;
            var old=pair.Value;if(old.Object)Object.Destroy(old.Object);if(old.Mesh)Object.Destroy(old.Mesh);old.Page.Free.Push(old.Slot);visuals.Remove(pair.Key);
        }
        rebuild=false;int created=0;
        foreach(var tile in wanted)
        {
            if(visuals.ContainsKey(tile.Key))continue;
            if(created>=4){rebuild=true;break;}
            var surface=PaintSurfaces.All[tile.Surface];var geometry=Geometry(surface,tile);if(geometry.vertices.Count==0){if(PaintMeshCache.Preparing)rebuild=true;continue;}created++;
            Page? page=pages.FirstOrDefault(p=>p.Free.Count>0||p.Used<PerPage);if(page==null){if(pages.Count>=MaxPages)continue;page=CreatePage();pages.Add(page);}int slot=page.Free.Count>0?page.Free.Pop():page.Used++;
            var go=new GameObject("Prospice.Paint.Overlay");go.layer=0;if(surface.Root)go.transform.SetParent(surface.Root,false);
            var mesh=new Mesh{name="Prospice.Paint.Patch",indexFormat=IndexFormat.UInt32};mesh.vertices=geometry.vertices.ToArray();mesh.normals=geometry.normals.ToArray();
            var uv=geometry.uv.Select(p=>new Vector2(((slot%8)*Size+1+p.x*(Size-2))/AtlasSize,((slot/8)*Size+1+p.y*(Size-2))/AtlasSize)).ToArray();mesh.uv=uv;mesh.triangles=Enumerable.Range(0,uv.Length).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=page.Material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            var visual=new Visual{Object=go,Mesh=mesh,Page=page,Slot=slot,Root=surface.Root,Signature=surface.Signature};visuals.Add(tile.Key,visual);Upload(tile,visual,false);
        }
        foreach(var page in pages.Where(p=>p.Dirty)){page.Texture.Apply(false,false);page.Dirty=false;}
    }
    static Page CreatePage()
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader||!shader.isSupported)throw new InvalidOperationException("Paint requires shipped URP/Lit shader.");
        var mat=new Material(shader);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.2f);mat.SetFloat("_Surface",1);mat.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);mat.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);mat.SetInt("_ZWrite",0);mat.SetInt("_Cull",(int)CullMode.Off);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;
        var texture=new Texture2D(AtlasSize,AtlasSize,TextureFormat.RGBA32,false);texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",Color.white);
        return new(){Texture=texture,Material=mat};
    }
    static void Upload(PaintTileState tile,Visual visual,bool apply=true)
    {
        var colors=new Color[Size*Size];for(int i=0;i<colors.Length;i++)colors[i]=new Color{r=tile.Pixels[i*4]/255f,g=tile.Pixels[i*4+1]/255f,b=tile.Pixels[i*4+2]/255f,a=tile.Pixels[i*4+3]/255f};
        visual.Page.Dirty=true;visual.Page.Texture.SetPixels((visual.Slot%8)*Size,(visual.Slot/8)*Size,Size,Size,colors);if(apply)visual.Page.Texture.Apply(false,false);
    }
    sealed class Patch {internal List<Vector3> vertices=new(),metric=new(),normals=new();internal List<Vector2> uv=new();}
    static Patch Geometry(PaintSurfaces.Surface surface,PaintTileState tile)
    {
        if(generation!=PaintSurfaces.Generation){geometryCache.Clear();generation=PaintSurfaces.Generation;}
        if(geometryCache.TryGetValue(tile.Key,out var cached))return cached;
        var patch=new Patch();Axes(tile.Face,out var u,out var v,out var n);var center=Center(tile);var size=Abs(u)*Cell+Abs(v)*Cell+Abs(n)*Depth;var bounds=new Bounds(center,size);
        foreach(var (a,b,c) in surface.Triangles(bounds))
        {
            var normal=Vector3.Cross(b-a,c-a).normalized;float facing=Vector3.Dot(normal,n);if(Mathf.Abs(facing)<.3f)continue;if(facing<0)normal=-normal;
            var polygon=new List<Vector3>{a,b,c};
            foreach(var axis in new[]{u,v,n})
            {
                float half=axis==n?Depth*.5f:Cell*.5f;
                polygon=Clip(polygon,axis,Vector3.Dot(center,axis)+half);polygon=Clip(polygon,-axis,-Vector3.Dot(center,axis)+half);
            }
            for(int i=1;i<polygon.Count-1;i++)foreach(var p in new[]{polygon[0],polygon[i],polygon[i+1]})
            {patch.metric.Add(p);patch.vertices.Add(surface.ToLocal(p+normal*.001f));patch.normals.Add(normal);patch.uv.Add(new((Vector3.Dot(p,u)-tile.X*Cell)/Cell,(Vector3.Dot(p,v)-tile.Y*Cell)/Cell));}
        }
        if(surface.Pending)return new Patch();
        if(patch.vertices.Count>0&&geometryCache.Count<2048)geometryCache[tile.Key]=patch;return patch;
    }
    static Vector3 Abs(Vector3 a)=>new(Mathf.Abs(a.x),Mathf.Abs(a.y),Mathf.Abs(a.z));
    static List<Vector3> Clip(List<Vector3> points,Vector3 normal,float distance)
    {
        var result=new List<Vector3>();if(points.Count==0)return result;var prev=points[^1];float dp=Vector3.Dot(prev,normal)-distance;
        foreach(var p in points){float d=Vector3.Dot(p,normal)-distance;if((d<=0)!=(dp<=0))result.Add(Vector3.LerpUnclamped(prev,p,dp/(dp-d)));if(d<=0)result.Add(p);prev=p;dp=d;}return result;
    }
    static void DisposeVisuals(){foreach(var v in visuals.Values){if(v.Object)Object.Destroy(v.Object);if(v.Mesh)Object.Destroy(v.Mesh);}visuals.Clear();foreach(var p in pages){Object.Destroy(p.Material);Object.Destroy(p.Texture);}pages.Clear();}
    internal static void Clear(){DisposeVisuals();tiles.Clear();geometryCache.Clear();Status="";nextStream=0;rebuild=false;}
}
