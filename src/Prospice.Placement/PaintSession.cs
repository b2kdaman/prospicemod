using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;
namespace Prospice.Placement;
internal static class PaintSession
{
    internal static bool Ready;internal static string Failure="";
    static PaintDocument document=new();static string path="",world="";static float nextRestore;
    static readonly Dictionary<string,PaintItemState> pending=new();
    static readonly Dictionary<string,PaintItemState> lastItems=new();
    static string Hash(string file)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
    static string World(SaveManager manager)=>manager.playerSave.worldseed.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
    static string Archive(string file,string hash)=>System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file)!,"ProspicePaint",System.IO.Path.GetFileNameWithoutExtension(file),hash+".paint.gz");
    internal static void Load(SaveManager manager)
    {
        if(!Plugin.PaintEnabled.Value)return;
        try
        {
            Reset();path=manager.ruk;world=World(manager);
            if(!File.Exists(path))return;
            string hash=Hash(path),archive=Archive(path,hash);
            if(File.Exists(archive))
            {
                try{document=PaintArchive.Read(archive);}catch{document=PaintArchive.Read(archive+".bak");}
                if(document.World!=world||document.NativeHash!=hash)throw new InvalidDataException("Paint sidecar does not match native save/world.");
            }
            else document=new(){World=world,NativeHash=hash};
            document.EnsureAirbrush();
            PaintOverlay.Bind(document.Tiles);foreach(var item in document.Items)pending[item.Id]=item;Ready=true;
        }
        catch(Exception e){Failure="Paint save unavailable";Plugin.Instance.Log.LogError("Paint load: "+e);Ready=false;}
    }
    internal static bool CheckpointExists(SaveManager manager)
    {
        if(!File.Exists(manager.ruk))return false;
        return File.Exists(Archive(manager.ruk,Hash(manager.ruk))) && Failure.Length==0;
    }
    internal static void Dirty(){if(SaveManager.Instance)SaveManager.Instance.blx();}
    internal static void Saved(SaveManager manager)
    {
        if(!Plugin.PaintEnabled.Value)return;
        try
        {
            if(!Ready){Load(manager);return;}
            if(path!=manager.ruk||world!=World(manager)){Load(manager);return;}
            if(!File.Exists(path))return;
            // OnSave is raised only after native File.WriteAllText. Hash binds the
            // sidecar to that exact checkpoint, including the corresponding currency.
            var activeItems=PaintEquipment.Snapshot();
            foreach(var item in activeItems)lastItems[item.Id]=item;
            foreach(var item in lastItems.Values)if(!PaintEquipment.Items.TryGetValue(item.Id,out var live)||!live.Object)pending[item.Id]=item;
            var items=activeItems.Concat(pending.Values).GroupBy(i=>i.Id).Select(g=>g.First()).ToList();
            document=new(){World=world,NativeHash=Hash(path),Items=items,Tiles=PaintOverlay.Snapshot()};
            PaintArchive.Write(Archive(path,document.NativeHash),document);Failure="";
        }
        catch(Exception e){Failure="Paint save failed";Plugin.Instance.Log.LogError("Paint save: "+e);}
    }
    internal static void Tick(PlayerInteraction p)
    {
        if(!Ready && Failure.Length==0 && SaveManager.Instance && SaveManager.Instance.playerSave && !string.IsNullOrEmpty(SaveManager.Instance.ruk))Load(SaveManager.Instance);
        if(!Ready)return;
        PaintSurfaces.Scan();
        if(Time.unscaledTime>=nextRestore)
        {
            nextRestore=Time.unscaledTime+2;PaintSurfaces.Scan();
            foreach(var item in PaintEquipment.Snapshot())lastItems[item.Id]=item;
            foreach(var pair in PaintEquipment.Items.ToArray())
                if(!pair.Value.Object){if(lastItems.TryGetValue(pair.Key,out var saved))pending[pair.Key]=saved;PaintEquipment.Items.Remove(pair.Key);}
            foreach(var pair in pending.ToArray())
            {
                var s=pair.Value;Vector3 position=PaintSurfaces.V(s.Position);var rotation=new Quaternion(s.Rotation[0],s.Rotation[1],s.Rotation[2],s.Rotation[3]);
                if(s.Stowed||s.Inventory){position=p.nlv.transform.position;s.Parent="";}
                else if(!string.IsNullOrEmpty(s.Parent))
                {
                    if(!PaintSurfaces.All.TryGetValue(s.Parent,out var surface)||!surface.Root)continue;
                    position=surface.Root!.TransformPoint(position);rotation=surface.Root.rotation*rotation;
                }
                else if(!Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path==s.Scene))continue;
                PaintEquipment.Spawn(s,position,rotation);pending.Remove(pair.Key);
            }
        }
        PaintOverlay.Stream(p.nlv.transform.position);
    }
    internal static void Reset(){PaintTool.Reset();PaintEquipment.Clear();PaintOverlay.Clear();PaintSurfaces.Clear();pending.Clear();lastItems.Clear();Ready=false;Failure="";path=world="";nextRestore=0;document=new();}
}
[HarmonyPatch(typeof(SaveManager),nameof(SaveManager.bma))]
internal static class PaintLoadPatch { [HarmonyPostfix] static void Postfix(SaveManager __instance){PaintSession.Load(__instance);} }
[HarmonyPatch(typeof(SaveManager),nameof(SaveManager.blz))]
internal static class PaintSavePatch { [HarmonyPostfix] static void Postfix(SaveManager __instance){if(!__instance.markedForSave)PaintSession.Saved(__instance);} }
[HarmonyPatch(typeof(SaveManager),nameof(SaveManager.bmb))]
internal static class PaintResetPatch { [HarmonyPrefix] static void Prefix(){PaintSession.Reset();} }
