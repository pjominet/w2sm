using System.IO;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Models;
using W2ScriptMerger.Tools;

namespace W2ScriptMerger.Services;

public class ScriptExtractionService(ConfigService configService)
{
    private string VanillaScriptsPath => configService.VanillaScriptsPath;
    private string ModScriptsPath => configService.ModScriptsPath;
    public string MergedScriptsPath => Path.Combine(configService.ModStagingPath, Constants.MERGED_SCRIPTS_FOLDER);

    internal async Task ExtractModDzipForConflictAsync(string modName, string modDzipPath, string dzipName, CancellationToken ctx)
    {
        Directory.CreateDirectory(ModScriptsPath);

        var modExtractPath = Path.Combine(ModScriptsPath, modName, dzipName);
        if (Directory.Exists(modExtractPath))
            return;

        await DzipService.UnpackDzipToAsync(modDzipPath.ToSystemPath(), modExtractPath, ctx);
    }

    internal string GetVanillaExtractedPath(string dzipName) => Path.Combine(VanillaScriptsPath, dzipName);

    internal string GetModExtractedPath(string modName, string dzipName) => Path.Combine(ModScriptsPath, modName, dzipName);

    internal List<string> GetExtractedScripts(string extractedDzipPath)
    {
        if (!Directory.Exists(extractedDzipPath))
            return [];

        return Directory.GetFiles(extractedDzipPath, "*.ws", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(extractedDzipPath, f))
            .ToList();
    }

    private void EnsureMergedScriptsFolder() => Directory.CreateDirectory(MergedScriptsPath);

    public void WriteMergeManifest(List<DzipConflict> conflicts)
    {
        EnsureMergedScriptsFolder();

        var manifestPath = Path.Combine(MergedScriptsPath, Constants.MERGE_SUMMARY_FILENAME);
        var lines = new List<string>
        {
            "# Merged Mods",
            "",
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            "",
            "## Included Mods",
            ""
        };

        var modNames = conflicts
            .SelectMany(c => c.ModSources)
            .Select(s => s.ModName)
            .Distinct()
            .OrderBy(n => n);

        lines.AddRange(modNames.Select(modName => $"- {modName}"));

        lines.Add("");
        lines.Add("## Merged Scripts");
        lines.Add("");

        foreach (var conflict in conflicts.Where(c => c.IsFullyMerged))
        {
            lines.Add($"### {conflict.DzipName}");
            lines.Add("");
            foreach (var script in conflict.ScriptConflicts)
            {
                var status = script.Status == ConflictStatus.AutoResolved ? "auto" : "manual";
                lines.Add($"- `{script.ScriptRelativePath}` ({status})");
            }
            lines.Add("");
        }

        File.WriteAllLines(manifestPath, lines);
    }

    public void SaveMergedScript(DzipConflict dzipConflict, ScriptFileConflict scriptConflict)
    {
        if (scriptConflict.MergedContent is null)
            return;

        EnsureMergedScriptsFolder();

        var dzipFolder = Path.Combine(MergedScriptsPath, dzipConflict.DzipName);
        var scriptPath = Path.Combine(dzipFolder, scriptConflict.ScriptRelativePath);

        var dir = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(scriptPath, scriptConflict.MergedContent);
    }

    public void LoadExistingMerges(List<DzipConflict> conflicts)
    {
        if (!Directory.Exists(MergedScriptsPath))
            return;

        foreach (var conflict in conflicts)
        {
            var dzipFolder = Path.Combine(MergedScriptsPath, conflict.DzipName);
            if (!Directory.Exists(dzipFolder))
                continue;

            foreach (var script in conflict.ScriptConflicts)
            {
                var mergedPath = Path.Combine(dzipFolder, script.ScriptRelativePath);
                if (!File.Exists(mergedPath))
                    continue;

                script.MergedContent = File.ReadAllBytes(mergedPath);
                script.Status = ConflictStatus.AutoResolved;
            }
        }
    }

    public string? PackMergedDzip(string dzipName)
    {
        var mergedDir = Path.Combine(MergedScriptsPath, dzipName);
        if (!Directory.Exists(mergedDir))
            return null;

        // Get vanilla extraction path - contains ALL original files
        var vanillaDir = Path.Combine(VanillaScriptsPath, dzipName);
        if (!Directory.Exists(vanillaDir))
            return null;

        // Create temp folder for combined output
        var packedFolder = Path.Combine(MergedScriptsPath, "packed");
        var tempCombinedDir = Path.Combine(packedFolder, $"{dzipName}_temp");

        // Clean up any previous temp folder
        if (Directory.Exists(tempCombinedDir))
            Directory.Delete(tempCombinedDir, true);

        Directory.CreateDirectory(tempCombinedDir);

        // Step 1: Copy ALL files from vanilla extraction
        DirectoryUtils.CopyDirectory(vanillaDir, tempCombinedDir);

        // Step 2: Overlay merged files (overwrites vanilla versions)
        DirectoryUtils.CopyDirectory(mergedDir, tempCombinedDir);

        // Step 3: Pack the combined directory
        var outputPath = Path.Combine(packedFolder, dzipName);
        if (File.Exists(outputPath))
            File.Delete(outputPath);

        DzipService.PackDzip(outputPath, tempCombinedDir);

        // Cleanup temp folder
        Directory.Delete(tempCombinedDir, true);

        return outputPath;
    }


    public void CleanupExtractedFiles()
    {
        if (Directory.Exists(ModScriptsPath))
            Directory.Delete(ModScriptsPath, true);
    }
}
