using Prospice.Placement;

internal static class SecureTests
{
    internal static int Run()
    {
        int passed = 0;
        void Check(bool result, string name) { if (!result) throw new Exception(name); passed++; Console.WriteLine("PASS Secure: " + name); }
        var g = new SecurePressGate();
        Check(!g.Step(true, 1, 2, 3, true), "entering deck aim with held control does not secure");
        Check(!g.Step(true, 1, 2, 3, true), "held control stays disarmed");
        Check(!g.Step(true, 1, 2, 3, false) && g.Step(true, 1, 2, 3, true), "release and fresh press secures once");
        Check(!g.Step(true, 1, 2, 3, true), "holding does not repeatedly rebuild belts");
        Check(!g.Step(true, 1, 2, 3, false) && g.Step(true, 1, 2, 3, true), "next fresh press allows new cargo to be secured");
        g.Step(true, 1, 2, 3, false);
        Check(!g.Step(true, 4, 2, 3, true), "changing from bed to rack requires a fresh release");
        g.Step(true, 4, 2, 3, false);
        Check(!g.Step(true, 4, 5, 3, true), "controller switch cannot inherit an armed press");
        g.Step(true, 4, 5, 3, false);
        Check(!g.Step(true, 4, 5, 6, true), "changed binding cannot inherit an armed press");
        foreach (string reason in new[] { "menu", "holding cargo", "out of range", "blocked view", "disconnect", "vehicle entry", "pending secure" })
        {
            g.Step(true, 1, 2, 3, false);
            Check(!g.Step(false, 1, 2, 3, true) && !g.Step(true, 1, 2, 3, true), reason + " disarms control");
        }
        g.Step(true, 1, 2, 3, false); g.Reset();
        Check(!g.Step(true, 1, 2, 3, true), "shutdown resets input state");
        Check(SecurePressGate.FirstUnused(new[] { 9, 5, 4 }, new HashSet<int>()) == 9, "preferred unused right-stick control selected");
        Check(SecurePressGate.FirstUnused(new[] { 9, 5, 4 }, new HashSet<int> { 9 }) == 5, "remapped preferred button falls back to free control");
        Check(SecurePressGate.FirstUnused(new[] { 9, 5, 4 }, new HashSet<int> { 9, 5, 4 }) == -1, "no free control does not steal an action");
        Check(SecurePressGate.FirstUnused(new[] { -1, 9 }, new HashSet<int>()) == 9, "missing physical template control skipped");
        return passed;
    }
}
