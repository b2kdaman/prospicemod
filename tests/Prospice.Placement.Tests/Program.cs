using Prospice.Placement;
int passed = CargoTests.Run() + SecureTests.Run() + PaintTests.Run();
void Check(bool result, string name) { if (!result) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
PlacementGate Armed() { var g = new PlacementGate(); g.Step(true, 0, false, false); return g; }
var gate = new PlacementGate();
Check(gate.Step(true, 1, false, false) == Decision.None, "LT already held on pickup does not enter");
Check(gate.Step(false, 0, false, false) == Decision.None && gate.Step(true, 1, false, false) == Decision.None, "Neutral must be observed in eligible context");
gate.Step(true, 0, false, false);
Check(gate.Step(true, .54f, false, false) == Decision.None, "Enter hysteresis below .55");
Check(gate.Step(true, .55f, false, false) == Decision.Begin && gate.Active, "Fresh press starts exactly once");
Check(gate.Step(true, .35f, false, false) == Decision.None && gate.Active, "Release threshold is strictly below .35");
Check(gate.Step(true, .34f, false, false) == Decision.Release && !gate.Active, "Release commits once");
Check(gate.Step(true, 1, false, false) == Decision.None, "Invalid or valid release cannot immediately retrigger");
Check(gate.Step(true, 0, false, false) == Decision.None && gate.Step(true, 1, false, false) == Decision.Begin, "Neutral rearms");
Check(gate.Step(true, 0, true, true) == Decision.Drop && !gate.Active, "X wins simultaneous B and release");
Check(gate.Step(true, 1, false, false) == Decision.None, "X disarms until neutral");
gate = Armed();
Check(gate.Step(true, 1, false, true) == Decision.None && !gate.Active, "X on entry leaves vanilla in control");
gate = Armed(); gate.Step(true, 1, false, false);
Check(gate.Step(true, 0, true, false) == Decision.Cancel, "B wins release");
Check(gate.Step(true, 1, false, false) == Decision.None, "B requires neutral");
foreach (var reason in new[] { "menu", "disconnect", "lost ownership", "scene change", "keyboard takeover" })
{
    gate = Armed(); gate.Step(true, 1, false, false);
    Check(gate.Step(false, 1, false, false) == Decision.Cancel && !gate.Active, reason + " cancels");
    Check(gate.Step(true, 1, false, false) == Decision.None, reason + " requires fresh press");
}
Check(PlacementGate.DeadZone(.18f, .18f) == 0 && PlacementGate.DeadZone(-.18f, .18f) == 0, "Dead zone symmetric");
Check(PlacementGate.DeadZone(1, .18f) == 1 && PlacementGate.DeadZone(-1, .18f) == -1, "Full stick retains full positioning speed");
var turns = new QuarterTurnInput();
Check(turns.Step(1, 0) == (0, 0), "Rotation requires neutral after entering placement or positioning");
turns.Step(0, 0);
Check(turns.Step(.54f, 0) == (0, 0), "Small stick drift does not rotate");
Check(turns.Step(.55f, 0) == (1, 0), "Deliberate horizontal deflection makes one quarter turn");
Check(turns.Step(1, 0) == (0, 0), "Held stick cannot repeat rotation");
turns.Step(0, 0);
Check(turns.Step(0, -1) == (0, -1), "Vertical deflection makes a signed pitch step");
turns.Step(0, 0);
Check(turns.Step(.8f, 1) == (0, 1), "Diagonal selects one dominant axis");
turns.Reset();
Check(turns.Step(-1, 0) == (0, 0), "Cancellation resets rotation arming");
foreach (var hz in new[] { 30, 60, 144 })
{
    turns.Reset(); turns.Step(0, 0); int count = 0;
    for (int i = 0; i < hz; i++) count += turns.Step(1, 0).x;
    Check(count == 1, $"Held stick makes exactly one 90-degree turn at {hz}Hz");
}
var orientation = new GridOrientation();
Check(orientation.Up == System.Numerics.Vector3.UnitY && orientation.Forward == System.Numerics.Vector3.UnitZ,
    "Preview starts horizontal at grid zero");
orientation.Turn(1, 0);
Check(orientation.Forward == System.Numerics.Vector3.UnitX && orientation.Up == System.Numerics.Vector3.UnitY,
    "Yaw is exactly 90 degrees relative to the grid");
orientation.Turn(0, 1);
Check(orientation.Forward == System.Numerics.Vector3.UnitX && orientation.Up == -System.Numerics.Vector3.UnitZ,
    "Pitch uses the grid X axis after yaw, independent of camera");
foreach (var axis in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
{
    var before = (orientation.Right, orientation.Up, orientation.Forward);
    for (int i = 0; i < 4; i++) orientation.Turn(axis.Item1, axis.Item2);
    Check((orientation.Right, orientation.Up, orientation.Forward) == before, $"Four {axis} turns return exactly to start");
}
var seen = new HashSet<(System.Numerics.Vector3, System.Numerics.Vector3, System.Numerics.Vector3)>();
var random = new Random(311);
for (int i = 0; i < 10000; i++)
{
    var step = random.Next(4);
    orientation.Turn(step < 2 ? (step == 0 ? 1 : -1) : 0, step >= 2 ? (step == 2 ? 1 : -1) : 0);
    if (orientation.Right.LengthSquared() != 1 || orientation.Up.LengthSquared() != 1 || orientation.Forward.LengthSquared() != 1 ||
        System.Numerics.Vector3.Cross(orientation.Up, orientation.Forward) != orientation.Right)
        throw new Exception("Mixed turns lost exact orthonormal grid alignment");
    seen.Add((orientation.Right, orientation.Up, orientation.Forward));
}
Check(seen.Count == 24, "10000 mixed turns reach exactly 24 grid orientations without drift");
foreach (float heading in new[] { 0f, 37f, -123f, 179f })
{
    var truck = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, heading * MathF.PI / 180);
    foreach (var pose in seen)
    {
        var local = System.Numerics.Vector3.Transform(System.Numerics.Vector3.Transform(pose.Item3, truck), System.Numerics.Quaternion.Inverse(truck));
        if (System.Numerics.Vector3.Distance(local, pose.Item3) > .000001f) throw new Exception("Truck heading skewed grid orientation");
    }
    Check(true, $"All quarter-turn poses retain lattice alignment at truck heading {heading}");
}
orientation.Reset();
Check(orientation.Right == System.Numerics.Vector3.UnitX && orientation.Up == System.Numerics.Vector3.UnitY && orientation.Forward == System.Numerics.Vector3.UnitZ,
    "A new placement resets previous turns to horizontal grid zero");
var floorRange = StorageFloorMath.QueryRange(3, 3.5f, 2);
Check(floorRange.top > 3.5f && floorRange.top - floorRange.distance < 3, "Storage query spans the floor independently of preview height or pickup reach");
float belowPivot = StorageFloorMath.RootHeight(0, 1, 0, 0, 3, 0, -.4f, 2, 1, .045f);
float abovePivot = StorageFloorMath.RootHeight(0, 1, 0, 0, 3, 0, .7f, 2, 1, .045f);
Check(Math.Abs(belowPivot - 3.445f) < .00001f && Math.Abs(abovePivot - 2.345f) < .00001f,
    "Seating handles geometry above and below an off-centre item pivot");
foreach (float reach in new[] { .05f, 2f, 100f })
{
    var interval = StorageFloorMath.QueryRange(-20, -19.5f, reach);
    Check(interval.top >= -19.2f && interval.top - interval.distance < -20,
        $"Floor query still spans the storage bottom with {reach} metre reach");
}
foreach (var pose in seen)
{
    // Support of an off-centre box after each exact orientation, on a tilted truck floor.
    var axis = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(.2f, 1, -.1f));
    float min = float.PositiveInfinity;
    for (int bits = 0; bits < 8; bits++)
    {
        var p = pose.Item1 * (.3f + ((bits & 1) == 0 ? -.8f : .8f)) +
            pose.Item2 * (-.2f + ((bits & 2) == 0 ? -.4f : .4f)) + pose.Item3 * ((bits & 4) == 0 ? -.6f : .6f);
        min = Math.Min(min, System.Numerics.Vector3.Dot(p, axis));
    }
    float y = StorageFloorMath.RootHeight(axis.X, axis.Y, axis.Z, 0, 2, 0, min, 1, -2, .045f);
    float seated = min + axis.X + axis.Y * y - axis.Z * 2;
    if (seated < axis.Y * 2 + .04499f) throw new Exception("Rotated footprint clips tilted floor");
}
Check(true, "All 24 rotated off-centre footprints stay above a tilted floor");
Check(float.IsPositiveInfinity(StorageFloorMath.RootHeight(1, 0, 0, 0, 0, 0, 0, 0, 0, .045f)), "Unsafe vertical floor plane cannot produce a valid placement");
// Drifting sticks must use the configured neutral zone for rotation as well as movement.
turns.Reset();
Check(turns.Step(.25f, -.2f, .3f) == (0, 0) && turns.Step(.55f, 0, .3f) == (1, 0),
    "Configured larger neutral zone rearms a drifting stick");
