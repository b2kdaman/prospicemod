using TMPro;
using UnityEngine;
namespace Prospice.Placement;

// Reuse ObjectHighlighter's two existing action/input rows and its native layout pass.
// No UI objects, fonts, masks, canvases or independent text overlays are created.
internal static class CarryHint
{
    static ObjectHighlighter? current;
    static Rigidbody? body;
    static readonly List<(TMP_Text label, string original, string written)> edits = new();
    static bool failed;

    internal static void Refresh(ObjectHighlighter highlighter)
    {
        if (failed) return;
        try
        {
            PlacementController.Tick();
            var owner = PlacementController.Owner;
            if (owner == null || !owner || owner.highlighter != highlighter) return;
            if (!PlacementController.ShowNativePrompts) { Dispose(); return; }
            if (highlighter.actions == null || highlighter.inputs == null || highlighter.actions.Length < 2 || highlighter.inputs.Length < 2) return;
            if (current != highlighter || body != owner.heldRigidbody)
            {
                Dispose(); current = highlighter; body = owner.heldRigidbody;
            }
            // The game's layout projects this collider's bounds, just as for its original X prompt.
            // NativePromptPatch restores the reference after Update, including exceptional exits.
            var nativeTarget = highlighter.objectRenderer;
            bool contextual = nativeTarget && nativeTarget.attachedRigidbody != body && !nativeTarget.transform.IsChildOf(body!.transform);
            string action = PlacementController.PromptAction;
            string button = PhysicalLT(owner, !PlacementController.Active);
            if (contextual && !PlacementController.Active)
            {
                // For example, a carried fuel can can have a native use/fill target. Keep its
                // labels and layout. Append LT within the same row instead of erasing an action.
                Write(highlighter.actions[0], NativeText(highlighter.actions[0]) + " / Place");
                Write(highlighter.inputs[0], NativeText(highlighter.inputs[0]) + " / " + PhysicalLT(owner, false));
            }
            else
            {
                highlighter.objectRenderer = body!.GetComponent<Collider>();
                Write(highlighter.actions[0], "Drop");
                Write(highlighter.inputs[0], owner.bvv("Use", false));
                Write(highlighter.actions[1], action);
                Write(highlighter.inputs[1], button);
            }
        }
        catch (Exception error)
        {
            Dispose(); failed = true;
            Plugin.Instance.Log.LogWarning("Native placement prompt unavailable; controls remain available. " + error);
        }
    }
    internal static string PhysicalLT(PlayerInteraction owner, bool hold)
    {
        // Reuse the game's Hold localization and bracket formatting, but keep the physical LT
        // label accurate if Aim was remapped. Use/drop remains the game's mapped action.
        string formatted = owner.bvv("Aim", hold);
        string mapped = RewiredUtils.Instance.crs("Aim");
        return string.IsNullOrEmpty(mapped) ? (hold ? "Hold [ LT ]" : "[ LT ]") : formatted.Replace(mapped, "LT");
    }
    static string NativeText(TMP_Text label)
    {
        foreach (var edit in edits)
            if (edit.label == label && label.text == edit.written) return edit.original;
        return label.text;
    }
    internal static void Write(TMP_Text label, string value)
    {
        if (!label) return;
        for (int i = 0; i < edits.Count; i++)
        {
            if (edits[i].label != label) continue;
            var old = edits[i];
            // A native update/localization change becomes the new restoration value.
            edits[i] = (label, label.text == old.written ? old.original : label.text, value);
            if (label.text != value) label.text = value;
            return;
        }
        edits.Add((label, label.text, value));
        label.text = value;
    }
    internal static void Watchdog()
    {
        if (current && !PlacementController.ShowNativePrompts) Dispose();
    }
    internal static void Dispose()
    {
        foreach (var edit in edits)
            if (edit.label && edit.label.text == edit.written) edit.label.text = edit.original;
        edits.Clear(); current = null; body = null;
    }
}
