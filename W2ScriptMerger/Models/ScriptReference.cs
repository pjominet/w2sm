namespace W2ScriptMerger.Models;

public class ScriptReference
{
    public required string SourcePath { get; init; }
    public string? OverridenBy { get; set; } = null;

    public bool IsVanilla => OverridenBy is not null;
}
