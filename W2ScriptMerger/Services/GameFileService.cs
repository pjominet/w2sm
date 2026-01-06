using System.IO;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class GameFileService(ConfigService configService)
{
    private Dictionary<string, DzipReference> _dzipIndex = [];

    public void BuildDzipIndex()
    {
        _dzipIndex = new Dictionary<string, DzipReference>(StringComparer.OrdinalIgnoreCase);

        var cookedPcPath = configService.GameCookedPCPath;
        if (string.IsNullOrEmpty(cookedPcPath) || !Directory.Exists(cookedPcPath))
            return;

        // Index .dzip files in CookedPC in the game folder
        var dzipFiles = Directory.GetFiles(cookedPcPath, "*.dzip", SearchOption.AllDirectories);
        foreach (var dzipFile in dzipFiles)
        {
            _dzipIndex.Add(Path.GetFileName(dzipFile), new DzipReference
            {
                OverrideHistory = { { 0, dzipFile } }
            });
        }
    }

    public void AddDzip(string dzipName)
    {
        var cookedPcPath = configService.GameCookedPCPath;
        if (string.IsNullOrEmpty(cookedPcPath) || !Directory.Exists(cookedPcPath))
            return;

        if (DzipIsIndexed(dzipName))
        {
            var lastVersion = _dzipIndex[dzipName].OverrideHistory.Count;
            _dzipIndex[dzipName].OverrideHistory.Add(lastVersion + 1, Path.Combine(cookedPcPath, dzipName));
        }
        else _dzipIndex.Add(dzipName, new DzipReference
        {
            OverrideHistory = { { 0, Path.Combine(cookedPcPath, dzipName) } }
        });
    }

    public int GetDzipIndexCount() => _dzipIndex.Count;

    public bool DzipIsIndexed(string dzipName) => _dzipIndex.ContainsKey(dzipName);

    public DzipReference GetDzipReference(string dzipName) => _dzipIndex.GetValueOrDefault(dzipName) ?? throw new KeyNotFoundException();
}
