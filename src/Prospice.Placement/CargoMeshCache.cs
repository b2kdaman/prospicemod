using BepInEx;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
namespace Prospice.Placement;

// Only local game-derived geometry lives in this ignored cache. No game mesh or
// texture is compiled into the mod or added to a release archive.
internal static class CargoMeshCache
{
    const string Hash = "511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D";
    sealed record Entry(Vector3 Center, Vector3 Extents, Vector3[] Vertices, Vector2[] UV, int[][] Indices)
    {
        internal Mesh? Mesh;
    }
    static readonly Dictionary<string, List<Entry>> entries = new();
    static bool loaded;
    internal static Mesh Readable(Mesh source)
    {
        if (source.isReadable) return source;
        if (!loaded) Load();
        if (!entries.TryGetValue(source.name, out var matches)) throw new InvalidOperationException("No private surface cache for " + source.name);
        foreach (var entry in matches)
        {
            if (entry.Vertices.Length != source.vertexCount || entry.Indices.Length != source.subMeshCount ||
                Vector3.Distance(entry.Center, source.bounds.center) > .0001f || Vector3.Distance(entry.Extents, source.bounds.extents) > .0001f) continue;
            bool counts = true;
            for (int i = 0; i < entry.Indices.Length; i++) if (entry.Indices[i].Length != source.GetIndexCount(i)) counts = false;
            if (!counts) continue;
            if (!entry.Mesh)
            {
                var mesh = new Mesh { name = "Prospice.Cargo.Query." + source.name, indexFormat = IndexFormat.UInt32 };
                entry.Mesh = mesh;
                mesh.vertices = entry.Vertices; mesh.uv = entry.UV; mesh.subMeshCount = entry.Indices.Length;
                for (int i = 0; i < entry.Indices.Length; i++) mesh.SetTriangles(entry.Indices[i], i);
                mesh.RecalculateBounds();
            }
            return entry.Mesh!;
        }
        throw new InvalidOperationException("Private surface cache does not match " + source.name);
    }
    static int Count(BinaryReader reader, int max)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > max) throw new InvalidDataException("Cargo cache count outside bounds.");
        return count;
    }
    static Vector3 Vector(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    static void Load()
    {
        var path = Path.Combine(Paths.BepInExRootPath, "cache", "prospice-cargo-" + Hash[..12] + ".bin");
        using var input = File.OpenRead(path);
        if (input.Length > 256 * 1024 * 1024) throw new InvalidDataException("Cargo cache too large.");
        using var reader = new BinaryReader(input, Encoding.UTF8);
        if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "PCARGO01" || Encoding.ASCII.GetString(reader.ReadBytes(64)) != Hash)
            throw new InvalidDataException("Cargo cache version or game hash mismatch.");
        try
        {
            int count = Count(reader, 4096);
            for (int n = 0; n < count; n++)
            {
                string name = Encoding.UTF8.GetString(reader.ReadBytes(Count(reader, 4096)));
                int vertexCount = Count(reader, 250000), submeshes = Count(reader, 256);
                var center = Vector(reader); var extents = Vector(reader);
                var vertices = new Vector3[vertexCount]; var uv = new Vector2[vertexCount]; var indices = new int[submeshes][];
                for (int i = 0; i < vertexCount; i++) vertices[i] = Vector(reader);
                for (int i = 0; i < vertexCount; i++) uv[i] = new(reader.ReadSingle(), reader.ReadSingle());
                for (int s = 0; s < submeshes; s++)
                {
                    indices[s] = new int[Count(reader, 1500000)];
                    if (indices[s].Length % 3 != 0) throw new InvalidDataException("Invalid triangle count.");
                    for (int i = 0; i < indices[s].Length; i++)
                    {
                        int index = reader.ReadInt32();
                        if (index < 0 || index >= vertexCount) throw new InvalidDataException("Invalid triangle index.");
                        indices[s][i] = index;
                    }
                }
                if (!entries.TryGetValue(name, out var list)) entries[name] = list = new();
                list.Add(new(center, extents, vertices, uv, indices));
            }
            if (input.Position != input.Length) throw new InvalidDataException("Trailing cargo cache data.");
            loaded = true;
        }
        catch { Clear(); throw; }
    }
    internal static void Clear()
    {
        foreach (var entry in entries.Values.SelectMany(v => v)) if (entry.Mesh) Object.Destroy(entry.Mesh);
        entries.Clear(); loaded = false;
    }
}
