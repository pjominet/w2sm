namespace W2ScriptMerger.Models;

public class ModFile
{
    public string RelativePath { get; init; } = string.Empty;
    public byte[] Content { get; init; } = [];
    public ModFileType FileType { get; init; }
}

public enum ModFileType
{
    Script,      // .ws files
    Dzip,        // .dzip archives
    Xml,         // .xml files
    Strings,     // .w2strings localization
    Other        // Other files
}
