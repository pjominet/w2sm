using System.IO;
using SharpCompress.Archives;
using SharpCompress.Common;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class ArchiveService
{
    private static readonly string[] SupportedExtensions = { ".zip", ".7z", ".rar" };
    private static readonly string[] ScriptExtensions = { ".ws", ".dzip", ".xml", ".w2strings" };

    public bool IsSupportedArchive(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }

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

    public List<ModFile> GetScriptFiles(ModArchive archive)
    {
        return archive.Files
            .Where(f => f.FileType is ModFileType.Script or ModFileType.Dzip)
            .ToList();
    }

    public List<ModFile> GetNonConflictingFiles(ModArchive archive, HashSet<string> existingPaths)
    {
        return archive.Files
            .Where(f => !existingPaths.Contains(f.RelativePath.ToLowerInvariant()))
            .ToList();
    }

    public void ExtractToDirectory(string archivePath, string outputDirectory)
    {
        using var archive = ArchiveFactory.Open(archivePath);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            var outputPath = Path.Combine(outputDirectory, NormalizePath(entry.Key ?? string.Empty));
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            entry.WriteToFile(outputPath, new ExtractionOptions { ExtractFullPath = false, Overwrite = true });
        }
    }

    public void ExtractFile(ModFile modFile, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(outputPath, modFile.Content);
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
