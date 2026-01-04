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
            FilePath = archivePath
        };

        try
        {
            // create mod folder in staging directory
            var outputDir = Path.Combine(configService.ModStagingPath, modArchive.ModName);
            Directory.CreateDirectory(outputDir);

            using (var archive = ArchiveFactory.Open(archivePath))
            {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    var normalizedPath = entry.Key.NormalizePath();

                    // ignore empty entries
                    if (!normalizedPath.HasValue())
                        continue;

                    // ignore txt files, as they are not relevant to the mod (readme, manual install instructions, changelog, etc.)
                    if (normalizedPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string relativePath;
                    if (normalizedPath.StartsWith("CookedPc/", StringComparison.OrdinalIgnoreCase))
                    {
                        modArchive.ModInstallLocation = InstallLocation.CookedPC;
                        relativePath = normalizedPath["CookedPc/".Length..];
                    }
                    else if (normalizedPath.StartsWith("UserContent/", StringComparison.OrdinalIgnoreCase))
                    {
                        modArchive.ModInstallLocation = InstallLocation.UserContent;
                        relativePath = normalizedPath["UserContent/".Length..];
                    }
                    else
                    {
                        modArchive.ModInstallLocation = InstallLocation.Unknown;
                        relativePath = normalizedPath;
                    }

                    await using var entryStream = await entry.OpenEntryStreamAsync(ctx ?? CancellationToken.None);
                    using var ms = new MemoryStream();
                    await entryStream.CopyToAsync(ms);
                    ms.Position = 0;

                    // Extract to file
                    var extractedFilePath = Path.Combine(outputDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(extractedFilePath)!);
                    await using (var fileStream = File.Create(extractedFilePath))
                    {
                        await ms.CopyToAsync(fileStream);
                    }

                    // Create ModFile reference
                    modArchive.Files.Add(new ModFile
                    {
                        RelativePath = relativePath,
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
}
