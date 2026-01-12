using System.IO;
using System.Text.Json.Serialization;
using W2ScriptMerger.Extensions;

namespace W2ScriptMerger.Models;

public class ModFile
{
    public string RelativePath { get; init; } = string.Empty;
    public string Name => Path.GetFileName(RelativePath.NormalizePath());
    public ModFileType Type => GetFileType(Path.GetExtension(RelativePath));

    [JsonIgnore]
    public byte[]? Content { get; init; }

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
