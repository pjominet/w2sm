namespace W2ScriptMerger.Models;

public class AppConfig
{
    public string? GamePath { get; set; }
    public string? UserContentPath { get; set; }
    public string? LastModDirectory { get; set; }
    public InstallLocation DefaultInstallLocation { get; set; } = InstallLocation.UserContent;
    public List<string> RecentMods { get; set; } = new();
}

public enum InstallLocation
{
    UserContent,
    CookedPC
}
