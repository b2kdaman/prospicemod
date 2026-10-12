using Rewired;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Prospice.Placement;

internal static class CargoSecureInteraction
{
    readonly record struct Control(int Controller, int Element, string Label, bool Pressed);
    static readonly SecurePressGate gate = new();
    static readonly List<(TMP_Text label, string original, string written)> edits = new();
    static PlayerInteraction? owner;
    static TruckBed? target, pending;
    static Collider? targetCollider;
    static TruckBed[] beds = Array.Empty<TruckBed>();
    static int frame = -1, pendingFrame, feedbackTarget;
    static float nextScan, feedbackUntil;
    static string button = "", feedback = "";
    static bool failed;

    static bool Context(PlayerInteraction p) => Plugin.Ready && Plugin.CargoBelts.Value && p && p.isActiveAndEnabled &&
        p.inputManager && p.inputManager.Controlled && p.inputManager.controlLock <= 0 && p.nlv && p.nlx != null &&
        !p.heldRigidbody && !PlacementController.Active && (!p.playerHeldItem || p.playerHeldItem.currentItem == PlayerHeldItem.ItemType.Nothing) &&
        (!p.actions || (p.actions.currentItem == FPActions.ItemType.Nothing && !p.actions.inCameraZoom)) &&
        (!p.repairManager || !p.repairManager.activeInHierarchy) && Time.timeScale > 0 && Application.isFocused &&
        (!PauseManager.Instance || !PauseManager.Instance.paused) && (!UIMaster.Instance || !UIMaster.Instance.usingInterface) &&
        (!PlayerController.Instance || PlayerController.Instance.CurrentControl == PlayerController.ControlType.Player);

