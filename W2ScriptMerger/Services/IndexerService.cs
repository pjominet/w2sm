using System.IO;
using System.Text.Json;
using W2ScriptMerger.Tools;

namespace W2ScriptMerger.Services;

public class IndexerService(ConfigService configService)
{
    private static string AppBasePath => AppDomain.CurrentDomain.BaseDirectory;
    private static string VanillaFilesIndexPath => Path.Combine(AppBasePath, Constants.VANILLA_FILES_INDEX_FILENAME);

    private readonly HashSet<string> _vanillaFiles = new(StringComparer.OrdinalIgnoreCase);

    public int VanillaDzipCount { get; private set; }
    public int ModDzipCount { get; private set; }

    public void IndexVanillaFiles()
    {
        var cookedPcPath = configService.GameCookedPCPath;
        if (string.IsNullOrEmpty(cookedPcPath) || !Directory.Exists(cookedPcPath))
            return;

        VanillaDzipCount = 0;
        ModDzipCount = 0;

        // Load or create the vanilla files index
        var isFirstRun = !File.Exists(VanillaFilesIndexPath);
        switch (isFirstRun)
        {
            case false:
                LoadVanillaFilesIndex();
                break;
            case true:
            {
                // First run: index all files in CookedPC as vanilla
                var allFiles = Directory.GetFiles(cookedPcPath, "*", SearchOption.AllDirectories);
                foreach (var filePath in allFiles)
                {
                    var relativePath = Path.GetRelativePath(cookedPcPath, filePath);
                    _vanillaFiles.Add(relativePath);
                }
                SaveVanillaFilesIndex();
                break;
            }
        }

        // Count dzips
        var dzipFiles = Directory.GetFiles(cookedPcPath, "*.dzip", SearchOption.TopDirectoryOnly);
        foreach (var dzipPath in dzipFiles)
        {
            var dzipName = Path.GetFileName(dzipPath);

            if (_vanillaFiles.Contains(dzipName))
                VanillaDzipCount++;
            else
                ModDzipCount++;
        }
    }

    public bool IsVanillaFile(string relativePath) => _vanillaFiles.Contains(relativePath);

    public bool IsVanillaDzip(string dzipName) => _vanillaFiles.Contains(dzipName);

    private void LoadVanillaFilesIndex()
    {
        try
        {
            var json = File.ReadAllText(VanillaFilesIndexPath);
            var files = JsonSerializer.Deserialize<List<string>>(json);
            if (files is null)
                return;

            _vanillaFiles.Clear();
            foreach (var file in files)
                _vanillaFiles.Add(file);
        }
        catch
        {
            // If loading fails, treat as first run
            _vanillaFiles.Clear();
        }
    }

    private void SaveVanillaFilesIndex()
    {
        var json = JsonSerializer.Serialize(_vanillaFiles.ToList(), configService.JsonSerializerOptions);
        File.WriteAllText(VanillaFilesIndexPath, json);
    }
}
