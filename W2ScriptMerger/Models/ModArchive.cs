using System.IO;
using System.Text.Json.Serialization;

namespace W2ScriptMerger.Models;

public class ModArchive
{
    public string SourcePath { get; init; } = string.Empty;
    public string ModName => Path.GetFileNameWithoutExtension(SourcePath);
    public List<ModFile> Files { get; set; } = [];
    public InstallLocation ModInstallLocation { get; set; } = InstallLocation.CookedPC;
    public string StagingPath { get; set; } = string.Empty;
    [JsonIgnore]
    public bool IsLoaded { get; set; }
    [JsonIgnore]
    public string? Error { get; set; }
}
