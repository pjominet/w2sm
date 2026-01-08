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
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    var normalizedPath = DetermineRelativeModFilePath(entry.Key);

                    // ignore empty entries
                    if (!normalizedPath.HasValue())
                        continue;

                    // ignore txt files, as they are not relevant to the mod (readme, manual install instructions, changelog, etc.)
                    if (normalizedPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                        continue;

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
                        modArchive.ModInstallLocation = InstallLocation.Unknown;
                        fileStagingPath = $"CookedPC/{normalizedPath}"; // assume CookedPC for loose files & folders
                    }

                    await using var entryStream = await entry.OpenEntryStreamAsync(ctx ?? CancellationToken.None);
                    using var ms = new MemoryStream();
                    await entryStream.CopyToAsync(ms);
                    ms.Position = 0;

                    // Extract to file
                    var stagingPath = Path.Combine(modArchive.StagingPath, fileStagingPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)!);
                    await using (var fileStream = File.Create(stagingPath))
                    {
                        await ms.CopyToAsync(fileStream);
                    }

                    // Create ModFile reference
                    modArchive.Files.Add(new ModFile
                    {
                        RelativePath = fileStagingPath,
                        Content = ms.ToArray()
                    });
                }
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
