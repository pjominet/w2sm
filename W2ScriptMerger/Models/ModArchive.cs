using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace W2ScriptMerger.Models;

public partial class ModArchive : INotifyPropertyChanged
{
    private static readonly Regex NexusIdPattern = NexusIdRegex();

    public string SourcePath { get; init; } = string.Empty;

    public string ModName => Path.GetFileNameWithoutExtension(SourcePath);

    public string DisplayName => GetDisplayName();

    public string? NexusId => GetNexusId();

    public List<ModFile> Files { get; } = [];

    public InstallLocation ModInstallLocation { get; set; } = InstallLocation.CookedPC;

    public string StagingPath { get; set; } = string.Empty;

    public bool IsDeployed
    {
        get;
        set => SetField(ref field, value);
    }

    [JsonIgnore]
    public bool IsLoaded { get; set; }

    [JsonIgnore]
    public string? Error { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private string GetDisplayName()
    {
        var name = ModName;
        var match = NexusIdPattern.Match(name);
        return match.Success ? name[..match.Index].TrimEnd('-', '_', ' ') : name;
    }

    private string? GetNexusId()
    {
        var match = NexusIdPattern.Match(ModName);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"-\d+(-[\d\w]+)*$", RegexOptions.Compiled)]
    private static partial Regex NexusIdRegex();
}
