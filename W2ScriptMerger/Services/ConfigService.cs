using System.IO;
using System.Text.Json;
using W2ScriptMerger.Models;
using W2ScriptMerger.Tools;

namespace W2ScriptMerger.Services;

// ReSharper disable InconsistentNaming
public class ConfigService
{
    private static string ExeLocation => AppDomain.CurrentDomain.BaseDirectory;
    private readonly string _configPath;
    private AppConfig Config { get; }

    public ConfigService(JsonSerializerOptions options)
    {
        JsonSerializerOptions = options;
        _configPath = Path.Combine(ExeLocation, Constants.CONFIG_FILENAME);
        Config = Load();
    }

    public JsonSerializerOptions JsonSerializerOptions { get; }

    public string? GamePath
    {
        get => Config.GamePath;
        set
        {
            Config.GamePath = value;
            Save();
        }
    }

    public string RuntimeDataPath
    {
        get => string.IsNullOrEmpty(Config.RuntimeDataPath) ? ExeLocation : Config.RuntimeDataPath;
        set
        {
            Config.RuntimeDataPath = value;
            Save();
        }
    }

    public string ModStagingPath => Path.Combine(RuntimeDataPath, Constants.STAGING_FOLDER);
    public string VanillaScriptsPath => Path.Combine(RuntimeDataPath, Constants.VANILLA_SCRIPTS_FOLDER);
    public string ModScriptsPath => Path.Combine(RuntimeDataPath, Constants.MOD_SCRIPTS_FOLDER);

    private static string[] RuntimeFolders =>
    [
        Constants.STAGING_FOLDER,
        Constants.VANILLA_SCRIPTS_FOLDER,
        Constants.MOD_SCRIPTS_FOLDER
    ];

    public void MigrateRuntimeData(string newPath)
    {
        var oldPath = RuntimeDataPath;
        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var folder in RuntimeFolders)
        {
            var sourceDir = Path.Combine(oldPath, folder);
            var destDir = Path.Combine(newPath, folder);

            if (!Directory.Exists(sourceDir))
                continue;

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);

            CopyDirectory(sourceDir, destDir);
            Directory.Delete(sourceDir, true);
        }

        RuntimeDataPath = newPath;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }

    public string UserContentPath
    {
        get
        {
            if (!string.IsNullOrEmpty(Config.UserContentPath))
                return Config.UserContentPath;

            var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(myDocuments, "Witcher 2", "UserContent");
        }
        set
        {
            Config.UserContentPath = value;
            Save();
        }
    }

    public string? GameCookedPCPath => string.IsNullOrEmpty(Config.GamePath) ? null : Path.Combine(Config.GamePath, "CookedPC");

    public string? LastModDirectory
    {
        get => Config.LastModDirectory;
        set
        {
            Config.LastModDirectory = value;
            Save();
        }
    }

    public bool PromptForUnknownInstallLocation
    {
        get => Config.PromptForUnknownInstallLocation;
        set
        {
            Config.PromptForUnknownInstallLocation = value;
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
            var json = JsonSerializer.Serialize(Config, JsonSerializerOptions);
            File.WriteAllText(_configPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
