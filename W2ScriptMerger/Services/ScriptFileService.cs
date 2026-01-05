using System.IO;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class ScriptFileService(ConfigService configService)
{
    private Dictionary<string, ScriptReference> _scriptsIndex = [];

    public void BuildGameScriptsIndex()
    {
        _scriptsIndex = new Dictionary<string, ScriptReference>(StringComparer.OrdinalIgnoreCase);

        var cookedPcPath = configService.GameCookedPCPath;
        if (string.IsNullOrEmpty(cookedPcPath) || !Directory.Exists(cookedPcPath))
            return;

        // Index .dzip files in CookedPC in the game folder
        var scriptFilePaths = Directory.GetFiles(cookedPcPath, "*.dzip", SearchOption.AllDirectories);
        foreach (var scriptFilePath in scriptFilePaths)
        {
            var scriptFileName = Path.GetFileName(scriptFilePath);
            _scriptsIndex.Add(scriptFileName, new ScriptReference
            {
                OverrideHistory = { { 0, scriptFilePath } }
            });
        }
    }

    public void AddScript(string scriptName)
    {
        var cookedPcPath = configService.GameCookedPCPath;
        if (string.IsNullOrEmpty(cookedPcPath) || !Directory.Exists(cookedPcPath))
            return;

        if (ScriptExistsIndex(scriptName))
        {
            var lastVersion = _scriptsIndex[scriptName].OverrideHistory.Count;
            _scriptsIndex[scriptName].OverrideHistory.Add(lastVersion + 1, Path.Combine(cookedPcPath, scriptName));
        }
        else _scriptsIndex.Add(scriptName, new ScriptReference { OverrideHistory = { { 0, Path.Combine(cookedPcPath, scriptName) } } });
    }

    public int GetScriptIndexCount() => _scriptsIndex.Count;

    public bool ScriptExistsIndex(string scriptName) => _scriptsIndex?.ContainsKey(scriptName) ?? false;

    public ScriptReference GetScriptReference(string scriptName) => _scriptsIndex?.GetValueOrDefault(scriptName) ?? throw new KeyNotFoundException();
}
