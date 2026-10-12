using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Security.Cryptography;
using UnityEngine;
namespace Prospice.Placement;

[BepInPlugin(Id, "Prospice Gamepad Placement", "0.5.0")]
public sealed class Plugin : BasePlugin
{
    public const string Id = "prospice.gamepadplacement";
    internal static Plugin Instance = null!;
    internal static ConfigEntry<bool> Enabled = null!, SelfTest = null!, StorageGrid = null!, CargoBelts = null!, PaintEnabled = null!;
    internal static ConfigEntry<float> MoveSpeed = null!, DeadZone = null!, Enter = null!, Exit = null!, PaintReach = null!;
    Harmony? harmony;
    PlacementDriver? driver;
    internal static bool Ready { get; private set; }
    public override void Load()
    {
        Instance = this;
        Enabled = Config.Bind("Placement", "Enabled", true, "Enable gamepad placement for ordinary carried items.");
        PaintEnabled = Config.Bind("Paint", "Enabled", true, "Enable purchasable paint equipment and persistent graffiti.");
        PaintReach = Config.Bind("Paint", "SprayReach", 3f, new ConfigDescription("Maximum spray range in metres.", new AcceptableValueRange<float>(.2f, 3f)));
        CargoBelts = Config.Bind("Cargo", "EnableFlatBelts", true, "Replace cargo packing with supported stacks and gravity-projected flat belts. Disable to use native packing on the next vehicle entry.");
        StorageGrid = Config.Bind("Placement", "ShowTruckStorageGrid", true, "Show a subtle world-space glow grid on exposed truck storage while placing.");
        MoveSpeed = Config.Bind("Placement", "MoveMetersPerSecond", .5f, new ConfigDescription("RT + left stick positioning speed.", new AcceptableValueRange<float>(.05f, 2f)));
        DeadZone = Config.Bind("Placement", "StickDeadZone", .18f, new ConfigDescription("Left stick neutral zone for both positioning and quarter-turn rearming.", new AcceptableValueRange<float>(0, .8f)));
        Enter = Config.Bind("Placement", "TriggerEnter", .55f, new ConfigDescription("Fresh LT press threshold.", new AcceptableValueRange<float>(.4f, 1)));
        Exit = Config.Bind("Placement", "TriggerRelease", .35f, new ConfigDescription("LT release / neutral threshold.", new AcceptableValueRange<float>(.01f, .39f)));
        SelfTest = Config.Bind("Diagnostics", "RunSelfTests", false, "Developer diagnostics; keep false for normal play. Requires a verified disposable user profile: the surrounding game can accept menu input and reset/write saves.");
        using var stream = File.OpenRead(Path.Combine(Paths.GameRootPath, "GameAssembly.dll"));
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(stream));
        if (hash != "511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D")
        {
            Log.LogError("Unsupported game build: " + hash + ". Placement hooks NOT installed. Regenerate and verify bindings first.");
            return;
        }
        harmony = new Harmony(Id);
        try
        {
            // PatchAll can fail after installing some hooks. Do not enable placement until
            // both the complete hook set and its watchdog have been created.
            harmony.PatchAll(typeof(Plugin).Assembly);
            driver = AddComponent<PlacementDriver>();
            Ready = true;
        }
        catch
        {
            Stop();
            throw;
        }
        Log.LogInfo("Placement 0.5.0 loaded; verified game hash; native bvu(float) release binding. Waiting for eligible on-foot item and fresh gamepad LT.");
    }
    public override bool Unload()
    {
        return Stop();
    }
    bool Stop()
    {
        Ready = false;
        var oldDriver = driver;
        driver = null;
        var errors = Cleanup.Run(
            () => { if (oldDriver) oldDriver!.enabled = false; },
            PaintSession.Reset,
            PaintShop.Clear,
            PaintEquipment.Dispose,
            CargoSecureInteraction.Shutdown,
            CargoPacking.Shutdown,
            PlacementController.Shutdown,
            CarryHint.Dispose,
            RuntimeChecks.Cancel,
            () => { harmony?.UnpatchSelf(); harmony = null; },
            () => { if (oldDriver) UnityEngine.Object.Destroy(oldDriver); });
        foreach (var error in errors) Log.LogError("Plugin shutdown failed: " + error);
        return errors.Count == 0;
    }
}

