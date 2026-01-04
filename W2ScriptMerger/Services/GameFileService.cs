using System.IO;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class GameFileService(ConfigService configService)
{
    private Dictionary<string, ScriptReference>? _vanillaScriptIndex;

    public void BuildGameScriptIndex()
    {
        _vanillaScriptIndex = new Dictionary<string, ScriptReference>(StringComparer.OrdinalIgnoreCase);

        var cookedPcPath = configService.GameCookedPCPath;
        if (string.IsNullOrEmpty(cookedPcPath) || !Directory.Exists(cookedPcPath))
            return;

        // Index .dzip files in CookedPC
        var scriptFiles = Directory.GetFiles(cookedPcPath, "*.dzip", SearchOption.AllDirectories);
        foreach (var scriptFile in scriptFiles)
        {
            _vanillaScriptIndex.Add(scriptFile, new ScriptReference
            {
                SourcePath = Path.Combine(cookedPcPath, Path.GetFileName(scriptFile))
            });
        }
    }

    public int GetScriptIndexCount() => _vanillaScriptIndex?.Count ?? 0;

    public byte[]? GetVanillaScriptContent(string relativePath)
    {
        if (_vanillaScriptIndex is null)
            BuildGameScriptIndex();

        var key = relativePath.NormalizePath();
        if (!_vanillaScriptIndex!.TryGetValue(key, out var scriptReference))
            return null;

        // Extract from dzip
        var entries = DzipService.UnpackDzip(scriptReference.SourcePath);
        var entry = entries.FirstOrDefault(e =>
            e.Name.NormalizePath().Equals(key, StringComparison.OrdinalIgnoreCase));

        return entry is not null ? DzipService.ExtractEntry(scriptReference.SourcePath, entry) : null;
    }
}
