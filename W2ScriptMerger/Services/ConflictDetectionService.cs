using System.IO;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class ConflictDetectionService(ScriptExtractionService extractionService, IndexService indexService)
{
    private readonly Dictionary<string, DzipConflict> _conflicts = new(StringComparer.OrdinalIgnoreCase);

    public async Task<List<DzipConflict>> DetectConflictsAsync(IEnumerable<ModArchive> mods, CancellationToken ctx = default)
    {
        _conflicts.Clear();
        var modDzipSources = new Dictionary<string, List<(ModArchive Mod, GameFile File)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods)
        {
            ctx.ThrowIfCancellationRequested();
            // Capture every DZIP provided by the mod so we can reason about conflicts per archive name
            foreach (var file in mod.Files.Where(f => f.Type is FileType.Dzip))
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

        // Create conflicts only for:
        // 1. Vanilla dzips touched by at least one CookedPC (or unknown) mod
        // 2. Mod-added dzips where two or more mods within the same install root ship the same archive
        foreach (var (dzipName, sources) in modDzipSources)
        {
            var isVanillaDzip = indexService.IsVanillaDzip(dzipName);

            if (isVanillaDzip)
            {
                // Vanilla comparisons only make sense for CookedPC mods; UserContent mods should not be diffed against vanilla files.
                var cookedSources = sources
                    .Where(s => NormalizeInstallLocation(s.Mod.ModInstallLocation) is InstallLocation.CookedPC)
                    .ToList();

                if (cookedSources.Count == 0)
                    continue;

                CreateConflict(dzipName, InstallLocation.CookedPC, cookedSources, isVanillaDzip);
                continue;
            }

            foreach (var group in sources.GroupBy(s => NormalizeInstallLocation(s.Mod.ModInstallLocation)))
            {
                var location = group.Key;
                var groupedSources = group.ToList();

                // For mod-added dzips, only compare mods living in the same install root (CookedPC or UserContent).
                if (groupedSources.Count < 2)
                    continue;

                CreateConflict(dzipName, location, groupedSources, isVanillaDzip);
            }
        }

        var analysisTasks = _conflicts.Values.Select(conflict => ExtractAndAnalyzeConflictAsync(conflict, ctx)).ToList();
        await Task.WhenAll(analysisTasks);

        // Filter out conflicts with no script changes
        return _conflicts.Values.Where(c => c.ScriptConflicts.Count > 0).ToList();
    }

    private void CreateConflict(string dzipName, InstallLocation location, List<(ModArchive Mod, GameFile File)> sources, bool isVanillaDzip)
    {
        var conflictKey = BuildConflictKey(dzipName, location);

        // Vanilla conflicts use the actual game file; mod-added conflicts use the first mod as the merge base.
        var dzipPath = isVanillaDzip ? indexService.GetGameDzipPath(dzipName) : null;
        string extractionPath;

        if (isVanillaDzip && dzipPath is not null)
            extractionPath = extractionService.GetGameFileExtractionPath(dzipName);
        else
        {
            // For mod-added dzips, or when vanilla path is missing, use the first mod's version as the "base"
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

        _conflicts[conflictKey] = conflict;
    }

    // Unknown mods default to CookedPC for now
    private static InstallLocation NormalizeInstallLocation(InstallLocation location) => location is InstallLocation.Unknown ? InstallLocation.CookedPC : location;

    private static string BuildConflictKey(string dzipName, InstallLocation location) => $"{location}:{dzipName}";

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
                // Extract each mod's DZIP lazily so we only unpack archives that truly participate in a conflict
                await extractionService.ExtractModDzipForConflictAsync(modSource.ModName, modSource.DzipPath, conflict.DzipName, ctx);
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
