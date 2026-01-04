using System.IO;

namespace W2ScriptMerger.Models;

public class ModArchive
{
    public string FilePath { get; init; } = string.Empty;
    public string ModName => Path.GetFileNameWithoutExtension(FilePath);
    public List<ModFile> Files { get; } = [];
    public InstallLocation ModInstallLocation { get; set; } = InstallLocation.CookedPC;
    public bool IsLoaded { get; set; }
    public string? Error { get; set; }
}
