using System.IO;
using System.Text.Json;
using System.Windows;
using W2ScriptMerger.Models;
using W2ScriptMerger.Tools;
using W2ScriptMerger.Views;

namespace W2ScriptMerger.ViewModels;

public partial class MainViewModel
{
    private void UpdateLogText() => LogText = string.Join(Environment.NewLine, LogMessages);

    private async Task UpdateLoadedModsList()
    {
        var json = JsonSerializer.Serialize(LoadedMods.ToList(), _jsonSerializerOptions);
        await File.WriteAllTextAsync(ModsListPath, json);
    }

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var formatted = $"[{timestamp}] {message}";
        Application.Current.Dispatcher.Invoke(() => LogMessages.Add(formatted));
        _loggingService.Log(message);
    }

    private async Task ExtractVanillaScripts()
    {
        await Task.Run(() =>
        {
            _indexerService.IndexVanillaFiles();
            _extractionService.ExtractVanillaScripts();
            Application.Current.Dispatcher.Invoke(() =>
            {
                var modDzipMsg = _indexerService.ModDzipCount > 0
                    ? $", {_indexerService.ModDzipCount} mod dzips"
                    : "";
                Log($"Indexed {_indexerService.VanillaDzipCount} vanilla dzips{modDzipMsg}");
                StatusMessage = "Ready - Scripts indexed";
            });
        });
    }

    private async Task DetectStagedMods()
    {
        Log("Detecting staged mods...");
        if (!File.Exists(ModsListPath))
        {
            Log("No mods yet");
            return;
        }

        var json = await File.ReadAllTextAsync(ModsListPath);
        var mods = JsonSerializer.Deserialize<List<ModArchive>>(json);
        if (mods is null)
            return;

        var validMods = mods.Where(m =>
            !string.IsNullOrEmpty(m.StagingPath) &&
            Directory.Exists(m.StagingPath) &&
            m.Files.Count > 0).ToList();

        if (validMods.Count < mods.Count)
            Log($"Skipped {mods.Count - validMods.Count} mods with missing staging folders");

        if (validMods.Count == 0)
            return;

        Log($"Detected {validMods.Count} staged mods");
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            LoadedMods.Clear();
            foreach (var mod in validMods)
                LoadedMods.Add(mod);

            OnPropertyChanged(nameof(FilteredMods));
            await DetectConflictsAsync();
        });
    }

    private async Task DetectConflictsAsync()
    {
        DzipConflicts.Clear();

        if (LoadedMods.Count == 0)
            return;

        var conflicts = await Task.Run(() => _conflictDetectionService.DetectConflicts(LoadedMods.ToList()));

        _extractionService.LoadExistingMerges(conflicts);

        foreach (var conflict in conflicts)
            DzipConflicts.Add(conflict);

        var totalScripts = conflicts.Sum(c => c.ScriptConflicts.Count);
        var mergedScripts = conflicts.Sum(c => c.ScriptConflicts.Count(s => s.Status is ConflictStatus.AutoResolved or ConflictStatus.ManuallyResolved));

        HasExistingMerge = mergedScripts > 0;
        OnPropertyChanged(nameof(HasUnresolvedConflicts));

        Log(mergedScripts > 0
            ? $"Detected {DzipConflicts.Count} dzip conflicts ({totalScripts} scripts, {mergedScripts} already merged)"
            : $"Detected {DzipConflicts.Count} dzip conflicts ({totalScripts} scripts)");
    }

    private void OpenManualMergeEditor(DzipConflict dzipConflict, ScriptFileConflict scriptConflict)
    {
        var mergeWindow = new DiffMergeWindow(dzipConflict, scriptConflict, DzipConflicts.ToList())
        {
            Owner = Application.Current.MainWindow
        };

        if (mergeWindow.ShowDialog() != true)
            return;

        foreach (var resolved in mergeWindow.ResolvedConflicts)
        {
            _mergeService.ApplyManualMerge(resolved.Dzip, resolved.Script, resolved.MergedContent);
            Log($"Manually merged: {resolved.Script.ScriptFileName}");
        }

        var allResolved = DzipConflicts.All(c => c.IsFullyMerged);
        if (allResolved)
        {
            _extractionService.WriteMergeManifest(DzipConflicts.ToList());
            HasPendingMergeChanges = false;
            StatusMessage = "All conflicts resolved";
            Log("All conflicts resolved");

            var autoCount = DzipConflicts.Sum(c => c.ScriptConflicts.Count(s => s.Status == ConflictStatus.AutoResolved));
            var manualCount = mergeWindow.ResolvedConflicts.Count;
            ShowMergeSummary(autoCount, manualCount);
        }
        else
        {
            var remaining = DzipConflicts.Sum(c => c.ScriptConflicts.Count(s => s.Status is ConflictStatus.NeedsManualResolution));
            StatusMessage = $"{remaining} scripts still need manual resolution";
        }

        RefreshConflictsList();
    }

    private void ShowMergeSummary(int autoMergedCount, int manualMergedCount)
    {
        var mergedModsPath = Path.Combine(_extractionService.MergedScriptsPath, Constants.MERGE_SUMMARY_FILENAME);
        var dialog = new MergeSummaryDialog(mergedModsPath, autoMergedCount, manualMergedCount)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    private void RefreshConflictsList()
    {
        var conflicts = DzipConflicts.ToList();
        DzipConflicts.Clear();
        foreach (var c in conflicts)
            DzipConflicts.Add(c);
        OnPropertyChanged(nameof(HasUnresolvedConflicts));
    }

    private List<ModArchive> GetModsInSameMerge(ModArchive mod)
    {
        var result = new HashSet<ModArchive> { mod };

        // Find all merged conflicts that include this mod
        var mergedConflictsWithMod = DzipConflicts
            .Where(c => c.IsFullyMerged && c.ModSources.Any(s => s.ModName == mod.ModName))
            .ToList();

        if (mergedConflictsWithMod.Count == 0)
            return result.ToList();

        // Get all mod names from those conflicts
        var allModNamesInMerge = mergedConflictsWithMod
            .SelectMany(c => c.ModSources.Select(s => s.ModName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Find the corresponding ModArchive objects
        foreach (var loadedMod in LoadedMods)
        {
            if (allModNamesInMerge.Contains(loadedMod.ModName))
                result.Add(loadedMod);
        }

        return result.ToList();
    }
}
