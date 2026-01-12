using System.IO;
using SharpSevenZip;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class ArchiveService(ConfigService configService)
{
    public async Task<ModArchive> LoadModArchive(string archivePath, CancellationToken? ctx = null)
    {
        var modArchive = new ModArchive
        {
            SourcePath = archivePath
        };

        try
        {
            // create mod folder in staging directory
            modArchive.StagingPath = Path.Combine(configService.ModStagingPath, modArchive.ModName);
            Directory.CreateDirectory(modArchive.StagingPath);

            // Create a temporary extraction directory
            var tempExtractPath = Path.Combine(Path.GetTempPath(), $"w2sm_extract_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempExtractPath);

            try
            {
                // Extract all files in one batch call with 7z native bulk extraction
                using (var extractor = new SharpSevenZipExtractor(archivePath))
                {
                    await Task.Run(() => extractor.ExtractArchive(tempExtractPath), ctx ?? CancellationToken.None);
                }

                // Parallel file processing
                var extractedFiles = Directory.EnumerateFiles(tempExtractPath, "*", SearchOption.AllDirectories);
                var token = ctx ?? CancellationToken.None;

                await Parallel.ForEachAsync(extractedFiles, new ParallelOptions
                {
                    CancellationToken = token,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                }, async (extractedFilePath, ct) =>
                {
                    var relativeArchivePath = Path.GetRelativePath(tempExtractPath, extractedFilePath).Replace('\\', '/');
                    await ProcessExtractedFileAsync(relativeArchivePath, extractedFilePath, modArchive, ct);
                });
            }
            finally
            {
                // Cleanup temp directory
                try { Directory.Delete(tempExtractPath, recursive: true); } catch { /* ignore cleanup errors */ }
            }

            modArchive.IsLoaded = true;
        }
        catch (Exception ex)
        {
            modArchive.Error = $"Extracting archive failed: {ex.Message}";
            modArchive.IsLoaded = false;
        }

        return modArchive;
    }

    private static async Task ProcessExtractedFileAsync(string archiveRelativePath, string extractedFilePath, ModArchive modArchive, CancellationToken ctx)
    {
        var normalizedPath = DetermineRelativeModFilePath(archiveRelativePath);

        // ignore empty entries
        if (!normalizedPath.HasValue())
            return;

        // ignore txt files, as they are not relevant to the mod (readme, manual install instructions, changelog, etc.)
        if (normalizedPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return;

        string fileStagingPath;
        if (normalizedPath.StartsWith("CookedPC/", StringComparison.OrdinalIgnoreCase))
        {
            lock (modArchive) { modArchive.ModInstallLocation = InstallLocation.CookedPC; }
            fileStagingPath = normalizedPath;
        }
        else if (normalizedPath.StartsWith("UserContent/", StringComparison.OrdinalIgnoreCase))
        {
            lock (modArchive) { modArchive.ModInstallLocation = InstallLocation.UserContent; }
            fileStagingPath = normalizedPath;
        }
        else
        {
            // Mark as Unknown - will be resolved during deployment based on user preference
            lock (modArchive) { modArchive.ModInstallLocation = InstallLocation.Unknown; }
            fileStagingPath = $"CookedPC/{normalizedPath}"; // Stage to CookedPC by default
        }

        var extension = Path.GetExtension(fileStagingPath);
        var type = extension switch
        {
            ".dzip" => ModFileType.Dzip,
            ".xml" => ModFileType.Xml,
            ".w2strings" => ModFileType.Strings,
            _ => ModFileType.Other
        };
        var shouldStoreContent = type is ModFileType.Dzip or ModFileType.Xml or ModFileType.Strings;

        // Copy to staging path
        var stagingPath = Path.Combine(modArchive.StagingPath, fileStagingPath);
        Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)!);

        byte[]? content = null;
        if (shouldStoreContent)
        {
            // Read content into memory and write to staging
            content = await File.ReadAllBytesAsync(extractedFilePath, ctx);
            await File.WriteAllBytesAsync(stagingPath, content, ctx);
        }
        else
        {
            // Stream copy for large files we don't need in memory
            await using var source = new FileStream(extractedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
            await using var dest = new FileStream(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
            await source.CopyToAsync(dest, ctx);
        }

        // Create ModFile reference
        lock (modArchive.Files)
        {
            modArchive.Files.Add(new ModFile
            {
                RelativePath = fileStagingPath,
                Content = content
            });
        }
    }

    /// <summary>
    /// Determines the relative path for a mod file by trimming the archive path to start from the first valid root directory
    /// ("CookedPc" or "UserContent", case-insensitive) encountered when traversing upwards from the file.
    /// This prevents nesting issues where archives contain multiple root directories, ensuring only the deepest valid root is used
    /// (as per game folder structure expectations). If no valid root is found, returns the original normalized path.
    /// </summary>
    /// <param name="archivePath">The raw archive entry path to the file.</param>
    /// <returns>The relative path starting from the appropriate root, or the original normalized path if no root found.</returns>
    private static string DetermineRelativeModFilePath(string? archivePath)
    {
        if (!archivePath.HasValue())
            return string.Empty;

        var segments = archivePath.NormalizePath().Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rootIndex = -1;

        // Find the index of the first "cookedpc" or "usercontent" starting from the bottom (closest to the file)
        for (var i = segments.Length - 1; i >= 0; i--)
        {
            var seg = segments[i].ToLowerInvariant();
            if (seg is not ("cookedpc" or "usercontent"))
                continue;

            rootIndex = i;
            break;
        }

        // If a root was found, return the path from that root onwards; otherwise, return the original normalized path
        return rootIndex >= 0
            ? string.Join("/", segments.Skip(rootIndex))
            : archivePath!;
    }
}
