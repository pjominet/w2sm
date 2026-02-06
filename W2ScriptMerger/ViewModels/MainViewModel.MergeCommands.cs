using System.Windows;
using CommunityToolkit.Mvvm.Input;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task StartMerge()
    {
        if (DzipConflicts.Count == 0)
        {
            StatusMessage = "No conflicts to merge";
            return;
        }

        IsBusy = true;
        StatusMessage = "Merging scripts...";

        var mergeResult = await Task.Run(() => _mergeService.StartMergeSession(DzipConflicts.ToList()));

        Log($"Auto-merged {mergeResult.AutoMergedCount} scripts");

        if (mergeResult.IsComplete)
        {
            _extractionService.WriteMergeManifest(DzipConflicts.ToList());
            HasPendingMergeChanges = false;
            HasExistingMerge = true;
            StatusMessage = $"Merge complete - {mergeResult.AutoMergedCount} scripts merged";
            Log("All conflicts resolved automatically");
            ShowMergeSummary(mergeResult.AutoMergedCount, 0);
        }
        else
        {
            Log($"{mergeResult.NeedsManualCount} scripts need manual resolution");
            StatusMessage = $"Manual merge required for {mergeResult.NeedsManualCount} scripts";

            if (mergeResult.FirstUnresolvedConflict.HasValue)
            {
                var (dzip, script) = mergeResult.FirstUnresolvedConflict.Value;
                SelectedDzipConflict = dzip;
                SelectedScriptConflict = script;
                OpenManualMergeEditor(dzip, script);
            }
        }

        IsBusy = false;
        RefreshConflictsList();
    }

    [RelayCommand]
    private void OpenDebugMergeEditor()
    {
        var dzipConflict = SelectedDzipConflict ?? DzipConflicts.FirstOrDefault();
        var scriptConflict = SelectedScriptConflict ?? dzipConflict?.ScriptConflicts.FirstOrDefault();

        if (dzipConflict is null || scriptConflict is null)
        {
            MessageBox.Show("Select a script conflict first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        OpenManualMergeEditor(dzipConflict, scriptConflict);
    }

    [RelayCommand]
    private void ViewDiff()
    {
        var dzipConflict = SelectedDzipConflict ?? DzipConflicts.FirstOrDefault();
        var scriptConflict = SelectedScriptConflict ?? dzipConflict?.ScriptConflicts.FirstOrDefault();

        if (dzipConflict is null || scriptConflict is null)
        {
            MessageBox.Show("Select a script conflict first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var diffWindow = new Views.DiffViewerWindow(dzipConflict, scriptConflict, DzipConflicts.ToList())
        {
            Owner = Application.Current.MainWindow
        };
        diffWindow.ShowDialog();
    }

    [RelayCommand]
    private void ViewMergeSummary()
    {
        var autoCount = DzipConflicts.Sum(c => c.ScriptConflicts.Count(s => s.Status == ConflictStatus.AutoResolved));
        var manualCount = DzipConflicts.Sum(c => c.ScriptConflicts.Count(s => s.Status == ConflictStatus.ManuallyResolved));
        ShowMergeSummary(autoCount, manualCount);
    }

    [RelayCommand]
    private async Task Unmerge()
    {
        try
        {
            _extractionService.DiscardMergedScripts();
            HasExistingMerge = false;
            HasPendingMergeChanges = false;
            Log("Cleared merged output. You can now generate a new merge.");
            StatusMessage = "Merge cleared";
            await DetectConflictsAsync();
        }
        catch (Exception ex)
        {
            Log($"Error clearing merged output: {ex.Message}");
        }
    }
}
