using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace Prospice.Placement;
internal static class PaintShop
{
    internal sealed record Product(bool Sprayer,int Color){internal int Price=>Sprayer?0:25;internal string Name=>Sprayer?"Paint sprayer":PaintRules.Names[Color]+" paint / 1 L";}
    static readonly Dictionary<int,Product> products=new();static readonly List<GameObject> added=new();static readonly HashSet<int> menus=new();static int buyingFrame=-1;
    internal static bool IsPaint(ShopButton b)=>b && products.ContainsKey(b.GetInstanceID());
    internal static void Add(ShopMenu menu)
    {
        if(!Plugin.PaintEnabled.Value || !menu || !menu.defaultButton || !menus.Add(menu.GetInstanceID()))return;
        try
        {
            var template=menu.defaultButton;var parent=template.transform.parent;
            // Stage inactive so copied OnEnable cannot run with native inventory semantics.
            var staging=new GameObject("Prospice.Paint.ShopStaging");staging.SetActive(false);
            try
            {
                for(int i=0;i<8;i++)
                {
                    var go=Object.Instantiate(template.gameObject,staging.transform,false);go.name="Prospice.Paint.Shop."+i;go.SetActive(false);
                    var b=go.GetComponent<ShopButton>();var product=new Product(false,i);products[b.GetInstanceID()]=product;
                    b.shopMenu=menu;b.price=product.Price;b.locked=false;b.prerequisite=null;b.cargoPrefabs=new Il2CppSystem.Collections.Generic.List<GameObject>();
                    b.previewPrefab=PaintEquipment.ModelObject(product.Sprayer,product.Color);added.Add(b.previewPrefab);
                    var button=go.GetComponent<Button>();if(button){button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener((UnityEngine.Events.UnityAction)(()=>Buy(menu,b)));var nav=button.navigation;nav.mode=Navigation.Mode.Automatic;button.navigation=nav;}
                    go.transform.SetParent(parent,false);go.SetActive(true);Label(b);added.Add(go);
                }
            }
            finally{Object.Destroy(staging);}
            LayoutRebuilder.MarkLayoutForRebuild(parent.TryCast<RectTransform>());
        }
        catch(Exception e){Plugin.Instance.Log.LogError("Paint shop integration: "+e);}
    }
    internal static bool Label(ShopButton b)
    {
        if(!products.TryGetValue(b.GetInstanceID(),out var p))return false;
        b.locked=false;if(b.itemName)b.itemName.text=p.Name;if(b.itemPrice)b.itemPrice.text=p.Price.ToString();if(b.lockedPrefab)b.lockedPrefab.SetActive(false);
        if(b.icon){ColorUtility.TryParseHtmlString("#"+PaintRules.Hex[p.Color],out var c);b.icon.color=p.Sprayer?Color.gray:c;}return true;
    }
    internal static bool Select(ShopButton b)
    {
        if(!products.TryGetValue(b.GetInstanceID(),out var product))return false;
        var menu=b.shopMenu;menu.currentButton=b;menu.price=product.Price;menu.description.text=product.Sprayer?"Refillable sprayer / 250 ml. Equip with Use. RT / mouse sprays.":"1 liter of "+PaintRules.Names[product.Color].ToLowerInvariant()+" paint. Four sprayer refills.";
        if(b.previewPrefab)menu.cfm(b.previewPrefab);Label(b);return true;
    }
    internal static bool Buy(ShopMenu menu,ShopButton b)
    {
        if(!products.TryGetValue(b.GetInstanceID(),out var product))return false;
        if(buyingFrame==Time.frameCount)return true;buyingFrame=Time.frameCount;
        void Message(string s){if(menu.itemIssuedText)menu.itemIssuedText.text=s;}
        var save=SaveManager.Instance;if(!save||!save.playerSave||!PaintSession.Ready){Message("Paint save unavailable");return true;}
        if(save.playerSave.money<product.Price){Message("Not enough funds");return true;}
        if(PaintEquipment.Items.Count>=2048){Message("Paint item limit reached");return true;}
        var mission=MissionManager.Instance;
        var spawner=mission && mission.currentOutpost?mission.currentOutpost.cargoSpawner:null;
        if(spawner==null||!spawner){Message("Delivery area unavailable");return true;}
        Transform? spot=null;foreach(var candidate in spawner.spawnPoints)if(candidate && !Physics.CheckBox(candidate.position+Vector3.up*.18f,new Vector3(.14f,.16f,.14f),candidate.rotation,spawner.cargoLayerMask,QueryTriggerInteraction.Ignore)){spot=candidate;break;}
        if(spot==null||!spot){Message("Clear the delivery area");return true;}
        PaintEquipment.Item? item=null;int money=save.playerSave.money;
        try
        {
            var state=new PaintItemState{Sprayer=product.Sprayer,Supply=new(){Color=product.Sprayer?-1:product.Color,Ml=product.Sprayer?0:1000},Scene=spot.gameObject.scene.path};
            item=PaintEquipment.Spawn(state,spot.position+Vector3.up*.02f,spot.rotation);
            save.playerSave.money=money-product.Price;PaintSession.Dirty();save.blz("demo");
            if(!PaintSession.CheckpointExists(save))throw new IOException("Paint purchase checkpoint was not written.");
            Message("Issued: "+product.Name);if(menu.buySound)AudioSource.PlayClipAtPoint(menu.buySound,spot.position);
        }
        catch(Exception e)
        {
            save.playerSave.money=money;if(item!=null){PaintEquipment.Items.Remove(item.State.Id);Object.Destroy(item.Object);}try{save.blx();save.blz("demo");}catch(Exception rollback){Plugin.Instance.Log.LogError("Paint refund save failed: "+rollback);}Message("Purchase failed");Plugin.Instance.Log.LogError("Paint purchase: "+e);
        }
        return true;
    }
    internal static void Clear(){foreach(var go in added)if(go)Object.Destroy(go);added.Clear();products.Clear();menus.Clear();buyingFrame=-1;}
}
[HarmonyPatch(typeof(ShopMenu),nameof(ShopMenu.OnEnable))]
internal static class PaintShopOpenPatch{[HarmonyPostfix]static void Postfix(ShopMenu __instance)=>PaintShop.Add(__instance);}
[HarmonyPatch(typeof(ShopButton),nameof(ShopButton.cfk))]
internal static class PaintShopLabelPatch{[HarmonyPrefix]static bool Prefix(ShopButton __instance)=>!PaintShop.Label(__instance);}
[HarmonyPatch(typeof(ShopButton),nameof(ShopButton.ButtonSelected))]
internal static class PaintShopSelectPatch{[HarmonyPrefix]static bool Prefix(ShopButton __instance)=>!PaintShop.Select(__instance);}
[HarmonyPatch(typeof(ShopButton),nameof(ShopButton.ButtonPressed))]
internal static class PaintShopBuyPatch{[HarmonyPrefix]static bool Prefix(ShopButton __instance)=>!PaintShop.Buy(__instance.shopMenu,__instance);}
[HarmonyPatch(typeof(ShopMenu),nameof(ShopMenu.SpawnItemByButton))]
internal static class PaintShopSubmitPatch{[HarmonyPrefix]static bool Prefix(ShopMenu __instance)=>!__instance.currentButton || !PaintShop.Buy(__instance,__instance.currentButton);}
