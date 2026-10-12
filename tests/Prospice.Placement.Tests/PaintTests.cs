using Prospice.Placement;
using System.Numerics;
static class PaintTests
{
    internal static int Run()
    {
        int count=0;void Check(bool b,string label){if(!b)throw new Exception("Paint: "+label);count++;Console.WriteLine("PASS Paint: "+label);}
        Check(PaintRules.NearerHit(.5f,2,3),"nearer triangle replaces farther target");
        Check(!PaintRules.NearerHit(2,.5f,3),"later farther triangle cannot replace nearest target");
        Check(!PaintRules.NearerHit(.5f,3,.4f),"occluder limits receiving triangles");
        Check(!PaintRules.NearerHit(float.NaN,3,3)&&!PaintRules.NearerHit(float.PositiveInfinity,3,3)&&!PaintRules.NearerHit(-1,3,3),"invalid hit distances rejected");
        foreach(var order in new[]{new[]{2f,.5f,1f},new[]{.5f,1f,2f},new[]{1f,2f,.5f}})
        {float nearest=3;foreach(float distance in order)if(PaintRules.NearerHit(distance,nearest,3))nearest=distance;Check(nearest==.5f,"nearest hit independent of triangle order");}
        var subset=new PaintSpatialIndex(new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(0,1,0)},new[]{Array.Empty<int>(),new[]{0,1,2},Array.Empty<int>()});
        Check(subset.Query(new(-1,-1,-1),new(2,2,2),1,1).Single().Slot==1,"partial batch preserves original material slot");
        Check(!subset.Query(new(-1,-1,-1),new(2,2,2),0,1).Any(),"partial batch cannot leak into another renderer");
        Check(PaintRules.AdjustBrush(.5f,.2f,.1f,.5f,.05f,1)==.5f,"brush adjustment ignores stick drift");
        Check(PaintRules.AdjustBrush(.99f,1,.1f,.5f,.05f,1)==1,"alpha upper limit");
        Check(PaintRules.AdjustBrush(.06f,-1,.1f,.5f,.05f,1)==.05f,"alpha lower limit");
        Check(PaintRules.AdjustBrush(1.99f,1,.1f,.75f,.25f,2)==2,"brush size upper limit");
        Check(PaintRules.AdjustBrush(.26f,-1,.1f,.75f,.25f,2)==.25f,"brush size lower limit");
        foreach(int hz in new[]{30,60,144})
        {float alpha=.2f;for(int i=0;i<hz;i++)alpha=PaintRules.AdjustBrush(alpha,1,1f/hz,.5f,.05f,1);Check(Math.Abs(alpha-.7f)<.0001f,"brush adjustment rate independent at "+hz+" Hz");}
        Check(PaintRules.AdjustBrush(.5f,float.NaN,.1f,.5f,.05f,1)==.5f,"invalid brush input ignored");
        var freshInventory=new PaintDocument();var builtin=freshInventory.EnsureAirbrush();
        Check(builtin.Inventory&&builtin.Stowed&&builtin.Sprayer&&builtin.Supply.Ml==0,"new player receives an empty inventory airbrush");
        Check(ReferenceEquals(builtin,freshInventory.EnsureAirbrush())&&freshInventory.Items.Count==1,"inventory initialization cannot duplicate airbrush");
        var legacy=new PaintDocument();var legacyTool=new PaintItemState{Sprayer=true,Supply=new(){Color=2,Ml=123,MixedRgb=new double[]{100,120,140}}};
        var legacyBucket=new PaintItemState{Supply=new(){Color=3,Ml=700}};legacy.Items.Add(legacyBucket);legacy.Items.Add(legacyTool);
        Check(ReferenceEquals(legacy.EnsureAirbrush(),legacyTool)&&legacyTool.Inventory&&legacyTool.Stowed,"existing airbrush migrates to inventory");
        Check(legacyTool.Supply.Ml==123&&legacyTool.Supply.MixedRgb![1]==120&&legacyBucket.Supply.Ml==700&&!legacyBucket.Inventory,"migration preserves mixed paint and buckets");
        legacy.Validate();Check(true,"migrated inventory validates without changing native save schema");
        var bucket=new PaintSupply{Color=2,Ml=1000};var tank=new PaintSupply();
        Check(PaintRules.Refill(bucket,tank,5)==250&&bucket.Ml==750&&tank.Color==2,"full refill conserves paint");
        Check(PaintRules.Refill(bucket,tank,5)==0&&bucket.Ml==750,"full tank consumes nothing");
        PaintRules.Consume(tank,60);Check(Math.Abs(tank.Ml-125)<1e-6,"half tank after one minute");
        var mixed=new PaintSupply{Color=2,Ml=100};var yellow=new PaintSupply{Color=3,Ml=1000};
        Check(PaintRules.Refill(yellow,mixed,2)==100&&mixed.Ml==200&&yellow.Ml==900,"different colors mix and conserve volume");
        Check(PaintRules.Rgb(mixed).SequenceEqual(new byte[]{229,129,60}),"equal red and yellow volumes produce weighted orange");
        var prior=PaintRules.Rgb(mixed);PaintRules.Consume(mixed,1);Check(PaintRules.Rgb(mixed).SequenceEqual(prior),"spraying preserves mixture proportions");
        Check(PaintRules.Refill(yellow,mixed,0)==0&&PaintRules.Refill(yellow,mixed,double.NaN)==0&&PaintRules.Refill(yellow,mixed,-1)==0,"invalid refill duration transfers nothing");
        foreach(int hz in new[]{30,60,144})
        {
            var source=new PaintSupply{Color=3,Ml=1000};var dest=new PaintSupply{Color=2,Ml=100};
            for(int i=0;i<hz*2;i++)PaintRules.Refill(source,dest,1d/hz);
            Check(Math.Abs(dest.Ml-200)<1e-7&&Math.Abs(source.Ml-900)<1e-7,"gradual transfer conserves volume at "+hz+" Hz");
            Check(PaintRules.Rgb(dest).SequenceEqual(new byte[]{229,129,60}),"mixture is frame-rate independent at "+hz+" Hz");
        }
        var almost=new PaintSupply{Color=2,Ml=249};var sourcePaint=new PaintSupply{Color=6,Ml=2};
        Check(PaintRules.Refill(sourcePaint,almost,1)==1&&almost.Ml==250&&sourcePaint.Ml==1,"refill clamps at tank capacity");
        var tiny=new PaintSupply{Color=6,Ml=.25};var empty=new PaintSupply();
        Check(PaintRules.Refill(tiny,empty,1)==.25&&tiny.Ml==0&&empty.Ml==.25,"refill clamps at bucket exhaustion");
        Check(PaintRules.Refill(new(){Color=2,Ml=10},empty,.1)==5&&Math.Abs(empty.Ml-5.25)<1e-8,"short hold transfers only elapsed-time paint");
        PaintRules.Consume(empty,100);Check(empty.MixedRgb==null&&empty.Color==-1,"emptying removes old mixture");

