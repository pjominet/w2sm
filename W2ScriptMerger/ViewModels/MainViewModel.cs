using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using W2ScriptMerger.Models;
using W2ScriptMerger.Services;
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

    [ObservableProperty]
    private string _gamePath = string.Empty;

    [ObservableProperty]
    private string _userContentPath = string.Empty;

    [ObservableProperty]
    private bool _isGamePathValid;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private InstallLocation _selectedInstallLocation = InstallLocation.UserContent;

    [ObservableProperty]
    private ScriptConflict? _selectedConflict;

    [ObservableProperty]
    private string _diffViewText = string.Empty;

    public ObservableCollection<ModArchive> LoadedMods { get; } = new();
    public ObservableCollection<ScriptConflict> Conflicts { get; } = new();
    public ObservableCollection<string> LogMessages { get; } = new();

    public MainViewModel()
    {
        _configService = new ConfigService();
        _loggingService = new LoggingService();
        _archiveService = new ArchiveService();
        var dzipService = new DzipService();
        _gameFileService = new GameFileService(_configService, dzipService);
        _mergeService = new MergeService();
        _installService = new InstallService(_configService, dzipService);

        GamePath = _configService.GamePath ?? string.Empty;
        UserContentPath = _configService.UserContentPath;
        IsGamePathValid = _configService.IsGamePathValid();
        SelectedInstallLocation = _configService.DefaultInstallLocation;

        if (!IsGamePathValid)
            return;

        Log("Game path validated. Building vanilla script index...");
        Task.Run(() =>
        {
            _gameFileService.BuildVanillaIndex();
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
            Title = "Select Witcher 2 Installation Folder"
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
                _gameFileService.BuildVanillaIndex();
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

        if (dialog.ShowDialog() == true)
        {
            IsBusy = true;
            StatusMessage = "Loading mods...";

            var dir = Path.GetDirectoryName(dialog.FileNames.FirstOrDefault());
            if (!string.IsNullOrEmpty(dir))
                _configService.LastModDirectory = dir;

            foreach (var file in dialog.FileNames)
            {
                Log($"Loading: {Path.GetFileName(file)}");
                
                var archive = await Task.Run(() => ArchiveService.LoadModArchive(file));
                
                if (archive.IsLoaded)
                {
                    LoadedMods.Add(archive);
                    _configService.AddRecentMod(file);
                    
                    var scriptCount = archive.Files.Count(f => f.FileType == ModFileType.Script);
                    var dzipCount = archive.Files.Count(f => f.FileType == ModFileType.Dzip);
                    Log($"  Loaded: {archive.Files.Count} files ({scriptCount} scripts, {dzipCount} dzip archives)");
                }
                else
                {
                    Log($"  Error: {archive.Error}");
                }
            }

            await DetectConflictsAsync();
            
            IsBusy = false;
            StatusMessage = $"Loaded {LoadedMods.Count} mods, {Conflicts.Count} conflicts detected";
        }
    }

    [RelayCommand]
    private void RemoveMod(ModArchive? mod)
    {
        if (mod is null) return;
        
        LoadedMods.Remove(mod);
        Log($"Removed: {mod.FileName}");
        
        // Re-detect conflicts
        Task.Run(async () =>
        {
            await Application.Current.Dispatcher.InvokeAsync(async () => await DetectConflictsAsync());
        });
    }

    [RelayCommand]
    private void ClearMods()
    {
        LoadedMods.Clear();
        Conflicts.Clear();
        Log("Cleared all mods");
        StatusMessage = "Ready";
    }

    private async Task DetectConflictsAsync()
    {
        Conflicts.Clear();
        
        if (LoadedMods.Count == 0)
            return;

        var conflicts = await Task.Run(() => 
            MergeService.DetectConflicts(LoadedMods.ToList(), _gameFileService));

        foreach (var conflict in conflicts)
        {
            Conflicts.Add(conflict);
        }

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
                Log($"Installed non-conflicting files from: {mod.FileName}");
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
