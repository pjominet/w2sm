using System.IO;

namespace W2ScriptMerger.Models;

public class GameFile
{
    public string RelativePath { get; init; } = string.Empty;
    public string Name => Path.GetFileName(RelativePath);
    public FileType Type => GetFileType(Path.GetExtension(RelativePath));

    private static FileType GetFileType(string extension)
    {
        return extension switch
        {
            ".dzip" => FileType.Dzip,
            ".xml" => FileType.Xml,
            ".w2strings" => FileType.Strings,
            _ => FileType.Other
        };
    }
}

public enum FileType
{
    Dzip = 1,        // .dzip archives
    Xml = 2,         // .xml files
    Strings = 3,     // .w2strings localization
    Other = 4        // Other files
}
