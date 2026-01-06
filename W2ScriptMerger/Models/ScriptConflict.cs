namespace W2ScriptMerger.Models;

public class ScriptConflict
{
    public required string DzipSource { get; init; }
    public string RelativeScriptPath { get; set; } = string.Empty;
    public byte[] BaseScriptContent { get; set; } = [];
    public byte[] ConflictScriptContent { get; set; } = [];
}
