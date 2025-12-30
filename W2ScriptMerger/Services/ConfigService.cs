using System.IO;
using Newtonsoft.Json;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class ConfigService
{
    private readonly string _configPath;
    private AppConfig Config { get; }

    public ConfigService()
    {
        var exeLocation = AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(exeLocation, "config.json");
        Config = Load();
    }

    public string? GamePath
    {
        get => Config.GamePath;
        set
        {
            Config.GamePath = value;
            Save();
        }
    }

    public string UserContentPath
    {
        get
        {
            if (!string.IsNullOrEmpty(Config.UserContentPath))
                return Config.UserContentPath;
            
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "Witcher 2", "UserContent");
        }
        set
        {
            Config.UserContentPath = value;
            Save();
        }
    }

    public string? CookedPCPath => string.IsNullOrEmpty(Config.GamePath) ? null : Path.Combine(Config.GamePath, "CookedPC");

    public InstallLocation DefaultInstallLocation
    {
        get => Config.DefaultInstallLocation;
        set
        {
            Config.DefaultInstallLocation = value;
            Save();
        }
    }

    public string? LastModDirectory
    {
        get => Config.LastModDirectory;
        set
        {
            Config.LastModDirectory = value;
            Save();
        }
    }

    public bool IsGamePathValid()
    {
        if (string.IsNullOrEmpty(Config.GamePath))
            return false;

        var witcher2Exe = Path.Combine(Config.GamePath, "bin", "witcher2.exe");
        var cookedPC = Path.Combine(Config.GamePath, "CookedPC");
        
        return File.Exists(witcher2Exe) || Directory.Exists(cookedPC);
    }

    public void AddRecentMod(string path)
    {
        Config.RecentMods.Remove(path);
        Config.RecentMods.Insert(0, path);
        if (Config.RecentMods.Count > 10)
            Config.RecentMods = Config.RecentMods.Take(10).ToList();
        Save();
    }

    private AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch
        {
            // Ignore errors, return default config
        }
        return new AppConfig();
    }

    private void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
