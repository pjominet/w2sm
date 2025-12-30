using System.IO;
using Newtonsoft.Json;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class ConfigService
{
    private readonly string _configPath;
    private AppConfig _config;

    public ConfigService()
    {
        var exeLocation = AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(exeLocation, "config.json");
        _config = Load();
    }

    public AppConfig Config => _config;

    public string? GamePath
    {
        get => _config.GamePath;
        set
        {
            _config.GamePath = value;
            Save();
        }
    }

    public string UserContentPath
    {
        get
        {
            if (!string.IsNullOrEmpty(_config.UserContentPath))
                return _config.UserContentPath;
            
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "Witcher 2", "UserContent");
        }
        set
        {
            _config.UserContentPath = value;
            Save();
        }
    }

    public string? CookedPCPath
    {
        get
        {
            if (string.IsNullOrEmpty(_config.GamePath))
                return null;
            return Path.Combine(_config.GamePath, "CookedPC");
        }
    }

    public InstallLocation DefaultInstallLocation
    {
        get => _config.DefaultInstallLocation;
        set
        {
            _config.DefaultInstallLocation = value;
            Save();
        }
    }

    public string? LastModDirectory
    {
        get => _config.LastModDirectory;
        set
        {
            _config.LastModDirectory = value;
            Save();
        }
    }

    public bool IsGamePathValid()
    {
        if (string.IsNullOrEmpty(_config.GamePath))
            return false;

        var witcher2Exe = Path.Combine(_config.GamePath, "bin", "witcher2.exe");
        var cookedPC = Path.Combine(_config.GamePath, "CookedPC");
        
        return File.Exists(witcher2Exe) || Directory.Exists(cookedPC);
    }

    public void AddRecentMod(string path)
    {
        _config.RecentMods.Remove(path);
        _config.RecentMods.Insert(0, path);
        if (_config.RecentMods.Count > 10)
            _config.RecentMods = _config.RecentMods.Take(10).ToList();
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

    public void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
