using System.IO;
using System.Text.Json;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

// ReSharper disable InconsistentNaming
public class ConfigService
{
    private readonly string _configPath;
    private readonly string _modStagingPath;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private AppConfig Config { get; }

    public ConfigService(JsonSerializerOptions options)
    {
        _jsonSerializerOptions = options;
        var exeLocation = AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(exeLocation, "config.json");
        _modStagingPath = Path.Combine(exeLocation, "modStaging");
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

    public string ModStagingPath
    {
        get => _modStagingPath;
        set
        {
            Config.ModStagingPath = value;
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

    public string? GameCookedPCPath => string.IsNullOrEmpty(Config.GamePath) ? null : Path.Combine(Config.GamePath, "CookedPC");

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

        return File.Exists(witcher2Exe) && Directory.Exists(cookedPC);
    }

    private AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
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
            var json = JsonSerializer.Serialize(Config, _jsonSerializerOptions);
            File.WriteAllText(_configPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
