namespace W2ScriptMerger.Models;

public class DzipEntry
{
    public string Name { get; init; } = string.Empty;
    public DateTime TimeStamp { get; init; }
    public long ExpectedUncompressedSize { get; init; }
    public long Offset { get; init; }
    public long CompressedSize { get; init; }
}
