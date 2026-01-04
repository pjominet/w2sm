namespace W2ScriptMerger.Models;

// ReSharper disable InconsistentNaming
public class AppConfig
{
    public string? GamePath { get; set; }
    public string? ModStagingPath { get; set; }
    public string? UserContentPath { get; set; }
    public string? LastModDirectory { get; set; }
    public InstallLocation DefaultInstallLocation { get; set; } = InstallLocation.CookedPC;
}

public enum InstallLocation
{
    Unknown = 0,
    CookedPC = 1,
    UserContent = 2
}
