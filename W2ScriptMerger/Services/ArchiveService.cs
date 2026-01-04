using System.IO;
using SharpCompress.Archives.Zip;
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

            using (var archive = ZipArchive.Open(archivePath))
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

                    // only include files from CookedPC
                    string relativePath;
                    if (normalizedPath.StartsWith("CookedPc/", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!normalizedPath.StartsWith("CookedPc/", StringComparison.OrdinalIgnoreCase))
                            continue;
                        relativePath = normalizedPath["CookedPc/".Length..];
                    }
                    else relativePath = Path.Combine("CookedPC", normalizedPath); // assume if there is no CookedPc/ prefix it should be under CookedPc/ and include it

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
            modArchive.Error = ex.Message;
            modArchive.IsLoaded = false;
        }

        return modArchive;
    }
}
