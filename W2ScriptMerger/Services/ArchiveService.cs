using System.IO;
using SharpSevenZip;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Models;
using W2ScriptMerger.Tools;

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
                    var relativeArchivePath = Path.GetRelativePath(tempExtractPath, extractedFilePath).NormalizePath(false);
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
        // ignore unwanted entries
        if (!IsValidArchiveEntry(archiveRelativePath))
            return;

        var normalizedPath = ModPathHelper.DetermineRelativeModFilePath(archiveRelativePath);
        var (installLocation, relativePathWithRoot) = ModPathHelper.ResolveStagingPath(normalizedPath);
        lock (modArchive)
        {
            modArchive.ModInstallLocation = installLocation;
        }

        // Copy to staging path
        var stagingPath = Path.Combine(modArchive.StagingPath, relativePathWithRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)!);

        // Stream copy to never load entire files into memory, regardless of extension
        const int bufferSize = 5 * 16 * 1024; // 80KiB, tuned for high I/O throughput while keeping allocations small enough to be reused across many concurrent extract operations
        await using (var source = new FileStream(extractedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
        await using (var dest = new FileStream(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
        {
            await source.CopyToAsync(dest, ctx);
        }

        // Create ModFile reference
        lock (modArchive.Files)
        {
            modArchive.Files.Add(new ModFile
            {
                RelativePath = relativePathWithRoot
            });
        }
    }

    private static bool IsValidArchiveEntry(string archiveRelativePath)
    {
        string[] unwantedFileTypes = [ ".txt", ".png", ".jpg", "jpeg" ];

        // ignore empty entries
        if (!archiveRelativePath.HasValue())
            return false;

        // ignore unwanted files, as they are not relevant to the mod (readme, manual install instructions, changelogs, screenshots etc.)
        return !unwantedFileTypes.Contains(Path.GetExtension(archiveRelativePath), StringComparer.OrdinalIgnoreCase);
    }
}
