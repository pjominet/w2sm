using System.IO;
using System.Text.Json;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Models;
using W2ScriptMerger.Tools;

namespace W2ScriptMerger.Services;

public class IndexService(ConfigService configService)
{
    private static string GameFilesIndexPath => Path.Combine(Constants.APP_BASE_PATH, Constants.GAME_FILES_INDEX_FILENAME);

    private readonly Dictionary<string, GameFile> _gameFilesIndex = new(StringComparer.OrdinalIgnoreCase);

    public int GameDzipCount { get; private set; }
    public int ModDzipCount { get; private set; }

    public async Task IndexGameFiles(CancellationToken ctx = default)
    {
        var cookedPcPath = configService.GameCookedPCPath;
        if (!cookedPcPath.HasValue() || !Directory.Exists(cookedPcPath))
            return;

        GameDzipCount = 0;
        ModDzipCount = 0;

        // Load or create the game files index
        var isFirstRun = !File.Exists(GameFilesIndexPath);
        switch (isFirstRun)
        {
            case false:
                await LoadGameFilesIndex(ctx);
                break;
            case true:
            {
                // First run: index all files in CookedPC as game files
                var allFiles = await Task.Run(() => Directory.GetFiles(cookedPcPath, "*", SearchOption.AllDirectories), ctx);
                foreach (var filePath in allFiles)
                {
                    var relativePath = Path.GetRelativePath(cookedPcPath, filePath).NormalizePath();
                    var fileName = Path.GetFileName(filePath);
                    _gameFilesIndex[fileName] = new GameFile
                    {
                        RelativePath = relativePath
                    };
                }

                await SaveGameFilesIndex(ctx);
                break;
            }
        }

        // Count dzips
        var dzipFiles = Directory.GetFiles(cookedPcPath, "*.dzip", SearchOption.TopDirectoryOnly);
        foreach (var dzipPath in dzipFiles)
        {
            var dzipName = Path.GetFileName(dzipPath);
            if (_gameFilesIndex.ContainsKey(dzipName))
                GameDzipCount++;
            else ModDzipCount++;
        }
    }

    internal bool IsVanillaFile(string relativePath) => _gameFilesIndex.ContainsKey(relativePath);

    internal bool IsVanillaDzip(string dzipName) => _gameFilesIndex.ContainsKey(dzipName);

    internal string? GetGameDzipPath(string dzipName)
    {
        var  relativePath = _gameFilesIndex.GetValueOrDefault(dzipName)?.RelativePath;
        if (configService.GameCookedPCPath.HasValue() && relativePath.HasValue())
            return Path.Combine(configService.GameCookedPCPath, relativePath);

        return null;
    }

    private async Task LoadGameFilesIndex(CancellationToken ctx = default)
    {
        try
        {
            var json = await File.ReadAllTextAsync(GameFilesIndexPath, ctx);
            var files = JsonSerializer.Deserialize<Dictionary<string, GameFile>>(json);
            if (files is null)
                return;

            _gameFilesIndex.Clear();
            foreach (var (fileName, gameFile) in files)
                _gameFilesIndex.Add(fileName, gameFile);
        }
        catch
        {
            // If loading fails, treat as first run
            _gameFilesIndex.Clear();
        }
    }

    private async Task SaveGameFilesIndex(CancellationToken ctx = default)
    {
        var json = JsonSerializer.Serialize(_gameFilesIndex, configService.JsonSerializerOptions);
        await File.WriteAllTextAsync(GameFilesIndexPath, json, ctx);
    }
}
