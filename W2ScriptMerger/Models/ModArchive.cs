using System.IO;

namespace W2ScriptMerger.Models;

public class ModArchive
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public List<ModFile> Files { get; } = [];
    public bool IsLoaded { get; set; }
    public string? Error { get; set; }
}
