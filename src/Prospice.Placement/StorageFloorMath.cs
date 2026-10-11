namespace Prospice.Placement;

// A floor plane constrains the whole item, including off-centre/rotated geometry.
public static class StorageFloorMath
{
    public static float RootHeight(float nx, float ny, float nz, float planeX, float planeY, float planeZ,
        float itemMinimum, float rootX, float rootZ, float clearance)
    {
        if (!float.IsFinite(ny) || ny < .5f) return float.PositiveInfinity;
        return (nx * planeX + ny * planeY + nz * planeZ + clearance - itemMinimum - nx * rootX - nz * rootZ) / ny;
    }
    public static (float top, float distance) QueryRange(float markerBottom, float markerTop, float headroom)
    {
        float top = markerTop + Math.Max(.3f, headroom);
        return (top, top - markerBottom + .5f);
    }
}
