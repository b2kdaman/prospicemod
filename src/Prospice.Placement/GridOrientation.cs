using System.Numerics;
namespace Prospice.Placement;

// Signed unit axes keep all 24 cube orientations exact, without accumulated Euler drift.
// These axes are LOCAL TO THE GRID, never to the camera or carried-item pose.
public sealed class GridOrientation
{
    public Vector3 Right { get; private set; } = Vector3.UnitX;
    public Vector3 Up { get; private set; } = Vector3.UnitY;
    public Vector3 Forward { get; private set; } = Vector3.UnitZ;
    public void Reset() { Right = Vector3.UnitX; Up = Vector3.UnitY; Forward = Vector3.UnitZ; }
    public void Turn(int horizontal, int vertical)
    {
        Vector3 Rotate(Vector3 v)
        {
            if (vertical != 0) { int d = -Math.Sign(vertical); v = new(v.X, -d * v.Z, d * v.Y); }
            if (horizontal != 0) { int d = Math.Sign(horizontal); v = new(d * v.Z, v.Y, -d * v.X); }
            return v;
        }
        Right = Rotate(Right); Up = Rotate(Up); Forward = Rotate(Forward);
    }
}
