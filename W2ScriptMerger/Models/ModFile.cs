using System.IO;
using System.Text.Json.Serialization;

namespace W2ScriptMerger.Models;

public class ModFile
{
    public string RelativePath { get; init; } = string.Empty;
    [JsonIgnore]
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
    Dzip = 1,        // .dzip archives
    Xml = 2,         // .xml files
    Strings = 3,     // .w2strings localization
    Other = 4        // Other files
}
