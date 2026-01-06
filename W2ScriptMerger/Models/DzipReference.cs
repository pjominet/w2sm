namespace W2ScriptMerger.Models;

public class DzipReference
{
    public SortedDictionary<int, string> OverrideHistory { get; set; } = [];

    public bool IsVanilla => OverrideHistory.Count == 1;
    public string GetCurrentScriptPath() => OverrideHistory.Last().Value;
    public string GetVanillaScriptPath() => OverrideHistory.First().Value;
    public string GetScriptPath(int version) => OverrideHistory[version];
}
