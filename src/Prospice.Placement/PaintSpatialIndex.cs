using System.Numerics;
namespace Prospice.Placement;
internal sealed class PaintSpatialIndex
{
    internal readonly record struct Triangle(int A,int B,int C,int Slot);
    readonly record struct Box(Vector3 Min,Vector3 Max)
    {
        internal bool Intersects(Vector3 min,Vector3 max)=>Min.X<=max.X&&Max.X>=min.X&&Min.Y<=max.Y&&Max.Y>=min.Y&&Min.Z<=max.Z&&Max.Z>=min.Z;
    }
    sealed record Node(Box Box,int Start,int Count,Node? Left,Node? Right);
    readonly Triangle[] triangles;readonly Vector3[] centers;readonly Box[] boxes;readonly int[] order;readonly Node? root;
    internal PaintSpatialIndex(Vector3[] vertices,int[][] groups)
    {
        var list=new List<Triangle>();for(int s=0;s<groups.Length;s++){var g=groups[s];if(g.Length%3!=0)throw new InvalidDataException("Triangle count");for(int i=0;i<g.Length;i+=3)list.Add(new(g[i],g[i+1],g[i+2],s));}
        triangles=list.ToArray();centers=new Vector3[triangles.Length];boxes=new Box[triangles.Length];order=Enumerable.Range(0,triangles.Length).ToArray();
        for(int i=0;i<triangles.Length;i++){var t=triangles[i];if(t.A<0||t.B<0||t.C<0||t.A>=vertices.Length||t.B>=vertices.Length||t.C>=vertices.Length)throw new InvalidDataException("Triangle index");var a=vertices[t.A];var b=vertices[t.B];var c=vertices[t.C];boxes[i]=new(Vector3.Min(a,Vector3.Min(b,c)),Vector3.Max(a,Vector3.Max(b,c)));centers[i]=(a+b+c)/3;}
        if(order.Length>0)root=Build(0,order.Length);
    }
    Node Build(int start,int count)
    {
        var min=new Vector3(float.PositiveInfinity);var max=new Vector3(float.NegativeInfinity);
        for(int i=start;i<start+count;i++){var b=boxes[order[i]];min=Vector3.Min(min,b.Min);max=Vector3.Max(max,b.Max);}
        var box=new Box(min,max);if(count<=16)return new(box,start,count,null,null);
        var size=max-min;int axis=size.X>=size.Y&&size.X>=size.Z?0:size.Y>=size.Z?1:2;
        float Component(Vector3 p)=>axis==0?p.X:axis==1?p.Y:p.Z;
        Array.Sort(order,start,count,Comparer<int>.Create((a,b)=>Component(centers[a]).CompareTo(Component(centers[b]))));
        int half=count/2;return new(box,start,count,Build(start,half),Build(start+half,count-half));
    }
    internal IEnumerable<Triangle> Query(Vector3 min,Vector3 max,int firstSlot,int count)
    {
        if(root==null)yield break;var stack=new Stack<Node>();stack.Push(root);
        while(stack.Count>0){var n=stack.Pop();if(!n.Box.Intersects(min,max))continue;if(n.Left!=null){stack.Push(n.Left);stack.Push(n.Right!);continue;}for(int i=n.Start;i<n.Start+n.Count;i++){int id=order[i];var t=triangles[id];if(t.Slot>=firstSlot&&t.Slot<firstSlot+count&&boxes[id].Intersects(min,max))yield return t;}}
    }
}
