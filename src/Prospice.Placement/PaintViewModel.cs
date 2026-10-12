using UnityEngine;
using Object=UnityEngine.Object;
namespace Prospice.Placement;
// The photographic camera's model and anchor identify the native first-person
// layer. Reuse its held-tool parent without enabling camera mode or changing FOV.
internal static class PaintViewModel
{
    static GameObject? model;static readonly List<(Renderer renderer,bool visible)> hidden=new();
    static AudioSource? hiss;static AudioClip? hissClip;
    static Renderer[] indicators=Array.Empty<Renderer>();
    static MaterialPropertyBlock? indicatorProperties;static int lastColor=-1;
    internal static void Feedback(PaintSupply? supply,bool spraying)
    {
        if(!model)return;
        int color=0x303438;
        if(supply!=null&&supply.Ml>0&&PaintRules.ValidColor(supply.Color))
        {var rgb=PaintRules.Rgb(supply);color=(rgb[0]<<16)|(rgb[1]<<8)|rgb[2];}
        if(color!=lastColor&&indicatorProperties!=null)
        {
            lastColor=color;
            indicatorProperties.SetTexture("_BaseMap",Texture2D.whiteTexture);
            indicatorProperties.SetColor("_BaseColor",new Color(((color>>16)&255)/255f,((color>>8)&255)/255f,(color&255)/255f,1));
            foreach(var renderer in indicators)if(renderer)renderer.SetPropertyBlock(indicatorProperties);
        }
        if(!hiss)return;
        if(!model!.activeInHierarchy){hiss!.Stop();hiss.volume=0;return;}
        float target=spraying?.16f:0;
        if(spraying&&!hiss!.isPlaying)hiss.Play();
        hiss!.volume=Mathf.MoveTowards(hiss.volume,target,Time.unscaledDeltaTime*4);
        if(!spraying&&hiss.volume<=0&&hiss.isPlaying)hiss.Stop();
    }
    internal static void Equip(PlayerInteraction p,PaintEquipment.Item item)
    {
        Clear();model=PaintEquipment.ModelObject(true,2);
        // Camera mode disables its own anchor when switching tools. The airbrush
        // must not inherit that inactive hierarchy or its draw/stow animation.
        var camera=p.highlighter.mainCamera;
        var parent=camera.transform;
        model.transform.SetParent(parent,false);
        float depth=Mathf.Max(.4f,camera.nearClipPlane+.2f);
        model.transform.localPosition=new(.16f,-.18f,depth);
        model.transform.localRotation=Quaternion.identity;
        var scale=parent.lossyScale;
        model.transform.localScale=new(1.3f/Mathf.Abs(scale.x),1.3f/Mathf.Abs(scale.y),1.3f/Mathf.Abs(scale.z));
        int layer=p.actions&&p.actions.cameraModel?p.actions.cameraModel.layer:0;
        if((camera.cullingMask&(1<<layer))==0)
            for(int i=0;i<32;i++)if((camera.cullingMask&(1<<i))!=0&&i!=5){layer=i;break;}
        foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;
        foreach(var r in item.Object.GetComponentsInChildren<Renderer>(true)){hidden.Add((r,r.enabled));r.enabled=false;}
        indicators=model.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.GetComponent<MeshFilter>().sharedMesh.name.Contains("Color side panel",StringComparison.Ordinal)).Select(r=>r.Cast<Renderer>()).ToArray();
        indicatorProperties=new MaterialPropertyBlock();lastColor=-1;
        // Original synthesized filtered noise: no external audio dependencies.
        var samples=new float[44100];var random=new System.Random(614);float previous=0;
        for(int i=0;i<samples.Length;i++){float value=(float)(random.NextDouble()*2-1);samples[i]=(value-previous*.8f)*.4f;previous=value;}
        hissClip=AudioClip.Create("Prospice.Paint.Hiss",samples.Length,1,44100,false);
        hissClip.SetData(samples,0);
        hiss=model.AddComponent<AudioSource>();hiss.playOnAwake=false;hiss.loop=true;hiss.spatialBlend=0;hiss.volume=0;hiss.clip=hissClip;
        if(p.actions&&p.actions.mixerGroup)hiss.outputAudioMixerGroup=p.actions.mixerGroup;
        model.SetActive(true);Feedback(item.State.Supply,false);
    }
    internal static void Visible(bool active){if(model)model!.SetActive(active);}
    internal static void Clear(){if(hiss)hiss!.Stop();hiss=null;if(hissClip)Object.Destroy(hissClip);hissClip=null;indicators=Array.Empty<Renderer>();indicatorProperties=null;lastColor=-1;if(model)Object.Destroy(model);model=null;foreach(var (renderer,visible) in hidden)if(renderer)renderer.enabled=visible;hidden.Clear();}
}
