namespace W2ScriptMerger.Models;

public class DzipEntry
{
    public string Name { get; set; } = string.Empty;
    public DateTime TimeStamp { get; set; }
    public long UncompressedSize { get; set; }
    public long Offset { get; set; }
    public long CompressedSize { get; set; }
    public byte[]? Data { get; set; }
}
