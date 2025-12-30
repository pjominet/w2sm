using System.IO;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class GameFileService(ConfigService configService, DzipService dzipService)
{
    private Dictionary<string, string>? _vanillaScriptIndex;

    public void BuildVanillaIndex()
    {
        _vanillaScriptIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var cookedPcPath = configService.CookedPCPath;
        if (string.IsNullOrEmpty(cookedPcPath) || !Directory.Exists(cookedPcPath))
            return;

        // Index .dzip files in CookedPC
        var dzipFiles = Directory.GetFiles(cookedPcPath, "*.dzip", SearchOption.AllDirectories);
        foreach (var dzipFile in dzipFiles)
        {
            try
            {
                var entries = dzipService.ReadDzip(dzipFile);
                foreach (var entry in entries)
                {
                    if (!entry.Name.EndsWith(".ws", StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    var key = NormalizeScriptPath(entry.Name);
                    _vanillaScriptIndex.TryAdd(key, dzipFile);
                }
            }
            catch
            {
                // Skip invalid dzip files
            }
        }

        // Index loose .ws files
        var wsFiles = Directory.GetFiles(cookedPcPath, "*.ws", SearchOption.AllDirectories);
        foreach (var wsFile in wsFiles)
        {
            var relativePath = Path.GetRelativePath(cookedPcPath, wsFile);
            var key = NormalizeScriptPath(relativePath);
            _vanillaScriptIndex.TryAdd(key, wsFile);
        }
    }

    public bool HasVanillaScript(string relativePath)
    {
        if (_vanillaScriptIndex is null)
            BuildVanillaIndex();

        var key = NormalizeScriptPath(relativePath);
        return _vanillaScriptIndex!.ContainsKey(key);
    }

    public byte[]? GetVanillaScriptContent(string relativePath)
    {
        if (_vanillaScriptIndex is null)
            BuildVanillaIndex();

        var key = NormalizeScriptPath(relativePath);
        if (!_vanillaScriptIndex!.TryGetValue(key, out var sourcePath))
            return null;

        if (!sourcePath.EndsWith(".dzip", StringComparison.OrdinalIgnoreCase)) 
            return File.ReadAllBytes(sourcePath);
        
        // Extract from dzip
        var entries = dzipService.ReadDzip(sourcePath);
        var entry = entries.FirstOrDefault(e => 
            NormalizeScriptPath(e.Name).Equals(key, StringComparison.OrdinalIgnoreCase));
            
        return entry is not null ? DzipService.ExtractEntry(sourcePath, entry) : null;
    }

    public HashSet<string> GetAllVanillaScriptPaths()
    {
        if (_vanillaScriptIndex is null)
            BuildVanillaIndex();

        return new HashSet<string>(_vanillaScriptIndex!.Keys, StringComparer.OrdinalIgnoreCase);
    }

    public List<string> GetInstalledMods()
    {
        var mods = new List<string>();

        // Check UserContent
        var userContentPath = configService.UserContentPath;
        if (!Directory.Exists(userContentPath)) 
            return mods;
        
        mods.AddRange(Directory.GetDirectories(userContentPath));
        mods.AddRange(Directory.GetFiles(userContentPath, "*.dzip"));

        return mods;
    }

    private static string NormalizeScriptPath(string path) => path.Replace('\\', '/').ToLowerInvariant().TrimStart('/');
}
