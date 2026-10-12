using TMPro;
using UnityEngine;
namespace Prospice.Placement;
// Temporarily lay out the existing native labels in canvas coordinates. Restore
// before native Update so ordinary target prompts retain their original layout.
internal static class PaintHudLayout
{
    sealed record Saved(TMP_Text Text,Transform Parent,int Sibling,Vector2 Min,Vector2 Max,Vector2 Pivot,Vector2 Size,Vector3 Position,Quaternion Rotation,Vector3 Scale,TextAlignmentOptions Alignment,bool Wrap,bool Active);
    static readonly List<Saved> saved=new();
    static UnityEngine.UI.Image? image;static bool imageEnabled;
    internal static void Restore()
    {
        foreach(var s in saved)
        {
            if(!s.Text||!s.Parent)continue;
            var t=s.Text.rectTransform;t.SetParent(s.Parent,false);t.SetSiblingIndex(s.Sibling);
            t.anchorMin=s.Min;t.anchorMax=s.Max;t.pivot=s.Pivot;t.sizeDelta=s.Size;t.localPosition=s.Position;t.localRotation=s.Rotation;t.localScale=s.Scale;
            s.Text.alignment=s.Alignment;s.Text.enableWordWrapping=s.Wrap;s.Text.gameObject.SetActive(s.Active);
        }
        saved.Clear();if(image)image!.enabled=imageEnabled;image=null;
    }
    internal static void Apply(ObjectHighlighter h)
    {
        if(!PaintTool.FixedHud(h)||!h.canvas)return;
        var canvas=h.canvas.transform.TryCast<RectTransform>();if(!canvas)return;
        void Place(TMP_Text text,float x,float y,float width)
        {
            if(!text)return;var t=text.rectTransform;
            saved.Add(new(text,t.parent,t.GetSiblingIndex(),t.anchorMin,t.anchorMax,t.pivot,t.sizeDelta,t.localPosition,t.localRotation,t.localScale,text.alignment,text.enableWordWrapping,text.gameObject.activeSelf));
            t.SetParent(canvas,false);t.anchorMin=t.anchorMax=new(x,y);t.pivot=new(0,1);
            t.anchoredPosition=Vector2.zero;t.localRotation=Quaternion.identity;t.localScale=Vector3.one;t.sizeDelta=new(canvas!.rect.width*width,canvas.rect.height*.04f);
            text.alignment=TextAlignmentOptions.TopLeft;text.enableWordWrapping=false;text.gameObject.SetActive(true);
        }
        Place(h.text,.64f,.25f,.32f);
        for(int i=0;i<2;i++){Place(h.inputs[i],.64f,.205f-i*.045f,.08f);Place(h.actions[i],.73f,.205f-i*.045f,.24f);}
        image=h.highlightImage;if(image){imageEnabled=image!.enabled;image.enabled=false;}
    }
}
