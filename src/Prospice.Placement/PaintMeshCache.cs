using System.Text;
using UnityEngine;
namespace Prospice.Placement;
// Capture Unity metadata on the main thread. File IO and BVH construction use managed workers only.
internal static class PaintMeshCache
{
    internal sealed record Geometry(Vector3[] Vertices,int[][] Triangles)
    {
        internal readonly PaintSpatialIndex Index=new(Vertices.Select(v=>new System.Numerics.Vector3(v.x,v.y,v.z)).ToArray(),Triangles);
    }
    sealed record Entry(long Offset,int Vertices,int[] Counts,Vector3 Center,Vector3 Extents);
    sealed record Request(string Name,int Vertices,int[] Counts,Vector3 Center,Vector3 Extents,string Path,int First,int Count);
    static readonly Dictionary<(int Mesh,int First,int Count),(Geometry data,long stamp,long bytes)> loaded=new();
    static readonly Dictionary<(int Mesh,int First,int Count),Task<Geometry>> pending=new();
    static readonly Dictionary<(int Mesh,int First,int Count),string> failures=new();
    static Task<Dictionary<string,List<Entry>>>? directory;
    static long clock,bytes;
    const long Budget=128L*1024*1024;
    internal static bool Preparing=>pending.Count>0;
    static int Count(BinaryReader r,int max){int n=r.ReadInt32();if(n<0||n>max)throw new InvalidDataException("Invalid paint mesh count");return n;}
    static Vector3 V(BinaryReader r)=>new(){x=r.ReadSingle(),y=r.ReadSingle(),z=r.ReadSingle()};
    static Dictionary<string,List<Entry>> ReadDirectory(string path)
    {
        var index=new Dictionary<string,List<Entry>>();using var stream=File.OpenRead(path);using var r=new BinaryReader(stream,Encoding.UTF8);
        if(Encoding.ASCII.GetString(r.ReadBytes(8))!="PPAINT01"||Encoding.ASCII.GetString(r.ReadBytes(64))!="511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D")throw new InvalidDataException("Paint cache game/version mismatch");
        int count=Count(r,65536);
        for(int i=0;i<count;i++)
        {
            string name=Encoding.UTF8.GetString(r.ReadBytes(Count(r,4096)));int nv=Count(r,2000000),ns=Count(r,4096);var center=V(r);var extents=V(r);
            long offset=stream.Position;stream.Seek(nv*20L,SeekOrigin.Current);var counts=new int[ns];
            for(int s=0;s<ns;s++){counts[s]=Count(r,12000000);if(counts[s]%3!=0)throw new InvalidDataException("Invalid triangles");stream.Seek(counts[s]*4L,SeekOrigin.Current);}
            if(stream.Position>stream.Length)throw new EndOfStreamException();
            if(!index.TryGetValue(name,out var list))index[name]=list=new();list.Add(new(offset,nv,counts,center,extents));
        }
        if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing mesh data");return index;
    }
    static bool Near(Vector3 a,Vector3 b){float x=a.x-b.x,y=a.y-b.y,z=a.z-b.z;return x*x+y*y+z*z<1e-8f;}
    static Geometry Load(Request request,Dictionary<string,List<Entry>> index)
    {
        if(!index.TryGetValue(request.Name,out var choices))throw new InvalidDataException("No paint geometry: "+request.Name);
        var entry=choices.SingleOrDefault(e=>e.Vertices==request.Vertices&&e.Counts.Length==request.Counts.Length&&e.Counts.Select((n,i)=>request.Counts[i]<0||n==request.Counts[i]).All(x=>x)&&Near(e.Center,request.Center)&&Near(e.Extents,request.Extents))??throw new InvalidDataException("Ambiguous paint geometry: "+request.Name);
        using var stream=File.OpenRead(request.Path);using var r=new BinaryReader(stream,Encoding.UTF8);stream.Position=entry.Offset;
        var vertices=new Vector3[entry.Vertices];for(int i=0;i<vertices.Length;i++)vertices[i]=V(r);stream.Seek(vertices.Length*8L,SeekOrigin.Current);
        var triangles=new int[entry.Counts.Length][];
        for(int s=0;s<triangles.Length;s++){int n=Count(r,12000000);if(s<request.First||s>=request.First+request.Count){triangles[s]=Array.Empty<int>();stream.Seek(n*4L,SeekOrigin.Current);continue;}triangles[s]=new int[n];for(int i=0;i<n;i++){int v=r.ReadInt32();if(v<0||v>=vertices.Length)throw new InvalidDataException("Invalid vertex index");triangles[s][i]=v;}}
        return new(vertices,triangles);
    }
    internal static Geometry? Get(Mesh mesh,int firstSlot,int slotCount)
    {
        var id=(mesh.GetInstanceID(),firstSlot,slotCount);
        if(loaded.TryGetValue(id,out var old)){loaded[id]=(old.data,++clock,old.bytes);return old.data;}
        if(failures.TryGetValue(id,out var failure))throw new InvalidDataException(failure);
        // Drain all finished work, even if the player has looked away from that mesh.
        foreach(var pair in pending.Where(p=>p.Value.IsCompleted).ToArray())
        {
            pending.Remove(pair.Key);
            if(!pair.Value.IsCompletedSuccessfully){failures[pair.Key]=pair.Value.Exception?.GetBaseException().Message??"Mesh preparation failed";continue;}
            var data=pair.Value.Result;long size=data.Vertices.LongLength*12+data.Triangles.Sum(t=>t.LongLength*32);
            while(loaded.Count>0&&bytes+size>Budget){var evict=loaded.MinBy(x=>x.Value.stamp);bytes-=evict.Value.bytes;loaded.Remove(evict.Key);}
            // Retain a single oversized batch instead of rebuilding it on every stroke.
            loaded[pair.Key]=(data,++clock,size);bytes+=size;
        }
        if(loaded.TryGetValue(id,out old))return old.data;
        if(pending.ContainsKey(id)||pending.Count>=2)return null;
        // Built-in and runtime primitives need not match the private asset cache.
        // Copy only bounded readable meshes on the Unity thread; index them off-thread.
        if(mesh.isReadable&&mesh.vertexCount<=65536)
        {
            var vertices=mesh.vertices.ToArray();var triangles=Enumerable.Range(0,mesh.subMeshCount).Select(slot=>slot>=firstSlot&&slot<firstSlot+slotCount?mesh.GetTriangles(slot).ToArray():Array.Empty<int>()).ToArray();
            pending[id]=Task.Run(()=>new Geometry(vertices,triangles));return null;
        }
        string path=System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath,"cache","prospice-paint-511B68EBDE54.bin");
        directory??=Task.Run(()=>ReadDirectory(path));var indexTask=directory;
        var bounds=mesh.bounds;var request=new Request(mesh.name,mesh.vertexCount,Enumerable.Range(0,mesh.subMeshCount).Select(s=>s>=firstSlot&&s<firstSlot+slotCount?(int)mesh.GetIndexCount(s):-1).ToArray(),bounds.center,bounds.extents,path,firstSlot,slotCount);
        pending[id]=Task.Run(async()=>Load(request,await indexTask.ConfigureAwait(false)));
        return null;
    }
    internal static void Clear(){loaded.Clear();pending.Clear();failures.Clear();directory=null;bytes=clock=0;}
}
