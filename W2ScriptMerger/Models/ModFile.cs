using System.IO;

namespace W2ScriptMerger.Models;

public class ModFile
{
    public string RelativePath { get; init; } = string.Empty;
    public byte[] Content { get; init; } = [];

    public ModFileType FileType => GetFileType(Path.GetExtension(RelativePath));

    private static ModFileType GetFileType(string extension)
    {
        return extension switch
        {
            ".dzip" => ModFileType.Dzip,
            ".xml" => ModFileType.Xml,
            ".w2strings" => ModFileType.Strings,
            _ => ModFileType.Other
        };
    }
}

public enum ModFileType
{
    Dzip,        // .dzip archives
    Xml,         // .xml files
    Strings,     // .w2strings localization
    Other        // Other files
}