Check(turns.Step(1, 0, .3f) == (0, 0), "Configured dead zone still requires neutral between turns");
turns.Reset(); turns.Step(.15f, 0, .1f);
Check(turns.Step(1, 0, .1f) == (0, 0), "Smaller neutral zone cannot rearm outside its configured boundary");
turns.Step(.1f, 0, .1f);
Check(turns.Step(0, -1, .1f) == (0, -1), "Configured neutral boundary accepts signed pitch");
turns.Reset(); turns.Step(.8f, 0, .8f);
Check(turns.Step(.805f, 0, .8f) == (0, 0), "Large dead zone retains a distinct activation threshold");
Check(turns.Step(1, 0, .8f) == (1, 0), "Maximum configured dead zone still permits a full-stick turn");
var cleanupOrder = new List<string>();
var firstFailure = new InvalidOperationException("grid disposal");
var secondFailure = new InvalidOperationException("renderer restoration");
var cleanupErrors = Cleanup.Run(
    () => { cleanupOrder.Add("grid"); throw firstFailure; },
    () => { cleanupOrder.Add("preview"); throw secondFailure; },
    () => cleanupOrder.Add("aim"),
    () => cleanupOrder.Add("unpatch"));
Check(cleanupOrder.SequenceEqual(new[] { "grid", "preview", "aim", "unpatch" }),
    "Cleanup attempts every restoration after multiple failures");
