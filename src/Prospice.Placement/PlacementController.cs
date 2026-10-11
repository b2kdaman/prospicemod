using UnityEngine;
using UnityEngine.SceneManagement;
namespace Prospice.Placement;

internal static class PlacementController
{
    internal static PlayerInteraction? Owner;
    static readonly PlacementGate gate = new();
    static readonly GamepadInput input = new();
    static Preview? preview;
    static TruckStorageGrid? storageGrid;
    static readonly QuarterTurnInput turn = new();
    static readonly GridOrientation orientation = new();
    static Rigidbody? item, observedItem;
    static int frame = -1, suppressFrame = -1, dropFrame = -1, scene, activeScene;
    static Vector3 moveAxis, positionOffset;
    static bool translating;
    static PlacementTarget target;
    static bool faulted, eligible, savedAiming, hintEligible;
    static FPActions? previewActions;
    public static bool Active => preview != null;
    internal static string PromptAction => !Active ? "Place manually" : target.Valid ? "Release to place" : "Cannot place";
    internal static bool ShowNativePrompts => hintEligible && frame == Time.frameCount && Context() &&
        Owner!.heldRigidbody && Owner.heldRigidbody == observedItem &&
        (!Owner.actions || !Owner.actions.inCameraZoom) &&
        (!UIMaster.Instance || !UIMaster.Instance.usingInterface);

