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
                    var normalizedPath = entry.Key.NormalizePath();

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
                        fileStagingPath = $"CookedPC/{normalizedPath}";
                    }
                    else if (normalizedPath.StartsWith("UserContent/", StringComparison.OrdinalIgnoreCase))
                    {
                        modArchive.ModInstallLocation = InstallLocation.UserContent;
                        fileStagingPath = $"UserContent/{normalizedPath}";
                    }
                    else
                    {
                        modArchive.ModInstallLocation = InstallLocation.Unknown;
                        fileStagingPath = $"CookedPC/{normalizedPath}"; // assume CookedPC for now
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
}
