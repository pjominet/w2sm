using System.IO;

namespace W2ScriptMerger.Models;

public class ScriptConflict
{
    public required string OriginalFilePath { get; init; } = string.Empty;
    public string OriginalFileName => Path.GetFileName(OriginalFilePath);
    public List<string> ConflictingFilePaths { get; } = [];
    public ConflictStatus Status { get; set; } = ConflictStatus.Unresolved;
}

public enum ConflictStatus
{
    Unresolved,
    AutoResolved,
    ManuallyResolved,
    Skipped
}
