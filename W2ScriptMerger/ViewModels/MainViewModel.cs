using System.Collections.ObjectModel;
using System.IO;
using System.Text;
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
    private readonly GameFileService _gameFileService;
    private readonly MergeService _mergeService;
    private readonly InstallService _installService;
    private readonly LoggingService _loggingService;

    [ObservableProperty] private string _gamePath = string.Empty;

    [ObservableProperty] private string _modStagingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modStaging");

    [ObservableProperty] private string _userContentPath = string.Empty;

    [ObservableProperty] private bool _isGamePathValid;

    [ObservableProperty] private string _statusMessage = "Ready";

    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private InstallLocation _selectedInstallLocation = InstallLocation.UserContent;

    [ObservableProperty] private ScriptConflict? _selectedConflict;

    [ObservableProperty] private string _diffViewText = string.Empty;

    public ObservableCollection<ModArchive> LoadedMods { get; } = [];
    public ObservableCollection<ScriptConflict> Conflicts { get; } = [];
    public ObservableCollection<string> LogMessages { get; } = [];
    public InstallLocation[] InstallLocations { get; } = Enum.GetValues<InstallLocation>();

    public MainViewModel()
    {
        _configService = new ConfigService();
        _loggingService = new LoggingService();
        _archiveService = new ArchiveService(_configService);
        _gameFileService = new GameFileService(_configService);
        _installService = new InstallService(_configService);
        _mergeService = new MergeService(_gameFileService);

        GamePath = _configService.GamePath ?? string.Empty;
        ModStagingPath = _configService.ModStagingPath;
        UserContentPath = _configService.UserContentPath;
        IsGamePathValid = _configService.IsGamePathValid();
        SelectedInstallLocation = _configService.DefaultInstallLocation;

        if (!IsGamePathValid)
            return;

        Log("Game path validated. Building vanilla script index...");
        Task.Run(() =>
        {
            _gameFileService.BuildGameScriptIndex();
            Application.Current.Dispatcher.Invoke(() =>
            {
                Log($"Indexed {_gameFileService.GetAllVanillaScriptPaths().Count} vanilla scripts");
                StatusMessage = "Ready - Vanilla scripts indexed";
            });
        });
    }

    [RelayCommand]
    private void BrowseGamePath()
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

            Task.Run(() =>
            {
                _gameFileService.BuildGameScriptIndex();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Log($"Indexed {_gameFileService.GetAllVanillaScriptPaths().Count} vanilla scripts");
                    StatusMessage = "Ready - Vanilla scripts indexed";
                });
            });
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
        Log($"Mod staging path set: {UserContentPath}");
    }

    [RelayCommand]
    private void BrowseUserContent()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select UserContent Folder"
        };

        if (dialog.ShowDialog() != true)
            return;

        UserContentPath = dialog.FolderName;
        _configService.UserContentPath = UserContentPath;
        Log($"UserContent path set: {UserContentPath}");
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

        if (dialog.ShowDialog() is true)
        {
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
                    Log($"Mod {archive.ModName} staged");
                }
                else Log($"Error: {archive.Error}");
            }

            await DetectConflictsAsync();

            IsBusy = false;
            StatusMessage = $"Loaded {LoadedMods.Count} mods, {Conflicts.Count} conflicts detected";
        }
    }

    [RelayCommand]
    private void RemoveMod(ModArchive? mod)
    {
        if (mod is null)
            return;
        try
        {
            Directory.Delete(Path.Combine(_configService.ModStagingPath, mod.ModName));
            LoadedMods.Remove(mod);
            Log($"Removed: {mod.ModName}");

            // Re-detect conflicts
            Task.Run(async () => { await Application.Current.Dispatcher.InvokeAsync(async () => await DetectConflictsAsync()); });
        }
        catch (Exception ex)
        {
            Log($"Error removing mod: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ClearMods()
    {
        try
        {
            DirectoryUtils.ClearDirectory(_configService.ModStagingPath);
            LoadedMods.Clear();
            Conflicts.Clear();
            Log("Purged all mods");
            StatusMessage = "Ready";
        }
        catch (Exception ex)
        {
            Log($"Error purging mods: {ex.Message}");
        }
    }

    private async Task DetectConflictsAsync()
    {
        Conflicts.Clear();

        if (LoadedMods.Count == 0)
            return;

        var conflicts = await Task.Run(() => _mergeService.DetectConflicts(LoadedMods.ToList()));

        foreach (var conflict in conflicts)
            Conflicts.Add(conflict);

        Log($"Detected {Conflicts.Count} potential conflicts");
    }

    [RelayCommand]
    private async Task AutoMergeAll()
    {
        if (Conflicts.Count == 0)
        {
            StatusMessage = "No conflicts to merge";
            return;
        }

        IsBusy = true;
        StatusMessage = "Auto-merging...";

        var autoMerged = 0;
        var failed = 0;

        foreach (var conflict in Conflicts)
        {
            if (conflict.Status is not ConflictStatus.Pending)
                continue;

            var success = await Task.Run(() => _mergeService.TryAutoMerge(conflict));
            if (success)
            {
                autoMerged++;
                Log($"Auto-merged: {conflict.FileName}");
            }
            else
            {
                failed++;
                Log($"Needs manual merge: {conflict.FileName}");
            }
        }

        IsBusy = false;
        StatusMessage = $"Auto-merged {autoMerged} files, {failed} need manual intervention";
    }

    [RelayCommand]
    private void ViewConflictDiff()
    {
        if (SelectedConflict is null)
            return;

        var sb = new StringBuilder();
        sb.AppendLine($"=== Conflict: {SelectedConflict.RelativePath} ===");
        sb.AppendLine();

        if (SelectedConflict.VanillaContent is not null)
        {
            sb.AppendLine("--- VANILLA VERSION ---");
            sb.AppendLine(Encoding.UTF8.GetString(SelectedConflict.VanillaContent));
            sb.AppendLine();
        }

        foreach (var mod in SelectedConflict.ModVersions)
        {
            sb.AppendLine($"--- MOD: {mod.SourceArchive} ---");
            sb.AppendLine(Encoding.UTF8.GetString(mod.Content));
            sb.AppendLine();
        }

        if (SelectedConflict.MergedContent is not null)
        {
            sb.AppendLine("--- MERGED RESULT ---");
            sb.AppendLine(Encoding.UTF8.GetString(SelectedConflict.MergedContent));
        }

        DiffViewText = sb.ToString();
    }

    [RelayCommand]
    private void OpenMergeEditor()
    {
        if (SelectedConflict is null)
            return;

        var mergeWindow = new DiffMergeWindow(SelectedConflict)
        {
            Owner = Application.Current.MainWindow
        };

        if (mergeWindow.ShowDialog() != true || !mergeWindow.MergeAccepted)
            return;

        SelectedConflict.MergedContent = mergeWindow.MergedContent;
        SelectedConflict.Status = ConflictStatus.ManuallyMerged;
        Log($"Manually merged: {SelectedConflict.FileName}");
        StatusMessage = $"Merged: {SelectedConflict.FileName}";

        // Refresh the conflict list to update status indicators
        var index = Conflicts.IndexOf(SelectedConflict);
        if (index < 0)
            return;

        var conflict = SelectedConflict;
        Conflicts.RemoveAt(index);
        Conflicts.Insert(index, conflict);
        SelectedConflict = conflict;
    }

    [RelayCommand]
    private async Task InstallMergedFiles()
    {
        if (!IsGamePathValid)
        {
            StatusMessage = "Please set a valid game path first";
            return;
        }

        var pendingConflicts = Conflicts.Where(c => c.Status == ConflictStatus.Pending).ToList();
        if (pendingConflicts.Count != 0)
        {
            var result = MessageBox.Show(
                $"There are {pendingConflicts.Count} unresolved conflicts. Continue anyway?",
                "Unresolved Conflicts",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result is not MessageBoxResult.Yes)
                return;
        }

        IsBusy = true;
        StatusMessage = "Installing files...";

        try
        {
            var conflictPaths = new HashSet<string>(
                Conflicts.Select(c => c.RelativePath.Replace('\\', '/').ToLowerInvariant()));

            // Install non-conflicting files from each mod
            foreach (var mod in LoadedMods)
            {
                await Task.Run(() =>
                    _installService.InstallNonConflictingFiles(mod, SelectedInstallLocation, conflictPaths));
                Log($"Installed non-conflicting files from: {mod.ModName}");
            }

            // Install merged files
            var mergedCount = 0;
            foreach (var conflict in Conflicts.Where(c => c.MergedContent is not null))
            {
                await Task.Run(() =>
                    _installService.InstallMergedScript(conflict, SelectedInstallLocation));
                mergedCount++;
            }

            Log($"Installed {mergedCount} merged scripts");
            StatusMessage = $"Installation complete - {mergedCount} merged files installed";
        }
        catch (Exception ex)
        {
            Log($"Error during installation: {ex.Message}");
            StatusMessage = "Installation failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedInstallLocationChanged(InstallLocation value) => _configService.DefaultInstallLocation = value;

    private void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var formatted = $"[{timestamp}] {message}";
        LogMessages.Add(formatted);
        _loggingService.Log(message);
    }
}
