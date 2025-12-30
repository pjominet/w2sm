using System.IO;

namespace W2ScriptMerger.Models;

public class ScriptConflict
{
    public string RelativePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(RelativePath);
    public byte[]? VanillaContent { get; set; }
    public List<ModFileVersion> ModVersions { get; set; } = new();
    public ConflictStatus Status { get; set; } = ConflictStatus.Pending;
    public byte[]? MergedContent { get; set; }
    public bool CanAutoMerge { get; set; }
}

public class ModFileVersion
{
    public string SourceArchive { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public enum ConflictStatus
{
    Pending,
    AutoMerged,
    ManuallyMerged,
    Failed,
    Skipped
}
