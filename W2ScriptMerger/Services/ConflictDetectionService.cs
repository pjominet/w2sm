using System.IO;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

internal class ConflictDetectionService(ScriptExtractionService extractionService)
{
    private readonly Dictionary<string, DzipConflict> _conflicts = new(StringComparer.OrdinalIgnoreCase);

    public List<DzipConflict> DetectConflicts(IEnumerable<ModArchive> mods)
    {
        _conflicts.Clear();

        foreach (var mod in mods)
        {
            foreach (var file in mod.Files.Where(f => f.Type == ModFileType.Dzip))
            {
                var dzipName = file.Name;

                if (!extractionService.IsVanillaDzip(dzipName))
                    continue;

                var vanillaDzipPath = extractionService.GetVanillaDzipPath(dzipName);
                if (vanillaDzipPath is null)
                    continue;

                if (!_conflicts.TryGetValue(dzipName, out var conflict))
                {
                    conflict = new DzipConflict
                    {
                        DzipName = dzipName,
                        VanillaDzipPath = vanillaDzipPath,
                        VanillaExtractedPath = extractionService.GetVanillaExtractedPath(dzipName)
                    };
                    _conflicts[dzipName] = conflict;
                }

                var modDzipPath = Path.Combine(mod.StagingPath, file.RelativePath);
                conflict.ModSources.Add(new ModDzipSource
                {
                    ModName = mod.ModName,  // Use ModName consistently (not DisplayName)
                    DzipPath = modDzipPath,
                    ExtractedPath = extractionService.GetModExtractedPath(mod.ModName, dzipName)
                });
            }
        }

        foreach (var conflict in _conflicts.Values)
        {
            ExtractAndAnalyzeConflict(conflict);
        }

        return _conflicts.Values.ToList();
    }

    private void ExtractAndAnalyzeConflict(DzipConflict conflict)
    {
        if (!Directory.Exists(conflict.VanillaExtractedPath))
        {
            DzipService.UnpackDzipTo(conflict.VanillaDzipPath, conflict.VanillaExtractedPath);
        }

        var vanillaScripts = extractionService.GetExtractedScripts(conflict.VanillaExtractedPath);

        foreach (var modSource in conflict.ModSources)
        {
            if (!Directory.Exists(modSource.ExtractedPath))
            {
                extractionService.ExtractModDzipForConflict(
                    modSource.ModName,
                    modSource.DzipPath,
                    conflict.DzipName);
            }
        }

        foreach (var scriptPath in vanillaScripts)
        {
            var vanillaScriptFullPath = Path.Combine(conflict.VanillaExtractedPath, scriptPath);
            var modVersions = new List<ModScriptVersion>();

            foreach (var modSource in conflict.ModSources)
            {
                var modScriptPath = Path.Combine(modSource.ExtractedPath, scriptPath);
                if (File.Exists(modScriptPath) && HasFileChanged(vanillaScriptFullPath, modScriptPath))
                {
                    modVersions.Add(new ModScriptVersion
                    {
                        ModName = modSource.ModName,
                        ScriptPath = modScriptPath
                    });
                }
            }

            if (modVersions.Count == 0)
                continue;

            var scriptConflict = new ScriptFileConflict
            {
                ScriptRelativePath = scriptPath,
                VanillaScriptPath = vanillaScriptFullPath,
                CurrentMergeBasePath = vanillaScriptFullPath
            };

            foreach (var mv in modVersions)
                scriptConflict.ModVersions.Add(mv);

            conflict.ScriptConflicts.Add(scriptConflict);
        }
    }

    /// <summary>
    /// Checks if a mod file differs from the vanilla version.
    /// Uses file size first (fast), then byte comparison if sizes match.
    /// </summary>
    private static bool HasFileChanged(string vanillaPath, string modPath)
    {
        var vanillaInfo = new FileInfo(vanillaPath);
        var modInfo = new FileInfo(modPath);

        // Different sizes = definitely changed
        if (vanillaInfo.Length != modInfo.Length)
            return true;

        // Same size - compare content
        var vanillaBytes = File.ReadAllBytes(vanillaPath);
        var modBytes = File.ReadAllBytes(modPath);
        return !vanillaBytes.SequenceEqual(modBytes);
    }
}
