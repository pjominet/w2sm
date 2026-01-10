using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Models;
using W2ScriptMerger.Tools;

namespace W2ScriptMerger.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task AddMods()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Mod Archives",
            Filter = "Mod Archives (*.zip;*.7z;*.rar)|*.zip;*.7z;*.rar|All Files (*.*)|*.*",
            Multiselect = true,
            InitialDirectory = _configService.LastModDirectory
        };

        if (dialog.ShowDialog() is not true)
            return;

        IsBusy = true;
        StatusMessage = "Loading mods...";

        var targetDirectory = Path.GetDirectoryName(dialog.FileNames.FirstOrDefault());
        if (targetDirectory.HasValue())
            _configService.LastModDirectory = targetDirectory;

        foreach (var file in dialog.FileNames)
        {
            Log($"Loading: {Path.GetFileName(file)}");

            var archive = await Task.Run(() => _archiveService.LoadModArchive(file));

            if (archive.IsLoaded)
            {
                LoadedMods.Add(archive);
                OnPropertyChanged(nameof(FilteredMods));
                Log($"Mod staged: {archive.DisplayName}");
            }
            else
            {
                Log(archive.Error ?? "Failed to add mod: Unknown error");
            }
        }

        await UpdateLoadedModsList();
        await DetectConflictsAsync();
        HasPendingMergeChanges = DzipConflicts.Any(c => c.HasUnresolvedConflicts);

        IsBusy = false;
        StatusMessage = $"Loaded {LoadedMods.Count} mods, {DzipConflicts.Count} conflicts detected";
    }

    [RelayCommand]
    private async Task DeleteMod(ModArchive? mod)
    {
        if (mod is null)
            return;

        try
        {
            if (mod.IsDeployed)
            {
                _deploymentService.RemoveMod(mod);
                Log($"Undeployed: {mod.DisplayName}");
            }

            var modPath = Path.Combine(_configService.ModStagingPath, mod.ModName);
            DirectoryUtils.ClearDirectory(modPath);
            if (Directory.Exists(modPath))
                Directory.Delete(modPath);

            LoadedMods.Remove(mod);
            OnPropertyChanged(nameof(FilteredMods));
            await UpdateLoadedModsList();
            Log($"Removed: {mod.DisplayName}");

            await DetectConflictsAsync();
            HasPendingMergeChanges = true;

            if (HasPendingMergeChanges)
            {
                MessageBox.Show(
                    "Mod removed. You may need to regenerate the merge if this mod was part of a conflict.",
                    "Merge Update Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            Log($"Error removing mod: {ex.Message}");
        }
    }

    [RelayCommand]
    private void RemoveMod(ModArchive? mod)
    {
        if (mod is null || !mod.IsDeployed)
            return;

        try
        {
            _deploymentService.RemoveMod(mod);
            Log($"Undeployed: {mod.DisplayName}");
            StatusMessage = $"Undeployed {mod.DisplayName}";
        }
        catch (Exception ex)
        {
            Log($"Error undeploying mod: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PurgeMods()
    {
        var result = MessageBox.Show(
            "This will remove all staged mods and restore vanilla game files. Continue?",
            "Purge All",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result is not MessageBoxResult.Yes)
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Purging all mods...";

            _deploymentService.PurgeDeployedMods(LoadedMods);

            DirectoryUtils.ClearDirectory(_configService.ModStagingPath);
            _extractionService.CleanupExtractedFiles();

            LoadedMods.Clear();
            DzipConflicts.Clear();
            OnPropertyChanged(nameof(FilteredMods));

            await UpdateLoadedModsList();

            HasPendingMergeChanges = false;
            HasExistingMerge = false;
            Log("Purged all mods and restored vanilla game");
            StatusMessage = "Ready";
        }
        catch (Exception ex)
        {
            Log($"Error purging mods: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
