using System.IO;

namespace W2ScriptMerger.Models;

public class ScriptConflict
{
    public string RelativePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(RelativePath);
    public byte[]? VanillaContent { get; init; }
    public List<ModFileVersion> ModVersions { get; } = [];
    public ConflictStatus Status { get; set; } = ConflictStatus.Pending;
    public byte[]? MergedContent { get; set; }
}

public class ModFileVersion
{
    public string SourceArchive { get; init; } = string.Empty;
    public byte[] Content { get; init; } = [];
}

public enum ConflictStatus
{
    Pending,
    AutoMerged,
    ManuallyMerged,
    Failed,
    Skipped
}
