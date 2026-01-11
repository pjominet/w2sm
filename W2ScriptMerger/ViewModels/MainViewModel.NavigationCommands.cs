using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using W2ScriptMerger.Models;
using W2ScriptMerger.Views;

namespace W2ScriptMerger.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task OpenSettings()
    {
        var dialog = new SettingsDialog(GamePath, RuntimeDataPath)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
            return;

        // Handle game path change
        if (dialog.GamePathChanged)
        {
            GamePath = dialog.GamePath;
            _configService.GamePath = GamePath;
            IsGamePathValid = _configService.IsGamePathValid();

            if (IsGamePathValid)
            {
                Log($"Game path set: {GamePath}");
                UserContentPath = _configService.UserContentPath;
                await ExtractVanillaScripts();
                await DetectStagedMods();
            }
            else
            {
                Log("Warning: Selected path does not appear to be a valid Witcher 2 installation");
                StatusMessage = "Invalid game path";
            }
        }

        // Handle data path change
        if (dialog.DataPathChanged)
        {
            var newPath = dialog.DataPath;
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
