using System.IO;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class GameFileService
{
    private readonly ConfigService _configService;
    private readonly DzipService _dzipService;
    private Dictionary<string, string>? _vanillaScriptIndex;

    public GameFileService(ConfigService configService, DzipService dzipService)
    {
        _configService = configService;
        _dzipService = dzipService;
    }

    public void BuildVanillaIndex()
    {
        _vanillaScriptIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var cookedPcPath = _configService.CookedPCPath;
        if (string.IsNullOrEmpty(cookedPcPath) || !Directory.Exists(cookedPcPath))
            return;

        // Index .dzip files in CookedPC
        var dzipFiles = Directory.GetFiles(cookedPcPath, "*.dzip", SearchOption.AllDirectories);
        foreach (var dzipFile in dzipFiles)
        {
            try
            {
                var entries = _dzipService.ReadDzip(dzipFile);
                foreach (var entry in entries)
                {
                    if (entry.Name.EndsWith(".ws", StringComparison.OrdinalIgnoreCase))
                    {
                        var key = NormalizeScriptPath(entry.Name);
                        if (!_vanillaScriptIndex.ContainsKey(key))
                        {
                            _vanillaScriptIndex[key] = dzipFile;
                        }
                    }
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
            if (!_vanillaScriptIndex.ContainsKey(key))
            {
                _vanillaScriptIndex[key] = wsFile;
            }
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

        if (sourcePath.EndsWith(".dzip", StringComparison.OrdinalIgnoreCase))
        {
            // Extract from dzip
            var entries = _dzipService.ReadDzip(sourcePath);
            var entry = entries.FirstOrDefault(e => 
                NormalizeScriptPath(e.Name).Equals(key, StringComparison.OrdinalIgnoreCase));
            
            if (entry is not null)
            {
                return _dzipService.ExtractEntry(sourcePath, entry);
            }
            return null;
        }
        else
        {
            // Read loose file
            return File.ReadAllBytes(sourcePath);
        }
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
        var userContentPath = _configService.UserContentPath;
        if (Directory.Exists(userContentPath))
        {
            mods.AddRange(Directory.GetDirectories(userContentPath));
            mods.AddRange(Directory.GetFiles(userContentPath, "*.dzip"));
        }

        return mods;
    }

    private static string NormalizeScriptPath(string path)
    {
        return path.Replace('\\', '/').ToLowerInvariant().TrimStart('/');
    }
}
