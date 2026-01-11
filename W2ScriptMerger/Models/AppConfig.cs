namespace W2ScriptMerger.Models;

// ReSharper disable InconsistentNaming
internal class AppConfig
{
    public string? GamePath { get; set; }
    public string? RuntimeDataPath { get; set; }
    public string? UserContentPath { get; set; }
    public string? LastModDirectory { get; set; }
    public bool PromptForUnknownInstallLocation { get; set; }
}

public enum InstallLocation
{
    Unknown = 0,
    CookedPC = 1,
    UserContent = 2
}
