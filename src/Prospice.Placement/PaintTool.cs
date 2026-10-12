using TMPro;

using UnityEngine;

namespace Prospice.Placement;

internal static class PaintTool

{

    static readonly ToolCycleGate toolTap=new();

    static PlayerInteraction? owner;static PaintEquipment.Item? equipped,target;static readonly GamepadInput padInput=new();

    static readonly List<(TMP_Text label,string original,string written)> edits=new();

    static int frame=-1,blocked=-1;static bool armed,dumping,refilling;static bool? lastPad;static float discard,accumulator;static string message="";

    static float opacity=1,brushScale=1;
    static string BrushLabel=>$"Alpha {MathF.Round(opacity*100)}% · Size {MathF.Round(brushScale*100)}%";
    internal static void SuppressLook(FPController.FPInputManager manager)
    {
        if(owner!=null&&owner&&manager==owner.inputManager&&Equipped&&Context(owner))
        {
            var pad=padInput.Read(owner.nlx,false);
            if(pad.Connected&&pad.LB)manager.LookInput=Vector2.zero;
        }
    }
    static float nextProbe;
    static Vector3? previous;static string previousSurface="";

    internal static bool Equipped=>equipped!=null;
    internal static bool FixedHud(ObjectHighlighter h)=>owner!=null&&owner&&owner.highlighter==h&&Equipped&&Context(owner)&&PaintSession.Ready&&h.actions!=null&&h.inputs!=null&&h.actions.Length>=2&&h.inputs.Length>=2;

    internal static bool BlockActions=>Equipped||toolTap.Suppress||blocked==Time.frameCount;

    internal static bool Blocks(PlayerInteraction p)=>p==owner&&(Equipped||toolTap.Suppress||blocked==Time.frameCount);

    static bool Context(PlayerInteraction p)=>Plugin.Ready&&Plugin.PaintEnabled.Value&&p&&p.isActiveAndEnabled&&p.inputManager&&p.inputManager.Controlled&&p.inputManager.controlLock<=0&&p.nlv&&p.nlx!=null&&Time.timeScale>0&&Application.isFocused&&

        (!PauseManager.Instance||!PauseManager.Instance.paused)&&(!UIMaster.Instance||!UIMaster.Instance.usingInterface)&&(!p.repairManager||!p.repairManager.activeInHierarchy)&&

        (!PlayerController.Instance||PlayerController.Instance.CurrentControl==PlayerController.ControlType.Player);

    internal static void Tick(PlayerInteraction p)

