using Prospice.Placement;
using System.Numerics;

internal static class CargoTests
{
    internal static int Run()
    {
        int passed = 0;
        void Check(bool result, string name) { if (!result) throw new Exception(name); passed++; Console.WriteLine("PASS Cargo: " + name); }
        Check(CargoMath.InColumn(new(-.2f,.1f,-.2f),new(.2f,.5f,.2f),new(-1,0,-1),new(1,1,1)),"bed cargo discovered inside storage footprint");
        Check(CargoMath.InColumn(new(-.2f,3,-.2f),new(.2f,4,.2f),new(-1,0,-1),new(1,1,1)),"stack above trigger ceiling remains a cargo candidate");
        Check(CargoMath.InColumn(new(-.2f,5,-.2f),new(.2f,6,.2f),new(-1,2,-1),new(1,3,1)),"roof stack uses the same unbounded-height discovery");
        Check(!CargoMath.InColumn(new(2,3,2),new(3,4,3),new(-1,0,-1),new(1,1,1)),"nearby objects outside deck footprint excluded");
        Check(!CargoMath.InColumn(new(-.2f,0,-.2f),new(.2f,1,.2f),new(-1,2,-1),new(1,3,1)),"bed cargo below roof cannot be a roof load");
        var square = new[] { new Vector2(-1,-1), new Vector2(1,-1), new Vector2(1,1), new Vector2(-1,1) };
        Check(CargoMath.ProjectGravity(new(1,2,3),new(0,-9.81f,0),Vector3.Zero,Vector3.UnitY,out var level) && Vector3.Distance(level,new(1,0,3))<.00001f,"gravity projects shoulders straight down to level deck");
        var tiltedNormal=Vector3.Normalize(new Vector3(.5f,1,0));
        Check(CargoMath.ProjectGravity(new(1,2,3),new(0,-9.81f,0),Vector3.Zero,tiltedNormal,out var tilted) && Math.Abs(tilted.X-1)<.00001f && Math.Abs(tilted.Y+.5f)<.00001f,"tilted deck hit follows gravity rather than deck normal");
        Check(CargoMath.ProjectGravity(new(0,2,0),new(2,-10,0),Vector3.Zero,Vector3.UnitY,out var angled) && Math.Abs(angled.X-.4f)<.00001f,"uses configured gravity direction");
        Check(!CargoMath.ProjectGravity(Vector3.UnitY,Vector3.Zero,Vector3.Zero,Vector3.UnitY,out _),"zero gravity cannot invent a deck anchor");
        Check(!CargoMath.ProjectGravity(Vector3.UnitY,Vector3.UnitX,Vector3.Zero,Vector3.UnitY,out _),"gravity parallel to deck rejected");
        Check(!CargoMath.ProjectGravity(-Vector3.UnitY,-Vector3.UnitY,Vector3.Zero,Vector3.UnitY,out _),"deck behind gravity ray rejected");
        CargoMath.Contact[] On(int support, IEnumerable<Vector2>? points = null) => (points ?? square).Select(p => new CargoMath.Contact(support,p)).ToArray();
        Check(CargoMath.Supported(square, Vector2.Zero), "four deck contacts support center");
        Check(!CargoMath.Supported(square, new(1.1f,0)), "overhanging center of mass is unstable");
        Check(!CargoMath.Supported(square, new(1,0)), "center on support edge is not stable");
        Check(CargoMath.Supported(square, new(.7f,0)), "modest overhang with center inside support is stable");
        Check(!CargoMath.Supported(new[]{Vector2.Zero,Vector2.UnitX}, Vector2.Zero), "line contact cannot support a load");
        Check(!CargoMath.Supported(square, new(float.NaN,0)), "invalid center rejected");
        Check(CargoMath.Hull(square.Concat(square).Append(Vector2.Zero)).Count == 4, "duplicate and interior contacts do not inflate hull");
        var rotation = Matrix3x2.CreateRotation(.73f);
        Check(CargoMath.Supported(square.Select(p=>Vector2.Transform(p,rotation)),Vector2.Transform(new Vector2(.5f,.2f),rotation)), "rotated contact polygon");
        var concave = new[] {new Vector2(-1,-1), new Vector2(1,-1), new Vector2(1,1), new Vector2(0,0), new Vector2(-1,1)};
        Check(CargoMath.Supported(concave,new(0,.5f)), "compound contact patches form combined support");
        var loads = new[] { new CargoMath.Load(3,Vector2.Zero,On(2)), new CargoMath.Load(2,Vector2.Zero,On(1)), new CargoMath.Load(1,Vector2.Zero,On(0)) };
        Check(CargoMath.Resolve(loads,(_,_)=>true).SequenceEqual(new[]{1,2,3}), "unordered three-high stack resolves bottom-up");
        Check(CargoMath.Resolve(loads, (id,_)=>id!=1).Count==0, "blocked base straps prevent dependent packing");
        Check(CargoMath.Resolve(loads, (id,_)=>id!=2).SequenceEqual(new[]{1}), "blocked middle straps exclude top only");
        Check(CargoMath.Resolve(loads.Where(x=>x.Id!=1).ToArray(),(_,_)=>true).Count==0, "removing base releases whole stack");
        Check(CargoMath.Resolve(new[]{new CargoMath.Load(1,Vector2.Zero,On(2)),new CargoMath.Load(2,Vector2.Zero,On(1))},(_,_)=>true).Count==0, "unsupported cycle cannot seed itself");
        Check(CargoMath.Resolve(new[]{new CargoMath.Load(1,Vector2.Zero,Array.Empty<CargoMath.Contact>())},(_,_)=>true).Count==0, "floating item rejected");
        var bridge=new CargoMath.Load(4,Vector2.Zero,On(1,new[]{new Vector2(-1,-1),new Vector2(-1,1)}).Concat(On(2,new[]{new Vector2(1,-1),new Vector2(1,1)})).ToArray());
        var bases=new[]{new CargoMath.Load(1,Vector2.Zero,On(0)),new CargoMath.Load(2,Vector2.Zero,On(0)),bridge};
        Check(CargoMath.Resolve(bases,(_,_)=>true).Contains(4), "two supports together hold bridge");
        Check(!CargoMath.Resolve(bases.Where(x=>x.Id!=2).ToArray(),(_,_)=>true).Contains(4), "bridge releases after one required support is removed");
        var samples=new[]{new Vector2(-2,0),new Vector2(-1,1),new Vector2(0,.2f),new Vector2(1,1),new Vector2(2,0)};
        var hull=CargoMath.UpperEnvelope(samples);
        Check(hull.Count==4 && Math.Abs(CargoMath.Height(hull,0)-1)<.00001f, "taut belt bridges concavity instead of cutting through shoulders");
        Check(hull[0]==samples[0] && hull[^1]==samples[^1], "route keeps anchor endpoints");
        Check(samples.All(p=>CargoMath.Height(hull,p.X)>=p.Y-.00001f), "route clears every sampled obstruction");
        var random=new Random(941);
        for(int run=0;run<100;run++)
        {
            var p=Enumerable.Range(0,30).Select(i=>new Vector2(i*.03f,(float)random.NextDouble())).ToArray();
            var path=CargoMath.UpperEnvelope(p);
            if(!p.All(q=>CargoMath.Height(path,q.X)>=q.Y-.00001f)) throw new Exception("Randomized belt envelope cuts a sample");
        }
        Check(true,"100 irregular profiles clear sampled cargo geometry");
        for(int cycle=0;cycle<20;cycle++)
            if(CargoMath.Resolve(loads,(_,_)=>true).Count!=3 || CargoMath.Resolve(Array.Empty<CargoMath.Load>(),(_,_)=>true).Count!=0) throw new Exception("Packing lifecycle leaked state");
        Check(true,"repeated solver cycles retain no prior loads");
        return passed;
    }
}