        var small=new PaintSupply{Color=2,Ml=30};Check(PaintRules.Refill(small,tank,5)==30&&small.Ml==0&&tank.Ml==155,"partial refill empties only available contents");
        Check(PaintRules.Consume(tank,1000)==155&&tank.Ml==0&&tank.Color==-1,"emptying clamps and clears color");
        Check(PaintRules.Refill(new(){Color=3,Ml=20},tank,5)==20&&tank.Color==3,"empty tank changes color");
        Check(PaintRules.Consume(tank,double.NaN)==0&&tank.Ml==20,"invalid duration consumes nothing");
        Check(PaintRules.Consume(tank,-1)==0,"negative duration consumes nothing");
        foreach(int hz in new[]{30,60,144}){var t=new PaintSupply{Color=0,Ml=250};for(int i=0;i<hz*120;i++)PaintRules.Consume(t,1d/hz);Check(t.Ml<1e-6,"two-minute capacity at "+hz+" Hz");}
        Check(PaintRules.Percent(.001,1000)==1&&PaintRules.Percent(0,1000)==0,"nonempty buckets never display zero percent");
        Check(PaintRules.Percent(2000,1000)==100,"percentage capped");
        Check(Math.Abs(PaintRules.Radius(.1f)-.01f)<1e-6&&Math.Abs(PaintRules.Radius(2)-.15f)<1e-6,"original near and far dot diameters");
        Check(PaintRules.Softness(2)>PaintRules.Softness(.1f),"distance softens spray edge");
        Check(PaintRules.Coverage(1,.5f,1,.1f)==0,"no paint outside footprint");
        Check(Math.Abs(PaintRules.Coverage(.2f,.5f,.1,.02f)-PaintRules.Coverage(.2f,.5f,.1,.2f))<1e-6,"wide spray retains paint intensity");
        Check(PaintRules.Coverage(0,.5f,.1,.15f)>.99f,"spray center becomes opaque within a tenth of a second");
        Check(PaintRules.Radius(.2f)<PaintRules.Radius(.5f)&&PaintRules.Radius(.5f)<PaintRules.Radius(1)&&PaintRules.Radius(1)<PaintRules.Radius(2)&&PaintRules.Radius(2)<PaintRules.Radius(3),"physical spray width grows throughout reachable distances");
        var pixel=new byte[4];PaintRules.Blend(pixel,0,PaintRules.Rgb(2),1);Check(pixel[0]==216&&pixel[3]==255,"opaque red stroke");
        PaintRules.Blend(pixel,0,PaintRules.Rgb(6),1);Check(pixel[0]==52&&pixel[2]==206,"later color covers old paint");
        var blank=new byte[4];PaintRules.Blend(blank,0,PaintRules.Rgb(1),.5f);Check(blank[0]==242&&blank[3]==128,"straight alpha preserves white at edge");
        var vertices=new List<Vector3>();var indices=new List<int>();for(int i=0;i<1000;i++){int n=vertices.Count;vertices.Add(new(i*2,0,0));vertices.Add(new(i*2+1,0,0));vertices.Add(new(i*2,1,0));indices.AddRange(new[]{n,n+1,n+2});}
        var index=new PaintSpatialIndex(vertices.ToArray(),new[]{indices.ToArray()});Check(index.Query(new(20,0,-1),new(21,1,1),0,1).Count()==1,"spatial index isolates one of 1000 triangles");
        Check(!index.Query(new(20,0,-1),new(21,1,1),1,1).Any(),"static batch submesh ownership respected");
        var random=new Random(712);for(int i=0;i<100;i++){int x=random.Next(1000);Check(index.Query(new(x*2,0,-1),new(x*2+1,1,1),0,1).Single().A==x*3,"spatial query "+i);}
        var dir=Path.Combine(Path.GetTempPath(),"prospice-paint-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try
        {
            var path=Path.Combine(dir,"test.paint.gz");var doc=new PaintDocument{World="fixture",NativeHash="checkpoint"};doc.Items.Add(new(){Sprayer=true,Stowed=true,Inventory=true,Supply=new(){Color=4,Ml=123.5}});doc.Tiles.Add(new(){Surface="test/wall",Pixels=new byte[65536]});doc.Tiles[0].Pixels[12]=42;
            PaintArchive.Write(path,doc);var copy=PaintArchive.Read(path);Check(copy.Items[0].Supply.Ml==123.5&&copy.Tiles[0].Pixels[12]==42,"quantities and paint pixels survive compressed round trip");
            doc.Items[0].Supply.MixedRgb=new double[]{72.123,101.456,203.789};PaintArchive.Write(path,doc);var mixtureCopy=PaintArchive.Read(path);Check(mixtureCopy.Items[0].Supply.MixedRgb!.SequenceEqual(doc.Items[0].Supply.MixedRgb),"mixture precision survives compressed save");
            Check(copy.Items[0].Inventory,"built-in inventory ownership survives save round trip");
            Check(copy.Items[0].Stowed,"stowed airbrush survives save round trip");
            doc.Items[0].Supply.Ml=99;PaintArchive.Write(path,doc);Check(PaintArchive.Read(path+".bak").Items[0].Supply.Ml==123.5,"atomic replacement keeps previous-good backup");
            Check(!File.Exists(path+".tmp"),"successful save leaves no temporary file");
            doc.Items[0].Supply.Ml=double.NaN;bool rejected=false;try{PaintArchive.Write(path,doc);}catch(InvalidDataException){rejected=true;}Check(rejected&&PaintArchive.Read(path).Items[0].Supply.Ml==99,"invalid state cannot replace good save");
            File.WriteAllBytes(path,new byte[]{1,2,3,4});rejected=false;try{PaintArchive.Read(path);}catch(InvalidDataException){rejected=true;}Check(rejected,"corrupt compressed save rejected");
        }
        finally{Directory.Delete(dir,true);}
        return count;
    }
}
