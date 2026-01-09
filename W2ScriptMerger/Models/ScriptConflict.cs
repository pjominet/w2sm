namespace W2ScriptMerger.Models;

public class ScriptConflict
{
    public required string DzipSource { get; init; }
    public string DzipScriptPath { get; init; } = string.Empty;
    public required string BaseScriptPath { get; init; } = string.Empty;
    public required string ConflictScriptPath { get; init; } = string.Empty;
}
