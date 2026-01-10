using System.Windows;
using CommunityToolkit.Mvvm.Input;
using W2ScriptMerger.Models;
using W2ScriptMerger.Views;

namespace W2ScriptMerger.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task DeployMods()
    {
        if (!IsGamePathValid)
        {
            StatusMessage = "Please set a valid game path first";
            return;
        }

        var unresolvedCount = DzipConflicts.Sum(c => c.ScriptConflicts.Count(s =>
            s.Status is ConflictStatus.Unresolved or ConflictStatus.NeedsManualResolution));

        if (unresolvedCount > 0)
        {
            var result = MessageBox.Show(
                $"There are {unresolvedCount} unresolved script conflicts. Deploy anyway?\n\nNote: Unmerged scripts will use vanilla versions.",
                "Unresolved Conflicts",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result is not MessageBoxResult.Yes)
                return;
        }

        IsBusy = true;
        StatusMessage = "Deploying mods...";

        try
        {
            var mergedDzipNames = new HashSet<string>(
                DzipConflicts.Where(c => c.IsFullyMerged).Select(c => c.DzipName),
                StringComparer.OrdinalIgnoreCase);

            await Task.Run(() => _deploymentService.DeployMergedDzips(DzipConflicts.ToList()));
            Log("Deployed merged scripts");

            foreach (var mod in LoadedMods.Where(m => !m.IsDeployed))
            {
                if (mod.ModInstallLocation == InstallLocation.Unknown)
                {
                    if (PromptForUnknownInstallLocation)
                    {
                        var dialog = new InstallLocationDialog(mod.DisplayName)
                        {
                            Owner = Application.Current.MainWindow
                        };

                        if (dialog.ShowDialog() == true)
                            mod.ModInstallLocation = dialog.SelectedLocation;
                        else
                        {
                            Log($"Skipped: {mod.DisplayName}");
                            continue;
                        }
                    }
                    else
                    {
                        mod.ModInstallLocation = InstallLocation.CookedPC;
                    }
                }

                await Task.Run(() => _deploymentService.DeployMod(mod, mergedDzipNames));
                Log($"Deployed: {mod.DisplayName}");
            }

            await UpdateLoadedModsList();
            StatusMessage = $"Deployed {LoadedMods.Count(m => m.IsDeployed)} mods";
        }
        catch (Exception ex)
        {
            Log($"Error during deployment: {ex.Message}");
            StatusMessage = "Deployment failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreVanilla()
    {
        var result = MessageBox.Show(
            "This will restore all vanilla game files (remove deployed mods) but keep mods staged. Continue?",
            "Restore Vanilla",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result is not MessageBoxResult.Yes)
            return;

        IsBusy = true;
        StatusMessage = "Restoring vanilla game...";

        try
        {
            await Task.Run(() => _deploymentService.RestoreAllBackups());

            foreach (var mod in LoadedMods)
                mod.IsDeployed = false;

            await UpdateLoadedModsList();
            Log("Restored vanilla game files");
            StatusMessage = "Vanilla game restored";
        }
        catch (Exception ex)
        {
            Log($"Error restoring vanilla: {ex.Message}");
            StatusMessage = "Restore failed";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
