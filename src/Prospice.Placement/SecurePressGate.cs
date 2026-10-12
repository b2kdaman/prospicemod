namespace Prospice.Placement;

// Identity includes target and physical control so entering a new context with a
// held button cannot secure cargo accidentally or inherit another target's press.
internal sealed class SecurePressGate
{
    (int target, int controller, int button)? identity;
    bool armed;
    internal bool Step(bool eligible, int target, int controller, int button, bool pressed)
    {
        if (!eligible) { Reset(); return false; }
        var next = (target, controller, button);
        if (identity != next) { identity = next; armed = !pressed; return false; }
        if (!pressed) { armed = true; return false; }
        if (!armed) return false;
        armed = false; return true;
    }
    internal void Reset() { identity = null; armed = false; }
    internal static int FirstUnused(IEnumerable<int> candidates, IReadOnlySet<int> occupied) =>
        candidates.FirstOrDefault(id => id >= 0 && !occupied.Contains(id), -1);
}
