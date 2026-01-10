using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Models;
using W2ScriptMerger.Services;
using W2ScriptMerger.Tools;
using W2ScriptMerger.Views;

namespace W2ScriptMerger.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly ArchiveService _archiveService;
    private readonly ScriptExtractionService _extractionService;
    private readonly ConflictDetectionService _conflictDetectionService;
    private readonly ScriptMergeService _mergeService;
    private readonly DeploymentService _deploymentService;
    private readonly LoggingService _loggingService;

    private static string ModsListPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "loadedMods.json");

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    [ObservableProperty] private string _gamePath = string.Empty;
    [ObservableProperty] private string _modStagingPath = string.Empty;
    [ObservableProperty] private string _userContentPath = string.Empty;
    [ObservableProperty] private bool _isGamePathValid;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private DzipConflict? _selectedDzipConflict;
    [ObservableProperty] private ScriptFileConflict? _selectedScriptConflict;
    [ObservableProperty] private string _diffViewText = string.Empty;
    [ObservableProperty] private string _logText = string.Empty;
    [ObservableProperty] private bool _hasPendingMergeChanges;
    [ObservableProperty] private string _modSearchFilter = string.Empty;

    public ObservableCollection<ModArchive> LoadedMods { get; } = [];
    public ObservableCollection<DzipConflict> DzipConflicts { get; } = [];
    
    public IEnumerable<ModArchive> FilteredMods => string.IsNullOrWhiteSpace(ModSearchFilter)
        ? LoadedMods.OrderBy(m => m.DisplayName)
        : LoadedMods.Where(m => m.DisplayName.Contains(ModSearchFilter, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(m => m.DisplayName);

    partial void OnModSearchFilterChanged(string value) => OnPropertyChanged(nameof(FilteredMods));
    private ObservableCollection<string> LogMessages { get; } = [];

    public MainViewModel()
    {
        _loggingService = new LoggingService();
        _configService = new ConfigService(_jsonSerializerOptions);
        _extractionService = new ScriptExtractionService(_configService);
        _conflictDetectionService = new ConflictDetectionService(_extractionService);
        _mergeService = new ScriptMergeService(_extractionService);
        _archiveService = new ArchiveService(_configService);
        _deploymentService = new DeploymentService(_configService, _extractionService);

        GamePath = _configService.GamePath ?? string.Empty;
        ModStagingPath = _configService.ModStagingPath;
        UserContentPath = _configService.UserContentPath;
        IsGamePathValid = _configService.IsGamePathValid();

        LogMessages.CollectionChanged += (_, _) => UpdateLogText();

        if (!IsGamePathValid)
            return;

        Log("Game path validated. Extracting vanilla scripts...");
        Task.Run(async () =>
        {
            await ExtractVanillaScripts();
            await DetectStagedMods();
        });
    }

    #region Private Helpers

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
            _extractionService.ExtractVanillaScripts();
            Application.Current.Dispatcher.Invoke(() =>
            {
                Log($"Indexed {_extractionService.VanillaDzipCount} vanilla script archives");
                StatusMessage = "Ready - Vanilla scripts indexed";
            });
        });
    }

    private async Task DetectStagedMods()
    {
        Log("Detecting staged mods...");
        if (!File.Exists(ModsListPath))
            return;

        var json = await File.ReadAllTextAsync(ModsListPath);
        var mods = JsonSerializer.Deserialize<List<ModArchive>>(json);
        if (mods is null)
            return;

        var validMods = mods.Where(m =>
            !string.IsNullOrEmpty(m.StagingPath) &&
            Directory.Exists(m.StagingPath)).ToList();

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

        foreach (var conflict in conflicts)
            DzipConflicts.Add(conflict);

        var totalScripts = conflicts.Sum(c => c.ScriptConflicts.Count);
        Log($"Detected {DzipConflicts.Count} dzip conflicts ({totalScripts} scripts)");
    }

    #endregion

    #region Commands

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

    [RelayCommand]
    private async Task RemoveMod(ModArchive? mod)
    {
        if (mod is null)
            return;

        try
        {
            if (mod.IsDeployed)
            {
                _deploymentService.UndeployMod(mod);
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
    private async Task ClearMods()
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
            await UpdateLoadedModsList();

            HasPendingMergeChanges = false;
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
            StatusMessage = $"Merge complete - {mergeResult.AutoMergedCount} scripts merged";
            Log("All conflicts resolved automatically");
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
        }
        else
        {
            var remaining = DzipConflicts.Sum(c => c.ScriptConflicts.Count(s => s.Status is ConflictStatus.NeedsManualResolution));
            StatusMessage = $"{remaining} scripts still need manual resolution";
        }

        RefreshConflictsList();
    }

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
                var installLocation = mod.ModInstallLocation;
                if (installLocation == InstallLocation.Unknown)
                {
                    var dialog = new InstallLocationDialog(mod.DisplayName)
                    {
                        Owner = Application.Current.MainWindow
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        installLocation = dialog.SelectedLocation;
                        mod.ModInstallLocation = installLocation;
                    }
                    else
                    {
                        Log($"Skipped deploying: {mod.DisplayName}");
                        continue;
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

    private void RefreshConflictsList()
    {
        var conflicts = DzipConflicts.ToList();
        DzipConflicts.Clear();
        foreach (var c in conflicts)
            DzipConflicts.Add(c);
    }

    #endregion
}
