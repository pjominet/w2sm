using System.IO;

namespace W2ScriptMerger.Models;

public class ModArchive
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public List<ModFile> Files { get; set; } = new();
    public bool IsLoaded { get; set; }
    public string? Error { get; set; }
    public InstallLocation TargetLocation { get; set; } = InstallLocation.UserContent;
}
