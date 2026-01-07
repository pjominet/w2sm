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
    private readonly GameFileService _gameFileService;
    private readonly MergeService _mergeService;
    private readonly InstallService _installService;
    private readonly LoggingService _loggingService;

    private static string ModsListPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "loadedMods.json");
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    [ObservableProperty] private string _gamePath = string.Empty;

    [ObservableProperty] private string _modStagingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modStaging");

    [ObservableProperty] private string _userContentPath = string.Empty;

    [ObservableProperty] private bool _isGamePathValid;

    [ObservableProperty] private string _statusMessage = "Ready";

    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private InstallLocation _selectedInstallLocation = InstallLocation.UserContent;

    [ObservableProperty] private ModConflict? _selectedConflict;

    [ObservableProperty] private string _diffViewText = string.Empty;

    [ObservableProperty] private string _logText = string.Empty;

    public ObservableCollection<ModArchive> LoadedMods { get; } = [];
    public ObservableCollection<ModConflict> Conflicts { get; } = [];
    private ObservableCollection<string> LogMessages { get; } = [];

    public MainViewModel()
    {
        _loggingService = new LoggingService();
        _configService = new ConfigService(_jsonSerializerOptions);
        _gameFileService = new GameFileService(_configService);
        _archiveService = new ArchiveService(_configService);
        _installService = new InstallService(_configService);
        _mergeService = new MergeService(_gameFileService);

        GamePath = _configService.GamePath ?? string.Empty;
        ModStagingPath = _configService.ModStagingPath;
        UserContentPath = _configService.UserContentPath;
        IsGamePathValid = _configService.IsGamePathValid();
        SelectedInstallLocation = _configService.DefaultInstallLocation;

        LogMessages.CollectionChanged += (_, _) => UpdateLogText();

        if (!IsGamePathValid)
            return;

        Log("Game path validated. Building vanilla script index...");
        Task.Run(async () =>
        {
            await BuildGameScriptIndex();
            await DetectStagedMods();
        });
    }

    #region Private Helpers

    partial void OnSelectedInstallLocationChanged(InstallLocation value) => _configService.DefaultInstallLocation = value;

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
        LogMessages.Add(formatted);
        _loggingService.Log(message);
    }

    private async Task BuildGameScriptIndex()
    {
        await Task.Run(async () =>
        {
            _gameFileService.BuildDzipIndex();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Log($"Indexed {_gameFileService.GetDzipIndexCount()} vanilla script archives");
                StatusMessage = "Ready - Vanilla script archives indexed";
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
        if (mods is not null)
        {
            Log($"Detected {mods.Count} staged mods");
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                LoadedMods.Clear();
                foreach (var mod in mods)
                    LoadedMods.Add(mod);

                await DetectConflictsAsync();
            });
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

    #endregion

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
            await BuildGameScriptIndex();
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
                    foreach (var modFile in archive.Files.Where(f => f.Type is ModFileType.Dzip))
                        _gameFileService.AddDzip(modFile.Name);

                    Log($"Mod {archive.ModName} staged");
                }
                else Log(archive.Error ?? "Failed to add mod: Unknown error");
            }

            await UpdateLoadedModsList();
            await DetectConflictsAsync();

            IsBusy = false;
            StatusMessage = $"Loaded {LoadedMods.Count} mods, {Conflicts.Count} conflicts detected";
        }
    }

    [RelayCommand]
    private async Task RemoveMod(ModArchive? mod)
    {
        if (mod is null)
            return;
        try
        {
            // Remove mod directory and all its contents
            var modPath = Path.Combine(_configService.ModStagingPath, mod.ModName);
            DirectoryUtils.ClearDirectory(modPath);
            Directory.Delete(modPath);
            LoadedMods.Remove(mod);
            await UpdateLoadedModsList();
            Log($"Removed: {mod.ModName}");

            // Re-detect conflicts
            await Task.Run(async () => { await Application.Current.Dispatcher.InvokeAsync(async () => await DetectConflictsAsync()); });

        }
        catch (Exception ex)
        {
            Log($"Error removing mod: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ClearMods()
    {
        try
        {
            DirectoryUtils.ClearDirectory(_configService.ModStagingPath);
            LoadedMods.Clear();
            Conflicts.Clear();
            await UpdateLoadedModsList();
            Log("Purged all mods");
            StatusMessage = "Ready";
        }
        catch (Exception ex)
        {
            Log($"Error purging mods: {ex.Message}");
        }
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
        StatusMessage = "Attempting Auto-merge...";

        var autoMerged = 0;
        var failed = 0;

        foreach (var conflict in Conflicts)
        {
            if (conflict.Status is not ConflictStatus.Unresolved)
                continue;

            await Task.Run(() => _mergeService.AttemptAutoMerge(conflict));
            if (conflict.Status is ConflictStatus.AutoResolved)
            {
                autoMerged++;
                Log($"Auto-merged: {conflict.OriginalFileName}");
            }
            else
            {
                failed++;
                var conflictFiles = string.Join(", ", conflict.ConflictingFiles
                    .Select(Path.GetFileName));
                Log($"Needs manual merge: {conflictFiles} >> {conflict.OriginalFileName}");
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
            sb.AppendLine(Encoding.GetEncoding(1250).GetString(SelectedConflict.VanillaContent));
            sb.AppendLine();
        }

        foreach (var mod in SelectedConflict.ModVersions)
        {
            sb.AppendLine($"--- MOD: {mod.SourceArchive} ---");
            sb.AppendLine(Encoding.GetEncoding(1250).GetString(mod.Content));
            sb.AppendLine();
        }

        if (SelectedConflict.MergeContent is not null)
        {
            sb.AppendLine("--- MERGED RESULT ---");
            sb.AppendLine(Encoding.GetEncoding(1250).GetString(SelectedConflict.MergeContent));
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

        SelectedConflict.MergeContent = mergeWindow.MergedContent;
        SelectedConflict.Status = ConflictStatus.ManuallyResolved;
        Log($"Manually merged: {SelectedConflict.OriginalFileName}");
        StatusMessage = $"Merged: {SelectedConflict.OriginalFileName}";

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

        var pendingConflicts = Conflicts.Where(c => c.Status == ConflictStatus.Unresolved).ToList();
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
                var installLocation = mod.ModInstallLocation;
                if (installLocation == InstallLocation.Unknown)
                {
                    var dialog = new InstallLocationDialog(mod.ModName)
                    {
                        Owner = Application.Current.MainWindow
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        installLocation = dialog.SelectedLocation;
                    }
                    else
                    {
                        Log($"Skipped installing: {mod.ModName}");
                        continue;
                    }
                }

                await Task.Run(() =>
                    _installService.InstallNonConflictingFiles(mod, installLocation, conflictPaths));
                Log($"Installed non-conflicting files from: {mod.ModName}");
            }

            // Install merged files
            var mergedCount = 0;
            foreach (var conflict in Conflicts.Where(c => c.MergeContent is not null))
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
}
