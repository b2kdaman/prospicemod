namespace Prospice.Placement;
internal enum PaintToolSlot { Empty, Camera, Airbrush }
internal sealed class ToolCycleGate
{
    bool down, ready;
    internal bool Suppress { get; private set; }
    internal void Reset(){down=false;ready=false;Suppress=false;}
    internal bool Step(bool pressed,bool eligible)
    {
        if(!eligible){Reset();return false;}
        if(!pressed)ready=true;
        bool edge=pressed&&!down&&ready;down=pressed;Suppress=pressed;
        return edge;
    }
    internal static PaintToolSlot Next(PaintToolSlot current,bool hasAirbrush)=>current switch
    {
        PaintToolSlot.Empty=>PaintToolSlot.Camera,
        PaintToolSlot.Camera when hasAirbrush=>PaintToolSlot.Airbrush,
        _=>PaintToolSlot.Empty
    };
}