public sealed class PlacementDriver : MonoBehaviour
{
    bool tested;
    public PlacementDriver(IntPtr pointer) : base(pointer) { }
    public void Awake()
    {
        // Only isolated diagnostics should run while their validation window is hidden.
        if (Plugin.SelfTest.Value) Application.runInBackground = true;
    }
    public void Update()
    {
        if (!Plugin.Ready) return;
        CargoSecureInteraction.Update();
        CargoPacking.Update();
        PlacementController.Watchdog();
        RuntimeChecks.Advance();
        if (!tested && Rewired.ReInput.isReady && Time.realtimeSinceStartup > 20)
        {
            tested = true;
            if (Plugin.SelfTest.Value) RuntimeChecks.Run();
        }
    }
    public void LateUpdate() => CarryHint.Watchdog();
    public void OnDisable() { PaintSession.Reset(); PaintShop.Clear(); PaintEquipment.Dispose(); CargoSecureInteraction.Shutdown(); CargoPacking.Shutdown(); PlacementController.Cancel(); CarryHint.Dispose(); RuntimeChecks.Cancel(); }
    public void OnApplicationFocus(bool focus) { if (!focus) PlacementController.Cancel(); }

}

[HarmonyPatch(typeof(PlayerInteraction), nameof(PlayerInteraction.Update))]
internal static class InteractionPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    static bool Prefix(PlayerInteraction __instance)
    {
        PlacementController.Observe(__instance);
        PaintTool.Tick(__instance);
        if (PaintTool.Blocks(__instance)) return false;
        PlacementController.Tick();
        CargoSecureInteraction.Tick(__instance);
        return !PlacementController.BlockInteraction(__instance);
    }
}
[HarmonyPatch(typeof(PlayerInteraction), nameof(PlayerInteraction.OnDisable))]
internal static class InteractionDisablePatch
{
    [HarmonyPrefix] static void Prefix(PlayerInteraction __instance) { if (__instance == PlacementController.Owner) PlacementController.Cancel(); }
}
[HarmonyPatch(typeof(FPActions), nameof(FPActions.Update))]
internal static class ActionsPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    static bool Prefix(FPActions __instance)
    {
        if (__instance.playerInteraction) { PlacementController.Observe(__instance.playerInteraction); PaintTool.Tick(__instance.playerInteraction); }
        PlacementController.Tick();
        return !PaintTool.BlockActions && !PlacementController.BlockActions(__instance);
    }
}
[HarmonyPatch(typeof(FPController.FPInputManager), nameof(FPController.FPInputManager.Update))]
internal static class InputPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    static void Prefix(FPController.FPInputManager __instance, out Il2CppSystem.Action? __state)
    {
        __state = null;
        PlacementController.Tick();
        if (PlacementController.BlockInput(__instance)) __state = MuteJump(__instance);
        // Keep native look sampling, sensitivity, inversion, smoothing and camera sway running.
    }
    internal static Il2CppSystem.Action? MuteJump(FPController.FPInputManager manager)
    {
        var handler = manager.OnJumpPressed;
        manager.OnJumpPressed = null;
        return handler;
    }
    internal static void RestoreJump(FPController.FPInputManager manager, Il2CppSystem.Action? handler)
    {
        if (handler != null) manager.OnJumpPressed = handler;
    }
    [HarmonyFinalizer]
    static void Finalizer(FPController.FPInputManager __instance, Il2CppSystem.Action? __state)
    {
        if (!__instance) return;
        try { RestoreJump(__instance, __state); }
        finally { PlacementController.SuppressInput(__instance); PaintTool.SuppressLook(__instance); }
    }
}

[HarmonyPatch(typeof(FPController.FPController), nameof(FPController.FPController.dai))]
internal static class WalkingPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)] static void Prefix(FPController.FPController __instance)
    {
        PlacementController.Tick();
        if (!__instance.ocq || !PlacementController.BlockInput(__instance.ocq)) return;
        PlacementController.SuppressInput(__instance.ocq);
        PlacementController.StopWalking(__instance);
    }
}
[HarmonyPatch(typeof(ObjectHighlighter), nameof(ObjectHighlighter.Update))]
internal static class NativePromptPatch
{
    [HarmonyPrefix] static void Prefix(ObjectHighlighter __instance, out Collider? __state)
    {
        PaintHudLayout.Restore();
        __state = __instance.objectRenderer;
        CarryHint.Refresh(__instance);
        CargoSecureInteraction.Refresh(__instance);
        PaintTool.Refresh(__instance);
    }
    [HarmonyFinalizer] static void Finalizer(ObjectHighlighter __instance, Collider? __state)
    {
        // The ordinary interaction system owns this reference outside the native layout pass.
        if (__instance) { __instance.objectRenderer = __state; PaintHudLayout.Apply(__instance); }
    }
}
