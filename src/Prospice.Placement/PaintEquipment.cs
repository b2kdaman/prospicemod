using System.Reflection;
using System.Text.Json;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Prospice.Placement;
internal static class PaintEquipment
{
    internal sealed record Item(GameObject Object,Rigidbody Body,Packable Packable,PaintItemState State);
    sealed class Model {public Part[] Parts{get;set;}=Array.Empty<Part>();}
    sealed class Part {public string Name{get;set;}="";public int Slot{get;set;}public float[] Vertices{get;set;}=Array.Empty<float>();public float[] Normals{get;set;}=Array.Empty<float>();public float[] UV{get;set;}=Array.Empty<float>();}
    static readonly Dictionary<string,Mesh[]> models=new();static readonly Dictionary<string,int[]> slots=new();
    static readonly Dictionary<int,Material[]> materials=new();static readonly List<Texture2D> textures=new();
    internal static readonly Dictionary<string,Item> Items=new();
    static byte[] Resource(string name)
    {using var stream=typeof(Plugin).Assembly.GetManifestResourceStream("Paint."+name)??throw new FileNotFoundException("Missing paint asset "+name);using var ms=new MemoryStream();stream.CopyTo(ms);return ms.ToArray();}
    internal static GameObject ModelObject(bool sprayer,int color)
    {
        string key=sprayer?"sprayer":"bucket";
        if(!models.TryGetValue(key,out var meshes))
        {
            var model=JsonSerializer.Deserialize<Model>(Resource(key+".json"))!;meshes=new Mesh[model.Parts.Length];var indices=new int[meshes.Length];
            for(int j=0;j<meshes.Length;j++)
            {
                var p=model.Parts[j];var verts=new Vector3[p.Vertices.Length/3];var normals=new Vector3[verts.Length];var uv=new Vector2[verts.Length];
                for(int i=0;i<verts.Length;i++){verts[i]=new(p.Vertices[i*3],p.Vertices[i*3+1],p.Vertices[i*3+2]);normals[i]=new(p.Normals[i*3],p.Normals[i*3+1],p.Normals[i*3+2]);uv[i]=new(p.UV[i*2],p.UV[i*2+1]);}
                meshes[j]=new Mesh{name="Prospice.Paint."+p.Name};meshes[j].vertices=verts;meshes[j].normals=normals;meshes[j].uv=uv;meshes[j].triangles=Enumerable.Range(0,verts.Length).ToArray();meshes[j].RecalculateBounds();indices[j]=p.Slot;
            }
            models[key]=meshes;slots[key]=indices;
        }
        color=Math.Clamp(color,0,7);
        if(!materials.TryGetValue(color,out var mats))
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader||!shader.isSupported)throw new InvalidOperationException("Equipment lit shader missing");
            mats=new Material[4];ColorUtility.TryParseHtmlString("#"+PaintRules.Hex[color],out var paint);
            var colors=new[]{new Color(.32f,.34f,.36f),Color.white,color==1||color==3||color==5?new Color(.07f,.08f,.09f):new Color(.94f,.93f,.88f),new Color(.035f,.04f,.045f)};
            for(int i=0;i<4;i++){mats[i]=new Material(shader);mats[i].SetColor("_BaseColor",colors[i]);mats[i].SetFloat("_Metallic",i==0?.8f:0);mats[i].SetFloat("_Smoothness",.35f);}
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!ImageConversion.LoadImage(texture,Resource(PaintRules.Names[color].ToLowerInvariant()+".png")))throw new InvalidDataException("Invalid paint texture");
            textures.Add(texture);mats[1].SetTexture("_BaseMap",texture);materials[color]=mats;
        }
        var root=new GameObject("Prospice.Paint."+key);root.SetActive(false);
        for(int j=0;j<meshes.Length;j++){var child=new GameObject("Prospice.Paint.Part");child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=meshes[j];child.AddComponent<MeshRenderer>().sharedMaterial=mats[slots[key][j]];}
        return root;
    }
    internal static Item Spawn(PaintItemState state,Vector3 position,Quaternion rotation)
    {
        if(Items.TryGetValue(state.Id,out var existing)&&existing.Object)return existing;
        var go=ModelObject(state.Sprayer,state.Supply.Color);go.transform.SetPositionAndRotation(position,rotation);
        try{go.tag="Cargo";}catch{ /* Phase 2 accepts Packable components independently of native tag. */ }
        var collider=go.AddComponent<BoxCollider>();collider.center=state.Sprayer?new(0,.105f,0):new(0,.16f,0);collider.size=state.Sprayer?new(.09f,.21f,.2f):new(.28f,.32f,.28f);
        var body=go.AddComponent<Rigidbody>();body.mass=state.Sprayer?.65f:1.2f;body.interpolation=RigidbodyInterpolation.Interpolate;
        var pack=go.AddComponent<Packable>();pack.ropes=new Il2CppSystem.Collections.Generic.List<GameObject>();
        var item=new Item(go,body,pack,state);Items[state.Id]=item;go.SetActive(!state.Stowed&&!state.Inventory);return item;
    }
    internal static Item? Find(Rigidbody? body)=>body?Items.Values.FirstOrDefault(i=>i.Body==body):null;
    internal static Item? Find(Collider collider)=>Find(collider.attachedRigidbody);
    internal static List<PaintItemState> Snapshot()
    {
        foreach(var i in Items.Values)
        {
            if(!i.Object)continue;var s=i.State;var t=i.Object.transform;
            s.Scene=i.Object.scene.path;s.Parent="";
            var owner=PlacementController.Owner;
            // Held objects belong to the world, not to the temporary hand hierarchy.
            Vector3 position=owner && (owner!.heldRigidbody==i.Body||s.Stowed||s.Inventory)?owner.nlv.transform.position+owner.nlv.transform.forward*.6f:t.position;
            s.Position=PaintSurfaces.A(position);s.Rotation=new[]{t.rotation.x,t.rotation.y,t.rotation.z,t.rotation.w};
            if(i.Packable.packed && i.Packable.joint && i.Packable.joint.connectedBody)
            {
                var parent=i.Packable.joint.connectedBody.transform;s.Parent=PaintSurfaces.PathOf(parent);s.Position=PaintSurfaces.A(parent.InverseTransformPoint(t.position));var q=Quaternion.Inverse(parent.rotation)*t.rotation;s.Rotation=new[]{q.x,q.y,q.z,q.w};
            }
        }
        return Items.Values.Where(i=>i.Object).Select(i=>i.State).ToList();
    }
    internal static void Clear(){foreach(var i in Items.Values)if(i.Object){if(i.Packable.packed)i.Packable.bxg();Object.Destroy(i.Object);}Items.Clear();}
    internal static void Dispose(){Clear();foreach(var a in models.Values)foreach(var m in a)Object.Destroy(m);models.Clear();slots.Clear();foreach(var a in materials.Values)foreach(var m in a)Object.Destroy(m);materials.Clear();foreach(var t in textures)Object.Destroy(t);textures.Clear();}
}
