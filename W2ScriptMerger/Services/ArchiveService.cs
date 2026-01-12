using System.IO;
using SharpCompress.Archives;
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

            using (var archive = ArchiveFactory.Open(archivePath))
            {
                // Collect entries to process
                var entriesToProcess = archive.Entries.Where(e => !e.IsDirectory).ToList();

                // Process entries concurrently
                var tasks = entriesToProcess.Select(entry => ProcessEntryAsync(entry, modArchive, ctx ?? CancellationToken.None));
                await Task.WhenAll(tasks);
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

    private static async Task ProcessEntryAsync(IArchiveEntry entry, ModArchive modArchive, CancellationToken ctx)
    {
        var normalizedPath = DetermineRelativeModFilePath(entry.Key);

        // ignore empty entries
        if (!normalizedPath.HasValue())
            return;

        // ignore txt files, as they are not relevant to the mod (readme, manual install instructions, changelog, etc.)
        if (normalizedPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return;

        string fileStagingPath;
        if (normalizedPath.StartsWith("CookedPC/", StringComparison.OrdinalIgnoreCase))
        {
            modArchive.ModInstallLocation = InstallLocation.CookedPC;
            fileStagingPath = normalizedPath;
        }
        else if (normalizedPath.StartsWith("UserContent/", StringComparison.OrdinalIgnoreCase))
        {
            modArchive.ModInstallLocation = InstallLocation.UserContent;
            fileStagingPath = normalizedPath;
        }
        else
        {
            // Mark as Unknown - will be resolved during deployment based on user preference
            modArchive.ModInstallLocation = InstallLocation.Unknown;
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

        await using var entryStream = await entry.OpenEntryStreamAsync(ctx);

        // Extract to file
        var stagingPath = Path.Combine(modArchive.StagingPath, fileStagingPath);
        Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)!);

        byte[]? content = null;
        if (shouldStoreContent)
        {
            using var ms = new MemoryStream();
            await entryStream.CopyToAsync(ms, ctx);
            ms.Position = 0;

            await using (var fileStream = File.Create(stagingPath))
            {
                await ms.CopyToAsync(fileStream, ctx);
            }

            content = ms.ToArray();
        }
        else
        {
            await using var fileStream = File.Create(stagingPath);
            await entryStream.CopyToAsync(fileStream, ctx);
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
