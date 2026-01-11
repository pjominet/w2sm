using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace W2ScriptMerger.Views;

public partial class SettingsDialog : Window
{
    public string GamePath { get; private set; }
    public string DataPath { get; private set; }
    public bool GamePathChanged { get; private set; }
    public bool DataPathChanged { get; private set; }
    private bool IsGamePathValid { get; set; }

    private readonly string _originalGamePath;
    private readonly string _originalDataPath;

    public SettingsDialog(string gamePath, string dataPath)
    {
        InitializeComponent();

        _originalGamePath = gamePath;
        _originalDataPath = dataPath;

        GamePath = gamePath;
        DataPath = dataPath;

        GamePathTextBox.Text = gamePath;
        DataPathTextBox.Text = dataPath;

        ValidateGamePath();
    }

    private void ValidateGamePath()
    {
        if (string.IsNullOrEmpty(GamePath))
        {
            IsGamePathValid = false;
            GamePathValidationText.Text = "Game path is required";
            return;
        }

        var witcher2Exe = Path.Combine(GamePath, "bin", "witcher2.exe");
        var cookedPc = Path.Combine(GamePath, "CookedPC");

        IsGamePathValid = File.Exists(witcher2Exe) && Directory.Exists(cookedPc);

        GamePathValidationText.Text = IsGamePathValid
            ? string.Empty
            : "⚠ Invalid path: witcher2.exe or CookedPC not found";
    }

    private void BrowseGamePath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select your Witcher 2 Installation Folder"
        };

        if (dialog.ShowDialog() != true)
            return;

        GamePath = dialog.FolderName;
        GamePathTextBox.Text = GamePath;
        ValidateGamePath();
    }

    private void OpenGameFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(GamePath) && Directory.Exists(GamePath))
            System.Diagnostics.Process.Start("explorer.exe", GamePath);
    }

    private void BrowseDataPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select folder for data (mods, scripts, etc.)"
        };

        if (dialog.ShowDialog() != true)
            return;

        DataPath = dialog.FolderName;
        DataPathTextBox.Text = DataPath;
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(DataPath) && Directory.Exists(DataPath))
            System.Diagnostics.Process.Start("explorer.exe", DataPath);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        GamePathChanged = !string.Equals(GamePath, _originalGamePath, StringComparison.OrdinalIgnoreCase);
        DataPathChanged = !string.Equals(DataPath, _originalDataPath, StringComparison.OrdinalIgnoreCase);

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