    {

        if(!Plugin.PaintEnabled.Value||!p||!p.inputManager||!p.inputManager.Controlled){PaintHudLayout.Restore();toolTap.Reset();return;}

        if(owner!=p){Reset();owner=p;}if(frame==Time.frameCount)return;frame=Time.frameCount;

        bool spraying=false;
        try

        {

            if(!Context(p)){PaintHudLayout.Restore();toolTap.Reset();PaintViewModel.Visible(false);armed=false;dumping=false;refilling=false;discard=accumulator=0;previous=null;Restore();return;}

            PaintSession.Tick(p);if(!PaintSession.Ready)return;PaintViewModel.Visible(true);

            var held=PaintEquipment.Find(p.heldRigidbody);if(equipped!=null&&((!equipped.State.Inventory&&held!=equipped)||!equipped.Object)){equipped=null;armed=false;PaintViewModel.Clear();}

            target=Look(p);var pad=padInput.Read(p.nlx,false);

            if(lastPad!=pad.Connected){toolTap.Reset();armed=false;lastPad=pad.Connected;}

            bool cycle=toolTap.Step(pad.Right,pad.Connected&&!PlacementController.Active&&p.actions&&p.actions.controlLock<=0&&

                (!p.heldRigidbody||held?.State.Sprayer==true)&&(!p.actions.playerHeldItem||p.actions.playerHeldItem.currentItem==PlayerHeldItem.ItemType.Nothing));

            if(cycle){Cycle(p,held);return;}

            if(toolTap.Suppress)return;

            bool use=p.nlx.GetButtonDown(31);bool fire=pad.Connected?pad.RT>.55f:Input.GetMouseButton(0);

            if(equipped!=null&&pad.Connected&&pad.LB&&!pad.Cancel&&!Input.GetKeyDown(KeyCode.Escape))
            {
                opacity=PaintRules.AdjustBrush(opacity,pad.RightStick.y,Time.unscaledDeltaTime,.5f,.05f,1);
                brushScale=PaintRules.AdjustBrush(brushScale,pad.RightStick.x,Time.unscaledDeltaTime,.75f,.25f,2);
                blocked=frame;armed=false;dumping=refilling=false;discard=accumulator=0;previous=null;
                message=BrushLabel;PlacementController.Cancel();return;
            }
            var paintHand=equipped??held;
            if(paintHand?.State.Sprayer!=true)refilling=false;
            if(paintHand?.State.Sprayer==true && (refilling || fire&&target!=null&&!target.State.Sprayer))

            {

                blocked=frame;PlacementController.Cancel();armed=false;dumping=false;discard=accumulator=0;previous=null;

                refilling=fire;

                if(fire&&target!=null&&!target.State.Sprayer)

                {

                    double moved=PaintRules.Refill(target.State.Supply,paintHand.State.Supply,Time.deltaTime);

                    message=moved>0?"Drawing paint":paintHand.State.Supply.Ml>=PaintRules.TankMl?"Tank full":"Bucket empty";

                    if(moved>0)PaintSession.Dirty();

                }

                // Looking away stops transfer; release before spraying or throwing.

                return;

            }

            if(equipped!=null)

            {

                blocked=frame;PlacementController.Cancel();

                if(pad.Cancel||Input.GetKeyDown(KeyCode.Escape)){if(equipped.State.Inventory)Stow(p,equipped);equipped=null;armed=false;discard=accumulator=0;previous=null;PaintViewModel.Clear();return;}

                if(use)dumping=target==null;

                if(!p.nlx.GetButton(31)||target!=null||fire)dumping=false;

                if(dumping)

                {discard+=Time.deltaTime;if(discard>=2){equipped.State.Supply.Ml=0;equipped.State.Supply.Color=-1;equipped.State.Supply.MixedRgb=null;PaintSession.Dirty();message="Tank emptied";discard=0;dumping=false;}}

                else discard=0;

                if(!fire)
                {
                    armed=true;accumulator=0;previous=null;
                    if(Time.unscaledTime>=nextProbe){nextProbe=Time.unscaledTime+.1f;PaintSurfaces.Hit(p,out _,out _,out _);}
                    return;
                }

                if(!armed)return;
                spraying=equipped.State.Supply.Ml>0;

                accumulator+=Math.Min(Time.deltaTime,.1f);if(accumulator<1f/30)return;float duration=accumulator;accumulator=0;
                bool hit=PaintSurfaces.Hit(p,out var surface,out var point,out var normal);
                if(!hit&&(PaintMeshCache.Preparing||PaintSurfaces.Preparing)){spraying=false;message="Preparing paint surface…";previous=null;accumulator=0;return;}
                byte[] color=PaintRules.ValidColor(equipped.State.Supply.Color)?PaintRules.Rgb(equipped.State.Supply):new byte[3];double used=PaintRules.Consume(equipped.State.Supply,duration);

                if(used<=0){message="Empty — refill";return;}PaintSession.Dirty();

                float time=(float)(used/PaintRules.MlPerSecond);

                if(hit)

                {

                    float distance=Vector3.Distance(p.nlv.transform.position,surface!.ToWorld(point));float radius=PaintRules.Radius(distance)*brushScale;

                    int steps=previous.HasValue&&previousSurface==surface.Key?Math.Clamp((int)MathF.Ceiling(Vector3.Distance(previous.Value,point)/(radius*.35f)),1,4):1;

                    for(int i=1;i<=steps;i++)PaintOverlay.Dab(surface,steps==1?point:Vector3.Lerp(previous!.Value,point,i/(float)steps),normal,p.nlv.transform.position,radius,color,time*opacity/steps);

                    previous=point;previousSurface=surface.Key;message="Spray";

                }

                else{previous=null;message=(PaintMeshCache.Preparing||PaintSurfaces.Preparing)?"Preparing paint surface…":"No paintable surface";}

                return;

            }

            if(use&&!PlacementController.Active&&held?.State.Sprayer==true)

            {p.actions.bva();PaintViewModel.Equip(p,held);equipped=held;armed=false;blocked=frame;discard=accumulator=0;previous=null;message="Spray";PlacementController.Cancel();return;}

            if(use&&!p.heldRigidbody&&target!=null)

            {

                blocked=frame;p.bvs(target.Body,new Vector3(.2f,-.2f,.45f),Vector3.zero,false,target.Packable);p.nly=.5f;PaintSession.Dirty();

            }

        }

        catch(Exception e){Reset();Plugin.Instance.Log.LogError("Paint interaction stopped: "+e);}
        finally { PaintViewModel.Feedback(equipped?.State.Supply,spraying); }

    }

