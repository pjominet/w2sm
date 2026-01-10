using System.IO;

namespace W2ScriptMerger.Models;

public class DzipConflict
{
    public required string DzipName { get; init; }
    public required string VanillaDzipPath { get; init; }
    public string VanillaExtractedPath { get; set; } = string.Empty;
    public List<ModDzipSource> ModSources { get; } = [];
    public List<ScriptFileConflict> ScriptConflicts { get; } = [];
    public bool IsFullyMerged => ScriptConflicts.Count > 0 && ScriptConflicts.All(c => c.Status is ConflictStatus.AutoResolved or ConflictStatus.ManuallyResolved);
    public bool HasUnresolvedConflicts => ScriptConflicts.Any(c => c.Status is ConflictStatus.Unresolved or ConflictStatus.NeedsManualResolution);
}

public class ModDzipSource
{
    public required string ModName { get; init; }
    public required string DzipPath { get; init; }
    public string ExtractedPath { get; set; } = string.Empty;
}

public class ScriptFileConflict
{
    public required string ScriptRelativePath { get; init; }
    public string ScriptFileName => Path.GetFileName(ScriptRelativePath);
    public required string VanillaScriptPath { get; init; }
    public List<ModScriptVersion> ModVersions { get; } = [];
    public ConflictStatus Status { get; set; } = ConflictStatus.Unresolved;
    public byte[]? MergedContent { get; set; }
    public string? CurrentMergeBasePath { get; set; }
}

public class ModScriptVersion
{
    public required string ModName { get; init; }
    public required string ScriptPath { get; init; }
}

public enum ConflictStatus
{
    Unresolved,
    AutoResolved,
    ManuallyResolved,
    NeedsManualResolution
}