Check(cleanupErrors.SequenceEqual(new[] { firstFailure, secondFailure }),
    "Cleanup preserves all failures for diagnostic reporting");
Check(Cleanup.Run(() => cleanupOrder.Add("done")).Count == 0,
    "Successful cleanup reports no errors");
var tap = new ToolCycleGate();
Check(!tap.Step(true,true), "Held right on context entry cannot cycle");
tap.Step(false,true);
Check(tap.Step(true,true) && tap.Suppress, "Single right press cycles immediately and suppresses native shortcut");
Check(!tap.Step(true,true), "Held right does not repeat");
Check(!tap.Step(false,true) && !tap.Suppress, "Release immediately restores native actions");
Check(tap.Step(true,true), "Next tap cycles without a timing window");
tap.Step(false,false);
Check(!tap.Step(true,true), "Interrupted control requires fresh release");
tap.Step(false,true);
Check(tap.Step(true,true), "Fresh tap after interruption cycles");
var slot=PaintToolSlot.Empty;
foreach(var expected in new[]{PaintToolSlot.Camera,PaintToolSlot.Airbrush,PaintToolSlot.Empty,PaintToolSlot.Camera})
{
    slot=ToolCycleGate.Next(slot,true);
    Check(slot==expected,"Three-state tap cycle selects "+expected);
}
Check(ToolCycleGate.Next(PaintToolSlot.Camera,false)==PaintToolSlot.Empty,"No airbrush preserves native camera-to-empty toggle");
Check(ToolCycleGate.Next(PaintToolSlot.Empty,false)==PaintToolSlot.Camera,"No airbrush preserves native empty-to-camera toggle");
Check(ToolCycleGate.Next(PaintToolSlot.Airbrush,false)==PaintToolSlot.Empty,"Missing airbrush resolves to empty");
Console.WriteLine($"{passed} checks passed.");
