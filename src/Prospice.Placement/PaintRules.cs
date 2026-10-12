using System.Numerics;
namespace Prospice.Placement;

internal static class PaintRules
{
    internal static readonly string[] Names = { "Black", "White", "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta" };
    internal static readonly string[] Hex = { "202124", "F2F0E8", "D83A36", "F2C842", "39965B", "35B5BF", "3465CE", "B944A1" };
    internal const double BucketMl = 1000, TankMl = 250, MlPerSecond = TankMl / 120, RefillMlPerSecond = 50;
    internal static bool ValidColor(int color) => color >= 0 && color < Names.Length;
    internal static float AdjustBrush(float current,float axis,float seconds,float rate,float min,float max)
    {
        if(!float.IsFinite(axis)||!float.IsFinite(seconds)||seconds<=0)return current;
        float input=Math.Abs(axis)<=.25f?0:Math.Sign(axis)*(Math.Clamp(Math.Abs(axis),0,1)-.25f)/.75f;
        return Math.Clamp(current+input*Math.Min(seconds,.1f)*rate,min,max);
    }
    internal static bool NearerHit(float distance,float best,float limit)=>float.IsFinite(distance)&&distance>=.03f&&distance<=limit&&distance<best;
    internal static float Radius(float distance) => .01f + Math.Clamp((distance - .1f) / 1.9f, 0, 1.526316f) * .14f;
    internal static float Softness(float distance) => .15f + .65f * Math.Clamp(distance / 3, 0, 1);
    internal static double Consume(PaintSupply tank, double seconds)
    {
        if (!ValidColor(tank.Color) || !double.IsFinite(seconds) || seconds <= 0) return 0;
        double amount = Math.Min(tank.Ml, seconds * MlPerSecond); tank.Ml -= amount;
        if (tank.Ml < 1e-7) { tank.Ml = 0; tank.Color = -1; tank.MixedRgb = null; }
        return amount;
    }
    internal static bool ValidMix(double[]? rgb)=>rgb==null || rgb.Length==3&&rgb.All(v=>double.IsFinite(v)&&v>=0&&v<=255);
    internal static double[] Components(PaintSupply supply)=>supply.MixedRgb??Rgb(supply.Color).Select(v=>(double)v).ToArray();
    internal static byte[] Rgb(PaintSupply supply)=>Components(supply).Select(v=>(byte)Math.Clamp((int)Math.Round(v),0,255)).ToArray();
    internal static double Refill(PaintSupply bucket, PaintSupply tank,double seconds)
    {
        if(!double.IsFinite(seconds)||seconds<=0||!ValidColor(bucket.Color)||!ValidMix(bucket.MixedRgb)||!ValidMix(tank.MixedRgb)||
           !double.IsFinite(bucket.Ml)||!double.IsFinite(tank.Ml)||bucket.Ml<=0||tank.Ml<0||tank.Ml>TankMl||tank.Ml>0&&!ValidColor(tank.Color))return 0;
        double amount=Math.Min(seconds*RefillMlPerSecond,Math.Min(bucket.Ml,TankMl-tank.Ml));
        if(amount<=0)return 0;
        var incoming=Components(bucket);
        if(tank.Ml<=0){tank.Color=bucket.Color;tank.MixedRgb=bucket.MixedRgb==null?null:(double[])incoming.Clone();}
        else if(tank.Color!=bucket.Color||tank.MixedRgb!=null||bucket.MixedRgb!=null)
        {
            var current=Components(tank);var mixed=new double[3];
            for(int c=0;c<3;c++)mixed[c]=Math.Clamp((current[c]*tank.Ml+incoming[c]*amount)/(tank.Ml+amount),0,255);
            tank.MixedRgb=mixed;
        }
        tank.Ml+=amount;bucket.Ml=Math.Max(0,bucket.Ml-amount);return amount;
    }
    internal static int Percent(double amount, double capacity) => (int)Math.Ceiling(Math.Clamp(amount / capacity * 100, 0, 100));
    internal static byte[] Rgb(int color) => Convert.FromHexString(Hex[color]);
    // Straight alpha storage. Repeated dabs compose without dark transparent fringes.
    internal static void Blend(byte[] pixels, int offset, byte[] color, float opacity)
    {
        float a = Math.Clamp(opacity, 0, 1), old = pixels[offset + 3] / 255f, result = a + old * (1 - a);
        if (result <= 0) return;
        for (int c = 0; c < 3; c++) pixels[offset+c] = (byte)Math.Clamp((int)MathF.Round((color[c]*a + pixels[offset+c]*old*(1-a))/result), 0, 255);
        pixels[offset+3] = (byte)Math.Clamp((int)MathF.Round(result*255),0,255);
    }
    internal static float Coverage(float normalizedRadius, float softness, double seconds, float radius)
    {
        if (normalizedRadius >= 1) return 0;
        float edge = Math.Clamp((1-normalizedRadius)/softness,0,1);
        return (float)(1 - Math.Exp(-seconds * 54 * edge * edge * (3-2*edge)));
    }
    internal static bool Finite(float[] values, int length) => values != null && values.Length == length && values.All(float.IsFinite);
}
internal sealed class PaintSupply
{
    public int Color { get; set; } = -1;
    public double Ml { get; set; }
    public double[]? MixedRgb { get; set; }
}
internal sealed class PaintItemState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Sprayer { get; set; }
    public bool Stowed { get; set; }
    public bool Inventory { get; set; }
    public PaintSupply Supply { get; set; } = new();
    public string Scene { get; set; } = "";
    public string Parent { get; set; } = "";
    public float[] Position { get; set; } = new float[3];
    public float[] Rotation { get; set; } = new float[] {0,0,0,1};
}
internal sealed class PaintTileState
{
    public string Surface { get; set; } = "";
    public int Face { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public byte[] Pixels { get; set; } = Array.Empty<byte>();
    public float[] Center { get; set; } = new float[3];
    public string Key => $"{Surface}|{Face}|{X}|{Y}|{Z}";
}
internal sealed class PaintDocument
{
    public int Version { get; set; } = 1;
    public string World { get; set; } = "";
    public string NativeHash { get; set; } = "";
    public List<PaintItemState> Items { get; set; } = new();
    public List<PaintTileState> Tiles { get; set; } = new();
    internal PaintItemState EnsureAirbrush()
    {
        var item=Items.FirstOrDefault(i=>i.Sprayer&&i.Inventory)??Items.FirstOrDefault(i=>i.Sprayer);
        if(item==null){item=new PaintItemState{Sprayer=true};Items.Add(item);}
        item.Inventory=true;item.Stowed=true;item.Parent="";
        return item;
    }
    internal void Validate()
    {
        if(Version!=1 || Items.Count>2048 || Tiles.Count>8192) throw new InvalidDataException("Unsupported paint save or capacity.");
        var ids=new HashSet<string>();
        foreach(var i in Items)
            if((i.Stowed||i.Inventory)&&!i.Sprayer || !Guid.TryParseExact(i.Id,"N",out _) || !ids.Add(i.Id) || !PaintRules.Finite(i.Position,3) || !PaintRules.Finite(i.Rotation,4) ||
               !PaintRules.ValidMix(i.Supply.MixedRgb) || !i.Sprayer&&i.Supply.MixedRgb!=null || !double.IsFinite(i.Supply.Ml) || i.Supply.Ml<0 || i.Supply.Ml>(i.Sprayer?250:1000) ||
               (!PaintRules.ValidColor(i.Supply.Color) && !(i.Sprayer && i.Supply.Color==-1 && i.Supply.Ml==0))) throw new InvalidDataException("Invalid paint item.");
        ids.Clear();
        foreach(var t in Tiles)
            if(string.IsNullOrEmpty(t.Surface) || t.Surface.Length>4096 || !ids.Add(t.Key) || t.Face<0 || t.Face>5 ||
               t.Pixels.Length!=128*128*4 || !PaintRules.Finite(t.Center,3)) throw new InvalidDataException("Invalid paint tile.");
    }
}
