using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using W2ScriptMerger.Models;
using W2ScriptMerger.Services;
using W2ScriptMerger.Tools;

namespace W2ScriptMerger.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly ArchiveService _archiveService;
    private readonly IndexerService _indexerService;
    private readonly ScriptExtractionService _extractionService;
    private readonly ConflictDetectionService _conflictDetectionService;
    private readonly ScriptMergeService _mergeService;
    private readonly DeploymentService _deploymentService;
    private readonly LoggingService _loggingService;

    private static string ModsListPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.STAGING_LIST_FILENAME);

    private static string AppVersion => Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";

    public string WindowTitle => $"Witcher 2 Mod Manager v{AppVersion}";

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    [ObservableProperty] private string _gamePath = string.Empty;
    [ObservableProperty] private string _runtimeDataPath = string.Empty;
    [ObservableProperty] private string _userContentPath = string.Empty;
    [ObservableProperty] private bool _isGamePathValid;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private DzipConflict? _selectedDzipConflict;
    [ObservableProperty] private ScriptFileConflict? _selectedScriptConflict;
    [ObservableProperty] private string _diffViewText = string.Empty;
    [ObservableProperty] private string _logText = string.Empty;
    [ObservableProperty] private bool _hasPendingMergeChanges;
    [ObservableProperty] private bool _hasExistingMerge;
    [ObservableProperty] private string _modSearchFilter = string.Empty;
    [ObservableProperty] private bool _promptForUnknownInstallLocation;

    private ObservableCollection<ModArchive> LoadedMods { get; } = [];
    public ObservableCollection<DzipConflict> DzipConflicts { get; } = [];

    public IEnumerable<ModArchive> FilteredMods => string.IsNullOrWhiteSpace(ModSearchFilter)
        ? LoadedMods.OrderBy(m => m.DisplayName)
        : LoadedMods.Where(m => m.DisplayName.Contains(ModSearchFilter, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(m => m.DisplayName);

    public bool HasUnresolvedConflicts => DzipConflicts.Any(c => c.ScriptConflicts.Any(s =>
        s.Status is ConflictStatus.Unresolved or ConflictStatus.NeedsManualResolution));

    partial void OnModSearchFilterChanged(string value) => OnPropertyChanged(nameof(FilteredMods));

    partial void OnPromptForUnknownInstallLocationChanged(bool value) => _configService.PromptForUnknownInstallLocation = value;

    private ObservableCollection<string> LogMessages { get; } = [];

    public MainViewModel()
    {
        _loggingService = new LoggingService();
        _configService = new ConfigService(_jsonSerializerOptions);
        _indexerService = new IndexerService(_configService);
        _extractionService = new ScriptExtractionService(_configService, _indexerService);
        _conflictDetectionService = new ConflictDetectionService(_extractionService);
        _mergeService = new ScriptMergeService(_extractionService);
        _archiveService = new ArchiveService(_configService);
        _deploymentService = new DeploymentService(_configService, _extractionService);

        GamePath = _configService.GamePath ?? string.Empty;
        RuntimeDataPath = _configService.RuntimeDataPath;
        UserContentPath = _configService.UserContentPath;
        IsGamePathValid = _configService.IsGamePathValid();
        PromptForUnknownInstallLocation = _configService.PromptForUnknownInstallLocation;

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
}