    static PaintEquipment.Item? Stowed()=>PaintEquipment.Items.Values.FirstOrDefault(i=>i.Object&&i.State.Sprayer&&i.State.Stowed);

    static bool Stow(PlayerInteraction p,PaintEquipment.Item item)

    {

        var stored=Stowed();if(stored!=null&&stored!=item)return false;

        // Release through the verified native path before freeing the hand slot.

        // Native obstruction refusal leaves the current tool owned and usable.

        if(!item.State.Inventory){p.bvu(0);if(p.heldRigidbody)return false;}

        equipped=null;PaintViewModel.Clear();item.State.Stowed=true;item.Object.SetActive(false);

        PaintSession.Dirty();return true;

    }

    static void Cycle(PlayerInteraction p,PaintEquipment.Item? held)

    {

        if(!p.actions||held!=null&&!held.State.Sprayer||p.heldRigidbody&&held==null||

            p.actions.playerHeldItem&&p.actions.playerHeldItem.currentItem!=PlayerHeldItem.ItemType.Nothing)return;

        blocked=frame;armed=false;dumping=false;refilling=false;discard=accumulator=0;previous=null;

        PlacementController.Cancel();

        var available=held??Stowed();

        var current=Equipped?PaintToolSlot.Airbrush:p.actions.currentItem==FPActions.ItemType.Camera?PaintToolSlot.Camera:PaintToolSlot.Empty;

        var next=ToolCycleGate.Next(current,available!=null);

        var outgoing=equipped??held;
        if(next!=PaintToolSlot.Airbrush&&outgoing!=null&&!Stow(p,outgoing))return;

        p.actions.bva();

        if(next==PaintToolSlot.Airbrush&&available!=null)

        {

            if(!available.State.Inventory&&!p.heldRigidbody)

            {

                available.Object.SetActive(true);

                p.bvs(available.Body,new Vector3(.2f,-.2f,.45f),Vector3.zero,false,available.Packable);

                if(p.heldRigidbody!=available.Body){available.Object.SetActive(false);return;}

            }

            available.State.Stowed=false;PaintSession.Dirty();

            PaintViewModel.Equip(p,available);equipped=available;message="Spray";

        }

        else

        {

            equipped=null;PaintViewModel.Clear();

            if(next==PaintToolSlot.Camera)p.actions.bvd();

        }

    }

    static PaintEquipment.Item? Look(PlayerInteraction p)

