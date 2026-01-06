using System.IO;

namespace W2ScriptMerger.Models;

public class ModConflict
{
    public required string OriginalFile { get; init; } = string.Empty;
    public string OriginalFileName => Path.GetFileName(OriginalFile);
    public List<string> ConflictingFiles { get; } = [];
    public ConflictStatus Status { get; set; } = ConflictStatus.Unresolved;
}

public enum ConflictStatus
{
    Unresolved,
    AutoResolved,
    ManuallyResolved,
    NeedsManualResolution
}