    internal static void Tick(PlayerInteraction p)
    {
        if (failed || !p || !p.isActiveAndEnabled || !p.inputManager || !p.inputManager.Controlled) return;
        if (owner != p) { Hide(); gate.Reset(); owner = p; frame = -1; }
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (!Context(p)) { Hide(); gate.Reset(); return; }
            var control = UnusedControl(p.nlx);
            if (control == null || !FindDeck(p, out var bed, out var collider)) { Hide(); gate.Reset(); return; }
            if (target != bed) RestoreText();
            target = bed; targetCollider = collider; button = control.Value.Label;
            var pad = control.Value;
            if (!gate.Step(pending == null, bed!.GetInstanceID(), pad.Controller, pad.Element, pad.Pressed)) return;
            // Refresh existing packing as well as newly placed cargo. Native destruction
            // is deferred until end-of-frame, so rebuild on the next frame, not immediately.
            pending = bed; pendingFrame = Time.frameCount + 1;
            feedbackTarget = bed.GetInstanceID(); feedback = "Securing cargo"; feedbackUntil = Time.unscaledTime + 2;
            foreach (var storage in CargoPacking.Related(bed).GroupBy(b => b.GetInstanceID()).Select(g => g.First())) storage.cxj();
        }
        catch (Exception error)
        {
            Hide(); gate.Reset(); pending = null; failed = true;
            Plugin.Instance.Log.LogWarning("Manual cargo secure disabled for this session: " + error);
        }
    }
    internal static void Update()
    {
        if (pending != null && Time.frameCount >= pendingFrame)
        {
            var bed = pending; pending = null;
            if (Plugin.Ready && Plugin.CargoBelts.Value && bed && bed.isActiveAndEnabled && bed.bedrb)
            {
                try
                {
                    // Re-enter the same native entry point used by vehicle entry. The
                    // Phase 2 prefix runs its complete support/route calculation again.
                    bed.cxi();
                    int count = bed.obb?.Count ?? 0;
                    feedbackTarget = bed.GetInstanceID(); feedback = count > 0 ? $"Secured: {count}" : "No cargo secured";
                    feedbackUntil = Time.unscaledTime + 1.5f;
                    Plugin.Instance.Log.LogInfo($"Manual secure: {count} loads on {bed.name}.");
                }
                catch (Exception error) { Plugin.Instance.Log.LogWarning("Manual secure failed: " + error); feedback = "Cannot secure"; feedbackUntil = Time.unscaledTime + 1.5f; }
            }
        }
        if (owner == null || !owner || !Context(owner)) { Hide(); gate.Reset(); }
    }
    static bool FindDeck(PlayerInteraction p, out TruckBed? bed, out Collider? collider)
    {
        bed = null; collider = null;
        if (Time.unscaledTime >= nextScan || beds.Length == 0)
        { beds = Object.FindObjectsOfType<TruckBed>().ToArray(); nextScan = Time.unscaledTime + 1; }
        RaycastHit? first = null;
        foreach (var hit in Physics.RaycastAll(p.nlv.transform.position, p.nlv.transform.forward, p.interactionDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider || hit.collider.transform.IsChildOf(p.transform) ||
                (p.characterController && hit.collider.transform.IsChildOf(p.characterController.transform))) continue;
            if (!first.HasValue || hit.distance < first.Value.distance) first = hit;
        }
        if (!first.HasValue || first.Value.collider.GetComponentInParent<Packable>()) return false;
        var surface = first.Value;
        foreach (var candidate in beds)
        {
            if (!candidate || !candidate.isActiveAndEnabled || !candidate.bed || !candidate.bedrb) continue;
            var marker = candidate.GetComponent<BoxCollider>();
            if (!marker || !marker.enabled || !marker.isTrigger || Vector3.Dot(surface.normal, marker.transform.up) < .65f) continue;
            if (surface.collider.attachedRigidbody != candidate.bedrb && !surface.collider.transform.IsChildOf(candidate.bed)) continue;
            var local = marker.transform.InverseTransformPoint(surface.point) - marker.center;
            // The authored physical floor is slightly below the trigger's bottom.
            var scale = marker.transform.lossyScale;
            float allowance = .08f / Mathf.Max(.001f, Mathf.Abs(scale.y));
            if (Mathf.Abs(local.x) > marker.size.x * .5f || Mathf.Abs(local.z) > marker.size.z * .5f ||
                local.y < -marker.size.y * .5f - allowance || local.y > marker.size.y * .5f) continue;
            bed = candidate; collider = surface.collider; return true;
        }
        return false;
    }
    static Control? UnusedControl(Player player)
    {
        if (!ReInput.isReady || player.controllers.joystickCount == 0) return null;
        var last = player.controllers.GetLastActiveController(ControllerType.Joystick);
        if (last == null) return null;
        double time = last.GetLastTimeActive();
        var keyboard = player.controllers.GetLastActiveController(ControllerType.Keyboard);
        var mouse = player.controllers.GetLastActiveController(ControllerType.Mouse);
        if ((keyboard != null && keyboard.GetLastTimeActive() > time) || (mouse != null && mouse.GetLastTimeActive() > time)) return null;
        for (int joystickIndex = 0; joystickIndex < player.controllers.joystickCount; joystickIndex++)
        {
            var joystick = player.controllers.Joysticks[joystickIndex];
            if (joystick.id != last.id) continue;
            var pad = joystick.GetTemplate(ez.typeGuid)?.TryCast<et>();
            if (pad == null || pad.start.value || pad.back.value) return null;
            var occupied = new HashSet<int>();
            void Reserve(Player mappedPlayer)
            {
                var maps = mappedPlayer.controllers.maps.GetMaps(ControllerType.Joystick, joystick.id);
                if (maps == null) return;
                for (int i = 0; i < maps.Cast<Il2CppSystem.Collections.Generic.ICollection<ControllerMap>>().Count; i++)
                {
                    var map = maps[i]; if (!map.enabled) continue;
                    var elements = map.AllMaps; if (elements == null) continue;
                    for (int j = 0; j < elements.Cast<Il2CppSystem.Collections.Generic.ICollection<ActionElementMap>>().Count; j++)
                    {
                        var element = elements[j];
                        if (element.enabled) occupied.Add(element.elementIdentifierId);
                    }
                }
            }
            Reserve(player);
            if (ReInput.players.SystemPlayer != null && ReInput.players.SystemPlayer.id != player.id) Reserve(ReInput.players.SystemPlayer);
            // Prefer right-stick click, then other physical buttons. Never reserve or
            // rewrite a player's mappings, and never share an enabled native action.
            var options = new (IControllerTemplateButton control, string label)[] {
                (pad.rightStick.press, "R3"), (pad.rightBumper, "RB"), (pad.leftBumper, "LB"), (pad.y, "Y"),
                (pad.dPad.left, "D-pad Left"), (pad.dPad.right, "D-pad Right"), (pad.dPad.up, "D-pad Up"), (pad.dPad.down, "D-pad Down") };
            var available = new List<Control>();
            foreach (var (control, label) in options)
            {
                var source = control?.source?.target;
                if (source == null || source.elementType != ControllerElementType.Button) continue;
                available.Add(new(joystick.id, source.elementIdentifierId, label, control!.value));
            }
            int chosen = SecurePressGate.FirstUnused(available.Select(c => c.Element), occupied);
            if (chosen >= 0) return available.First(c => c.Element == chosen);
        }
        return null;
    }
    internal static void Refresh(ObjectHighlighter highlighter)
    {
        if (owner == null || !owner || owner.highlighter != highlighter) return;
        Tick(owner);
        if (target == null || !target || !targetCollider || !Context(owner)) { RestoreText(); return; }
        if (highlighter.actions == null || highlighter.inputs == null || highlighter.actions.Length < 2 || highlighter.inputs.Length < 2) return;
        // NativePromptPatch restores objectRenderer after the native layout pass.
        highlighter.objectRenderer = targetCollider;
        Write(highlighter.text, "Cargo deck");
        Write(highlighter.actions[0], target.GetInstanceID() == feedbackTarget && Time.unscaledTime < feedbackUntil ? feedback : "Secure cargo");
        Write(highlighter.inputs[0], pending != null ? "" : "[ " + button + " ]");
        Write(highlighter.actions[1], ""); Write(highlighter.inputs[1], "");
    }
    static void Write(TMP_Text label, string value)
    {
        if (!label) return;
        for (int i = 0; i < edits.Count; i++)
        {
            var edit = edits[i]; if (edit.label != label) continue;
            edits[i] = (label, label.text == edit.written ? edit.original : label.text, value);
            if (label.text != value) label.text = value; return;
        }
        edits.Add((label, label.text, value)); label.text = value;
    }
    static void RestoreText()
    {
        foreach (var edit in edits) if (edit.label && edit.label.text == edit.written) edit.label.text = edit.original;
        edits.Clear();
    }
    static void Hide() { RestoreText(); target = null; targetCollider = null; button = ""; }
    internal static void Shutdown()
    {
        Hide(); gate.Reset(); owner = null; pending = null; beds = Array.Empty<TruckBed>();
        nextScan = feedbackUntil = 0; frame = -1; failed = false;
    }
}