    {

        RaycastHit? first=null;

        foreach(var hit in Physics.RaycastAll(p.nlv.transform.position,p.nlv.transform.forward,p.interactionDistance,~0,QueryTriggerInteraction.Ignore))

        {

            if(!hit.collider||hit.collider.transform.IsChildOf(p.transform)||(p.characterController&&hit.collider.transform.IsChildOf(p.characterController.transform))||(p.heldRigidbody&&hit.collider.transform.IsChildOf(p.heldRigidbody.transform)))continue;

            if(!first.HasValue||hit.distance<first.Value.distance)first=hit;

        }

        return first.HasValue?PaintEquipment.Find(first.Value.collider):null;

    }

    internal static bool Ignore(Collider c)=>owner!=null&&owner&&(c.transform.IsChildOf(owner.transform)||(owner.characterController&&c.transform.IsChildOf(owner.characterController.transform))||(owner.heldRigidbody&&c.transform.IsChildOf(owner.heldRigidbody.transform)));

    static string TankLabel(PaintSupply supply)=>"Airbrush "+PaintRules.Percent(supply.Ml,PaintRules.TankMl)+"%";

    static string Label(PaintEquipment.Item item)

    {var s=item.State.Supply;return (item.State.Sprayer?"Sprayer":PaintRules.Names[s.Color]+" paint")+" — "+PaintRules.Percent(s.Ml,item.State.Sprayer?250:1000)+"%";}

    internal static void Refresh(ObjectHighlighter h)
    {
        // Restore every row from the previous mode before writing this frame's prompts.
        // Otherwise a carried bucket can retain the last airbrush actions after stowing.
        Restore();
        if(owner==null||!owner||owner.highlighter!=h||!Context(owner)||!PaintSession.Ready)return;
        if(h.actions==null||h.inputs==null||h.actions.Length<2||h.inputs.Length<2)return;
        if(!Equipped)
        {
            // Looking at paint still shows the bucket amount; no airbrush HUD is shown.
            if(target==null||!target.Object||target.State.Sprayer)return;
            h.objectRenderer=target.Object.GetComponent<Collider>();Write(h.text,Label(target));
            Write(h.actions[0],"Pick up");Write(h.inputs[0],owner.bvv("Use",false));
            Write(h.actions[1],"");Write(h.inputs[1],"");return;
        }
        var item=target!=null&&!target.State.Sprayer?target:equipped!;
        if(!item.Object)return;
        h.objectRenderer=item.Object.GetComponent<Collider>();
        Write(h.text,item.State.Sprayer?TankLabel(item.State.Supply):Label(item));
        if(target!=null&&!target.State.Sprayer)
        {
            Write(h.actions[0],refilling?"Filling":"Refill / mix");Write(h.inputs[0],lastPad==true?"[RT]":"[LMB]");
            Write(h.actions[1],TankLabel(equipped!.State.Supply));Write(h.inputs[1],"");return;
        }
        Write(h.actions[0],discard>0?"Emptying":equipped!.State.Supply.Ml<=0?"Refill tank":"Spray");
        Write(h.inputs[0],lastPad==true?"[RT]":"[LMB]");
        Write(h.actions[1],$"A {MathF.Round(opacity*100)}%  S {MathF.Round(brushScale*100)}%");
        Write(h.inputs[1],lastPad==true?"[LB+RS]":"");

    }
    static void Write(TMP_Text label,string value)

    {

        if(!label)return;for(int i=0;i<edits.Count;i++){var e=edits[i];if(e.label!=label)continue;edits[i]=(label,label.text==e.written?e.original:label.text,value);label.text=value;return;}edits.Add((label,label.text,value));label.text=value;

    }

    static void Restore(){foreach(var e in edits)if(e.label&&e.label.text==e.written)e.label.text=e.original;edits.Clear();}

    internal static void Reset(){PaintHudLayout.Restore();toolTap.Reset();Restore();PaintViewModel.Clear();dumping=false;refilling=false;lastPad=null;equipped=target=null;owner=null;frame=blocked=-1;armed=false;discard=accumulator=0;previous=null;previousSurface=message="";padInput.Reset();}

}
