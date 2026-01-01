using System.IO;
using SharpCompress.Archives;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public static class ArchiveService
{
    public static ModArchive LoadModArchive(string archivePath)
    {
        var modArchive = new ModArchive
        {
            FilePath = archivePath
        };

        try
        {
            using var archive = ArchiveFactory.Open(archivePath);
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                var relativePath = NormalizePath(entry.Key ?? string.Empty);
                var ext = Path.GetExtension(relativePath).ToLowerInvariant();

                using var stream = entry.OpenEntryStream();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);

                var modFile = new ModFile
                {
                    RelativePath = relativePath,
                    Content = ms.ToArray(),
                    SourceArchive = archivePath,
                    FileType = GetFileType(ext)
                };

                modArchive.Files.Add(modFile);
            }

            modArchive.IsLoaded = true;
        }
        catch (Exception ex)
        {
            modArchive.Error = ex.Message;
            modArchive.IsLoaded = false;
        }

        return modArchive;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static ModFileType GetFileType(string extension)
    {
        return extension switch
        {
            ".ws" => ModFileType.Script,
            ".dzip" => ModFileType.Dzip,
            ".xml" => ModFileType.Xml,
            ".w2strings" => ModFileType.Strings,
            _ => ModFileType.Other
        };
    }
}
