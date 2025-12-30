using System.IO;

namespace W2ScriptMerger.Models;

public class ModFile
{
    public string RelativePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(RelativePath);
    public string Extension => Path.GetExtension(RelativePath).ToLowerInvariant();
    public byte[] Content { get; init; } = [];
    public string SourceArchive { get; set; } = string.Empty;
    public ModFileType FileType { get; init; }
    public bool RequiresMerge { get; set; }
    public bool IsNewFile { get; set; }
}

public enum ModFileType
{
    Script,      // .ws files
    Dzip,        // .dzip archives
    Xml,         // .xml files
    Strings,     // .w2strings localization
    Other        // Other files
}