    internal static void Observe(PlayerInteraction owner)
    {
        if (!owner || !owner.isActiveAndEnabled || !owner.inputManager || !owner.inputManager.Controlled) return;
        if (Owner != owner) { Cancel(); Owner = owner; frame = -1; observedItem = null; }
    }
    static bool Context() => Plugin.Ready && Plugin.Enabled.Value && !faulted && Owner != null && Owner && Owner.isActiveAndEnabled && Owner.inputManager &&
        Owner.inputManager.Controlled && Owner.inputManager.controlLock <= 0 && Time.timeScale > 0 && Application.isFocused &&
        (!UIMaster.Instance || !UIMaster.Instance.usingInterface) &&
        (!PauseManager.Instance || !PauseManager.Instance.paused) && (!Owner.repairManager || !Owner.repairManager.activeInHierarchy) &&
        (!PlayerController.Instance || PlayerController.Instance.CurrentControl == PlayerController.ControlType.Player);
    static bool Eligible(Rigidbody body)
    {
        if (!body || !body.GetComponent<Collider>() || (!body.GetComponent<Pickupable>() && !body.GetComponent<Packable>())) return false;
        // Special deployment ownership lives in PlayerHeldItem, not this ordinary rigidbody path.
        foreach (var component in body.GetComponentsInChildren<MonoBehaviour>(true))
        {
            var name = component.GetIl2CppType().Name;
            if (name.Contains("Anchor", StringComparison.OrdinalIgnoreCase) || name.Contains("Winch", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Whinch", StringComparison.OrdinalIgnoreCase) || name.Contains("Deploy", StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }
    internal static void Tick()
    {
        if (!Plugin.Ready || Owner == null || !Owner || frame == Time.frameCount) return;
        frame = Time.frameCount;
        hintEligible = false;
        try
        {
            if (!Context()) { Cancel(); return; }
            if (observedItem != Owner.heldRigidbody)
            {
                Cancel(); observedItem = Owner.heldRigidbody; eligible = Eligible(observedItem);
            }
            var pad = Owner.nlx == null ? default : input.Read(Owner.nlx, Active);
            bool context = Context() && pad.Connected && Owner.nlv && eligible;
            hintEligible = context && Owner.nlx!.controllers.GetLastActiveController(Rewired.ControllerType.Joystick) != null;
            bool drop = Owner.nlx != null && Owner.nlx.GetButtonDown(31);
            gate.Enter = Plugin.Enter.Value; gate.Exit = Plugin.Exit.Value;
            var decision = gate.Step(context, pad.LT, pad.Cancel || pad.Menu, drop);
            if (decision == Decision.Drop)
            {
                Cancel(); suppressFrame = frame; dropFrame = frame;
                Plugin.Instance.Log.LogInfo("Preview cancelled: mapped Use/drop passed to vanilla interaction.");
                return;
            }
            if (decision == Decision.Cancel) { Cancel(); suppressFrame = frame; if (drop) dropFrame = frame; return; }
            if (decision == Decision.Begin)
            {
                item = Owner.heldRigidbody;
                preview = new Preview(item, Owner.nlw);
                storageGrid = new TruckStorageGrid(); turn.Reset();
                orientation.Reset();
                positionOffset = Vector3.zero; translating = false;
                moveAxis = Vector3.ProjectOnPlane(Owner.nlv.transform.right, Vector3.up).normalized;
                scene = Owner.gameObject.scene.handle;
                activeScene = SceneManager.GetActiveScene().handle;
                previewActions = Owner.actions;
                if (previewActions) { savedAiming = previewActions.aiming; previewActions.aiming = false; }
                Plugin.Instance.Log.LogInfo($"Preview begin: {item.name}; reach={Owner.interactionDistance}; dropMask={Owner.dropMask.value}; originalScale={Owner.nlw}");
            }
            if (preview == null) return;
            if (item != Owner.heldRigidbody || scene != Owner.gameObject.scene.handle || activeScene != SceneManager.GetActiveScene().handle) { Cancel(); return; }
            float x = PlacementGate.DeadZone(pad.Stick.x, Plugin.DeadZone.Value);
            float y = PlacementGate.DeadZone(pad.Stick.y, Plugin.DeadZone.Value);
            translating = pad.RT >= (translating ? Plugin.Exit.Value : Plugin.Enter.Value);

            if (translating)
                positionOffset = Vector3.ClampMagnitude(positionOffset + (moveAxis * x + Vector3.up * y) * Plugin.MoveSpeed.Value * Time.deltaTime, Owner.interactionDistance);
            else
            {
                var step = turn.Step(pad.Stick.x, pad.Stick.y, Plugin.DeadZone.Value);
                orientation.Turn(step.x, step.y);
            }
            if (translating) turn.Reset();
            // Re-solve on the actual release frame too: moving supports and new blockers cannot use a stale valid pose.
            target = GridPlacement.SolveAligned(preview, item!, Owner.characterController.transform, Owner.nlv,
                Owner.interactionDistance, Owner.dropMask.value, orientation, positionOffset, storageGrid!.Beds);
            preview.Colorize(target.Valid);
            storageGrid!.Update(preview, item!, Owner.characterController.transform, Owner.nlv, Owner.interactionDistance);
            if (decision == Decision.Release)
            {
                suppressFrame = frame;
                if (target.Valid && GameIntegration.Commit(Owner, item!, target))
                    Plugin.Instance.Log.LogInfo("Placement committed through native ordinary-release cleanup.");
                else Plugin.Instance.Log.LogInfo("Release kept item held: " + (target.Valid ? "native drop check declined" : target.Reason));
                Cancel();
            }
        }
        catch (UnsupportedItemException error)
        {
            Cancel(); eligible = false; suppressFrame = frame;
            Plugin.Instance.Log.LogWarning("This item cannot be previewed: " + error.Message);
        }
        catch (Exception error)
        {
            Cancel(); faulted = true;
            Plugin.Instance.Log.LogError("Placement disabled for this session after error; vanilla control restored. " + error);
        }
    }
    internal static void Watchdog()
    {
        if (!Active) return;
        try
        {
            if (!Context() || !item || item != Owner!.heldRigidbody || scene != Owner.gameObject.scene.handle || activeScene != SceneManager.GetActiveScene().handle) Cancel();
        }
        catch (Exception e) { Cancel(); Plugin.Instance.Log.LogError(e); }
    }
    internal static bool BlockInteraction(PlayerInteraction p) => p == Owner && (Active || (suppressFrame == Time.frameCount && dropFrame != Time.frameCount));
    internal static bool BlockActions(FPActions p) => Owner != null && Owner && p == Owner.actions && (Active || suppressFrame == Time.frameCount);
    internal static bool BlockInput(FPController.FPInputManager manager) => Owner != null && Owner &&
        manager == Owner.inputManager && (Active || suppressFrame == Time.frameCount);
    internal static void SuppressInput(FPController.FPInputManager manager)
    {
        if (!BlockInput(manager)) return;
        ClearInput(manager);
    }
    internal static void ClearInput(FPController.FPInputManager manager)
    {
        manager.MovementInput = Vector2.zero;
        manager.SprintInput = manager.JumpInput = manager.JumpInputHeld = false;
    }
    internal static void StopWalking(FPController.FPController controller)
    {
        // Verified dai uses ocp as velocity, ocs as desired velocity and oct for air momentum.
        // Retain vertical gravity/fall motion, but remove horizontal inertia immediately.
        controller.ocp = new Vector3(0, controller.ocp.y, 0);
        controller.ocs = new Vector3(0, controller.ocs.y, 0);
        controller.oct = new Vector3(0, controller.oct.y, 0);
    }
    internal static void Cancel()
    {
        gate.Reset();
        hintEligible = false;
        var oldGrid = storageGrid;
        var oldPreview = preview;
        var oldActions = previewActions;
        bool oldAiming = savedAiming;
        storageGrid = null; preview = null; item = null; previewActions = null;
        turn.Reset(); input.Reset();
        foreach (var error in Cleanup.Run(
            () => oldGrid?.Dispose(),
            () => oldPreview?.Dispose(),
            () => { if (oldActions) oldActions!.aiming = oldAiming; }))
            Plugin.Instance.Log.LogError("Placement cleanup failed: " + error);
    }
    internal static void Shutdown()
    {
        Cancel();
        Owner = null; observedItem = null;
        frame = suppressFrame = dropFrame = -1;
        faulted = eligible = false;
    }
}
