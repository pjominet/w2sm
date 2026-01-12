using System.IO;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

internal class ConflictDetectionService(ScriptExtractionService extractionService)
{
    private readonly Dictionary<string, DzipConflict> _conflicts = new(StringComparer.OrdinalIgnoreCase);

    public async Task<List<DzipConflict>> DetectConflictsAsync(IEnumerable<ModArchive> mods, CancellationToken ctx = default)
    {
        _conflicts.Clear();
        var modDzipSources = new Dictionary<string, List<(ModArchive Mod, ModFile File)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods)
        {
            ctx.ThrowIfCancellationRequested();
            foreach (var file in mod.Files.Where(f => f.Type == ModFileType.Dzip))
            {
                var dzipName = file.Name;
                if (!modDzipSources.TryGetValue(dzipName, out var sources))
                {
                    sources = [];
                    modDzipSources[dzipName] = sources;
                }
                sources.Add((mod, file));
            }
        }

        // Second pass: create conflicts only for:
        // 1. Vanilla dzips that any mod modifies
        // 2. Mod-added dzips that 2+ mods provide (conflict between mods)
        foreach (var (dzipName, sources) in modDzipSources)
        {
            var isVanillaDzip = extractionService.IsVanillaDzip(dzipName);

            switch (isVanillaDzip)
            {
                // For vanilla dzips, we need at least 1 mod; for mod-added, we need 2+ mods
                case false when sources.Count < 2:
                // Vanilla dzips need at least one mod to create a conflict
                case true when sources.Count < 1:
                    continue;
            }

            var vanillaDzipPath = extractionService.GetVanillaDzipPath(dzipName);
            string vanillaExtractedPath;

            if (isVanillaDzip && vanillaDzipPath is not null)
                vanillaExtractedPath = extractionService.GetVanillaExtractedPath(dzipName);
            else
            {
                // For mod-added dzips, use the first mod's version as the "base"
                var firstSource = sources[0];
                vanillaDzipPath = Path.Combine(firstSource.Mod.StagingPath, firstSource.File.RelativePath);
                vanillaExtractedPath = extractionService.GetModExtractedPath(firstSource.Mod.ModName, dzipName);
            }

            var conflict = new DzipConflict
            {
                DzipName = dzipName,
                VanillaDzipPath = vanillaDzipPath,
                VanillaExtractedPath = vanillaExtractedPath,
                IsAddedByMod = !isVanillaDzip
            };
            _conflicts[dzipName] = conflict;

            foreach (var (mod, file) in sources)
            {
                var modDzipPath = Path.Combine(mod.StagingPath, file.RelativePath);
                conflict.ModSources.Add(new ModDzipSource
                {
                    ModName = mod.ModName,
                    DisplayName = mod.DisplayName,
                    DzipPath = modDzipPath,
                    ExtractedPath = extractionService.GetModExtractedPath(mod.ModName, dzipName)
                });
            }
        }

        foreach (var conflict in _conflicts.Values)
            await ExtractAndAnalyzeConflictAsync(conflict, ctx);

        // Filter out conflicts with no script changes
        return _conflicts.Values.Where(c => c.ScriptConflicts.Count > 0).ToList();
    }

    private async Task ExtractAndAnalyzeConflictAsync(DzipConflict conflict, CancellationToken ctx)
    {
        ctx.ThrowIfCancellationRequested();
        if (!Directory.Exists(conflict.VanillaExtractedPath))
            await Task.Run(async () => await DzipService.UnpackDzipToAsync(conflict.VanillaDzipPath, conflict.VanillaExtractedPath, ctx), ctx);

        ctx.ThrowIfCancellationRequested();
        var vanillaScripts = extractionService.GetExtractedScripts(conflict.VanillaExtractedPath);

        foreach (var modSource in conflict.ModSources)
        {
            ctx.ThrowIfCancellationRequested();
            if (!Directory.Exists(modSource.ExtractedPath))
            {
                await extractionService.ExtractModDzipForConflictAsync(
                    modSource.ModName,
                    modSource.DzipPath,
                    conflict.DzipName,
                    ctx);
            }
        }

        foreach (var scriptPath in vanillaScripts)
        {
            ctx.ThrowIfCancellationRequested();
            var vanillaScriptFullPath = Path.Combine(conflict.VanillaExtractedPath, scriptPath);
            var modVersions = new List<ModScriptVersion>();

            foreach (var modSource in conflict.ModSources)
            {
                ctx.ThrowIfCancellationRequested();
                var modScriptPath = Path.Combine(modSource.ExtractedPath, scriptPath);
                if (File.Exists(modScriptPath) && HasFileChanged(vanillaScriptFullPath, modScriptPath))
                {
                    modVersions.Add(new ModScriptVersion
                    {
                        ModName = modSource.ModName,
                        DisplayName = modSource.DisplayName,
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
