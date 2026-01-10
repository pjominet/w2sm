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
    private void BrowseModStagingPath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select your Witcher 2 Mod Staging Folder"
        };

        if (dialog.ShowDialog() != true)
            return;

        ModStagingPath = dialog.FolderName;
        _configService.ModStagingPath = ModStagingPath;
        Log($"Mod staging path set: {ModStagingPath}");
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
    private void OpenModStagingFolder()
    {
        if (!string.IsNullOrEmpty(ModStagingPath) && Directory.Exists(ModStagingPath))
            System.Diagnostics.Process.Start("explorer.exe", ModStagingPath);
    }
}
