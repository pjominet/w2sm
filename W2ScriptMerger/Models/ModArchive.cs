using System.IO;
using System.Text.Json.Serialization;

namespace W2ScriptMerger.Models;

public class ModArchive
{
    public string FilePath { get; init; } = string.Empty;
    public string ModName => Path.GetFileNameWithoutExtension(FilePath);
    public List<ModFile> Files { get; set; } = [];
    public InstallLocation ModInstallLocation { get; set; } = InstallLocation.CookedPC;
    [JsonIgnore]
    public bool IsLoaded { get; set; }
    [JsonIgnore]
    public string? Error { get; set; }
}
