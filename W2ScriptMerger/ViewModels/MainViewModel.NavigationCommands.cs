using System.IO;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task BrowseGamePath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select your Witcher 2 Installation Folder"
        };

        if (dialog.ShowDialog() != true)
            return;

        GamePath = dialog.FolderName;
        _configService.GamePath = GamePath;
        IsGamePathValid = _configService.IsGamePathValid();

        if (IsGamePathValid)
        {
            Log($"Game path set: {GamePath}");
            UserContentPath = _configService.UserContentPath;
            await ExtractVanillaScripts();
        }
        else
        {
            Log("Warning: Selected path does not appear to be a valid Witcher 2 installation");
            StatusMessage = "Invalid game path";
        }
    }

    [RelayCommand]
    private async Task BrowseRuntimeDataPath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select folder for data (mods, scripts, etc.)"
        };

        if (dialog.ShowDialog() != true)
            return;

        var newPath = dialog.FolderName;
        var oldPath = RuntimeDataPath;

        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Migrating data...";

            await Task.Run(() => _configService.MigrateRuntimeData(newPath));

            RuntimeDataPath = _configService.RuntimeDataPath;
            Log($"Data path changed: {RuntimeDataPath}");
            StatusMessage = "Data migrated successfully";
        }
        catch (Exception ex)
        {
            Log($"Error migrating data: {ex.Message}");
            StatusMessage = "Migration failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenModFolder(ModArchive? mod)
    {
        if (mod is null || string.IsNullOrEmpty(mod.StagingPath))
            return;

        if (Directory.Exists(mod.StagingPath))
            System.Diagnostics.Process.Start("explorer.exe", mod.StagingPath);
    }

    [RelayCommand]
    private void OpenGameFolder()
    {
        if (!string.IsNullOrEmpty(GamePath) && Directory.Exists(GamePath))
            System.Diagnostics.Process.Start("explorer.exe", GamePath);
    }

    [RelayCommand]
    private void OpenRuntimeDataFolder()
    {
        if (!string.IsNullOrEmpty(RuntimeDataPath) && Directory.Exists(RuntimeDataPath))
            System.Diagnostics.Process.Start("explorer.exe", RuntimeDataPath);
    }
}
