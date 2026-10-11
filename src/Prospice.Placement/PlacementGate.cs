namespace Prospice.Placement;

public enum Decision { None, Begin, Cancel, Drop, Release }

// Unity-independent input arbitration, shared verbatim with the executable tests.
public sealed class PlacementGate
{
    public bool Active { get; private set; }
    bool armed;
    public float Enter = .55f;
    public float Exit = .35f;

    public Decision Step(bool context, float lt, bool cancel, bool drop)
    {
        if (!context) { var was = Active; Reset(); return was ? Decision.Cancel : Decision.None; }
        // The mapped normal drop wins over B and LT release, including on the entry frame.
        if (drop) { var was = Active; Reset(); return was ? Decision.Drop : Decision.None; }
        if (Active)
        {
            if (cancel) { Reset(); return Decision.Cancel; }
            if (lt < Exit) { Reset(); return Decision.Release; }
            return Decision.None;
        }
        if (cancel) { armed = false; return Decision.None; }
        if (lt < Exit) armed = true;
        if (armed && lt >= Enter) { armed = false; Active = true; return Decision.Begin; }
        return Decision.None;
    }
    public void Reset() { Active = false; armed = false; }
    public static float DeadZone(float x, float zone) => Math.Abs(x) <= zone ? 0 : Math.Sign(x) * (Math.Abs(x) - zone) / (1 - zone);
}

// A deliberate stick deflection yields one quarter-turn, then requires neutral.
public sealed class QuarterTurnInput
{
    bool armed;
    public void Reset() => armed = false;
    public (int x, int y) Step(float x, float y)
    {
        if (Math.Max(Math.Abs(x), Math.Abs(y)) <= .18f) { armed = true; return (0, 0); }
        if (!armed || Math.Max(Math.Abs(x), Math.Abs(y)) < .55f) return (0, 0);
        armed = false;
        return Math.Abs(x) >= Math.Abs(y) ? (Math.Sign(x), 0) : (0, Math.Sign(y));
    }
}
