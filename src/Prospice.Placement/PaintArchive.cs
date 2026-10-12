using System.IO.Compression;
using System.Text.Json;
namespace Prospice.Placement;
internal static class PaintArchive
{
    internal static void Write(string path, PaintDocument doc)
    {
        doc.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp=path+".tmp";
        try
        {
            using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            {
                using(var zip=new GZipStream(file,CompressionLevel.Fastest,true)) JsonSerializer.Serialize(zip,doc);
                file.Flush(true);
            }
            if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
        }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    internal static PaintDocument Read(string path)
    {
        using var file=File.OpenRead(path);
        using var zip=new GZipStream(file,CompressionMode.Decompress);
        // Bound decompressed input as well as counts; malformed sidecars cannot exhaust RAM.
        using var buffer=new MemoryStream(); byte[] chunk=new byte[65536]; int read;
        while((read=zip.Read(chunk,0,chunk.Length))>0) { if(buffer.Length+read>768L*1024*1024) throw new InvalidDataException("Paint archive too large."); buffer.Write(chunk,0,read); }
        buffer.Position=0;
        var doc=JsonSerializer.Deserialize<PaintDocument>(buffer) ?? throw new InvalidDataException("Empty paint archive."); doc.Validate(); return doc;
    }
}
