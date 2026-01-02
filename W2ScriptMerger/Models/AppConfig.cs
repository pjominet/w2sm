namespace W2ScriptMerger.Models;

// ReSharper disable InconsistentNaming
public class AppConfig
{
    public string? GamePath { get; set; }
    public string? UserContentPath { get; set; }
    public string? LastModDirectory { get; set; }
    public InstallLocation DefaultInstallLocation { get; set; } = InstallLocation.UserContent;
    public List<string> RecentMods { get; set; } = [];
}

public enum InstallLocation
{
    UserContent,
    CookedPC
}
