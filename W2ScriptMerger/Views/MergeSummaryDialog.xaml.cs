using System.Diagnostics;
using System.IO;
using System.Windows;

namespace W2ScriptMerger.Views;

public partial class MergeSummaryDialog
{
    private readonly string _folderPath;

    public MergeSummaryDialog(string mergedModsPath, int autoMergedCount, int manualMergedCount)
    {
        InitializeComponent();

        _folderPath = Path.GetDirectoryName(mergedModsPath) ?? string.Empty;
        FolderPathText.Text = _folderPath;

        var total = autoMergedCount + manualMergedCount;
        SummaryText.Text = $"{total} script(s) merged ({autoMergedCount} auto, {manualMergedCount} manual)";

        ContentTextBox.Text = File.Exists(mergedModsPath)
            ? File.ReadAllText(mergedModsPath)
            : "Merge manifest not found.";
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_folderPath))
            Process.Start("explorer.exe", _folderPath);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
