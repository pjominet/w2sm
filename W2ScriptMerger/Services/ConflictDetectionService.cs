using System.IO;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

internal class ConflictDetectionService(ScriptExtractionService extractionService, IndexService indexService)
{
    private readonly Dictionary<string, DzipConflict> _conflicts = new(StringComparer.OrdinalIgnoreCase);

    public async Task<List<DzipConflict>> DetectConflictsAsync(IEnumerable<ModArchive> mods, CancellationToken ctx = default)
    {
        _conflicts.Clear();
        var modDzipSources = new Dictionary<string, List<(ModArchive Mod, GameFile File)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods)
        {
            ctx.ThrowIfCancellationRequested();
            foreach (var file in mod.Files.Where(f => f.Type == FileType.Dzip))
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
            var isVanillaDzip = indexService.IsVanillaDzip(dzipName);

            switch (isVanillaDzip)
            {
                // For mod-added, we need 2+ mods to create a conflict
                case false when sources.Count < 2:
                // Vanilla dzips need at only one mod to create a conflict
                case true when sources.Count < 1:
                    continue;
            }

            var dzipPath = indexService.GetGameDzipPath(dzipName);
            string extractionPath;

            if (isVanillaDzip && dzipPath is not null)
                extractionPath = extractionService.GetGameFileExtractionPath(dzipName);
            else
            {
                // For mod-added dzips, use the first mod's version as the "base"
                var firstSource = sources[0];
                dzipPath = Path.Combine(firstSource.Mod.StagingPath, firstSource.File.RelativePath);
                extractionPath = extractionService.GetModFileExtractionPath(firstSource.Mod.ModName, dzipName);
            }

            var conflict = new DzipConflict
            {
                DzipName = dzipName,
                BaseDzipPath = dzipPath,
                BaseExtractionPath = extractionPath,
                IsAddedByMod = !isVanillaDzip
            };
            _conflicts[dzipName] = conflict;

            foreach (var (mod, file) in sources)
            {
                var modDzipPath = Path.Combine(mod.StagingPath, file.RelativePath.ToSystemPath());
                conflict.ModSources.Add(new ModDzipSource
                {
                    ModName = mod.ModName,
                    DisplayName = mod.DisplayName,
                    DzipPath = modDzipPath,
                    ExtractedPath = extractionService.GetModFileExtractionPath(mod.ModName, dzipName)
                });
            }
        }

        var analysisTasks = _conflicts.Values.Select(conflict => ExtractAndAnalyzeConflictAsync(conflict, ctx)).ToList();
        await Task.WhenAll(analysisTasks);

        // Filter out conflicts with no script changes
        return _conflicts.Values.Where(c => c.ScriptConflicts.Count > 0).ToList();
    }

    private async Task ExtractAndAnalyzeConflictAsync(DzipConflict conflict, CancellationToken ctx)
    {
        ctx.ThrowIfCancellationRequested();
        if (!Directory.Exists(conflict.BaseExtractionPath))
            await DzipService.UnpackDzipToAsync(conflict.BaseDzipPath, conflict.BaseExtractionPath, ctx);

        ctx.ThrowIfCancellationRequested();
        var baseScripts = await extractionService.GetExtractedScriptsAsync(conflict.BaseExtractionPath, ctx);

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

        foreach (var scriptPath in baseScripts)
        {
            ctx.ThrowIfCancellationRequested();
            var vanillaScriptFullPath = Path.Combine(conflict.BaseExtractionPath, scriptPath);
            var modVersions = new List<ModScriptVersion>();

            foreach (var modSource in conflict.ModSources)
            {
                ctx.ThrowIfCancellationRequested();
                var modScriptPath = Path.Combine(modSource.ExtractedPath, scriptPath);
                if (File.Exists(modScriptPath) && await HasFileChangedAsync(vanillaScriptFullPath, modScriptPath, ctx))
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
    private static async Task<bool> HasFileChangedAsync(string vanillaPath, string modPath, CancellationToken ctx)
    {
        var vanillaInfo = new FileInfo(vanillaPath);
        var modInfo = new FileInfo(modPath);

        // Different sizes = definitely changed
        if (vanillaInfo.Length != modInfo.Length)
            return true;

        // Stream both files in fixed-size chunks to avoid allocating large byte arrays for whole scripts into memory
        const int bufferSize = 64 * 1024; // 64KiB, CPU cache friendly size, while minimizing the time spent copying arrays before comparison
        var vanillaBuffer = new byte[bufferSize];
        var modBuffer = new byte[bufferSize];

        await using var vanillaStream = new FileStream(vanillaPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        await using var modStream = new FileStream(modPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);

        while (true)
        {
            // Read the next chunk from both files and compare lengths first for early exit
            var vanillaRead = await vanillaStream.ReadAsync(vanillaBuffer.AsMemory(0, bufferSize), ctx);
            var modRead = await modStream.ReadAsync(modBuffer.AsMemory(0, bufferSize), ctx);

            if (vanillaRead != modRead)
                return true;

            if (vanillaRead == 0)
                break;

            // Byte-by-byte comparison of the chunk; any difference means the file changed
            for (var i = 0; i < vanillaRead; i++)
            {
                if (vanillaBuffer[i] != modBuffer[i])
                    return true;
            }
        }

        return false;
    }
}
