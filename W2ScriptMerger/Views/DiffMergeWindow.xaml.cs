using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using W2ScriptMerger.Models;
using W2ScriptMerger.Extensions;

namespace W2ScriptMerger.Views;

public partial class DiffMergeWindow
{
    private readonly ModConflict _conflict;
    private readonly Differ _differ = new();
    private int _selectedModIndex;
    private bool _isSyncingScroll;

    public bool MergeAccepted { get; private set; }
    public byte[]? MergedContent { get; private set; }

    public DiffMergeWindow(ModConflict conflict)
    {
        InitializeComponent();
        _conflict = conflict;

        FileNameText.Text = conflict.OriginalFileName;
        FilePathText.Text = conflict.RelativePath;

        // Populate mod version selector
        foreach (var mod in conflict.ModVersions)
            ModVersionSelector.Items.Add(mod.DzipSource);

        if (ModVersionSelector.Items.Count > 0)
            ModVersionSelector.SelectedIndex = 0;

        // Set initial merge result
        if (conflict.MergeContent is not null)
            MergeResultEditor.Text = Encoding.UTF8.GetString(conflict.MergeContent);
        else if (conflict.ModVersions.Count > 0)
            MergeResultEditor.Text = Encoding.UTF8.GetString(File.ReadAllBytes(conflict.ModVersions[0].ContentPath));

        // Show vanilla if available
        if (conflict.VanillaContentPath is not null)
        {
            LeftPanelTitle.Text = "Vanilla Version";
            UseVanillaButton.Visibility = Visibility.Visible;
        }
        else
        {
            LeftPanelTitle.Text = "No Vanilla Version";
            UseVanillaButton.Visibility = Visibility.Collapsed;
        }

        LoadDiffView();
        UpdateStatus();
    }

    private void LoadDiffView()
    {
        var vanillaText = _conflict.VanillaContentPath is not null
            ? Encoding.UTF8.GetString(File.ReadAllBytes(_conflict.VanillaContentPath))
            : string.Empty;

        var modText = _selectedModIndex < _conflict.ModVersions.Count
            ? Encoding.UTF8.GetString(File.ReadAllBytes(_conflict.ModVersions[_selectedModIndex].ContentPath))
            : string.Empty;

        // Build diff
        var diffBuilder = new InlineDiffBuilder(_differ);

        // Left panel - vanilla with diff highlighting
        DisplayDiffInRichTextBox(LeftDiffView, vanillaText, modText, isLeft: true);

        // Right panel - mod with diff highlighting
        DisplayDiffInRichTextBox(RightDiffView, vanillaText, modText, isLeft: false);
    }

    private void DisplayDiffInRichTextBox(RichTextBox rtb, string leftText, string rightText, bool isLeft)
    {
        var diffBuilder = new SideBySideDiffBuilder(_differ);
        var diff = diffBuilder.BuildDiffModel(leftText, rightText);

        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            PageWidth = 10000
        };

        var lines = isLeft ? diff.OldText.Lines : diff.NewText.Lines;
        var lineNumber = 1;

        foreach (var line in lines)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
                Padding = new Thickness(0),
                LineHeight = 1
            };

            // Line number
            var lineNumRun = new Run($"{lineNumber,4} ")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60))
            };
            paragraph.Inlines.Add(lineNumRun);

            // Line content with highlighting
            var contentRun = new Run(line.Text ?? string.Empty);

            switch (line.Type)
            {
                case ChangeType.Inserted:
                    paragraph.Background = new SolidColorBrush(Color.FromRgb(0x23, 0x42, 0x23));
                    contentRun.Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x99, 0x55));
                    break;
                case ChangeType.Deleted:
                    paragraph.Background = new SolidColorBrush(Color.FromRgb(0x42, 0x23, 0x23));
                    contentRun.Foreground = new SolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78));
                    break;
                case ChangeType.Modified:
                    paragraph.Background = new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x23));
                    contentRun.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xAA));
                    break;
                case ChangeType.Imaginary:
                    paragraph.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
                    contentRun.Foreground = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50));
                    contentRun.Text = "~";
                    break;
                case ChangeType.Unchanged:
                default:
                    contentRun.Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
                    break;
            }

            paragraph.Inlines.Add(contentRun);
            document.Blocks.Add(paragraph);
            lineNumber++;
        }

        rtb.Document = document;
    }

    private void ModVersionSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        _selectedModIndex = ModVersionSelector.SelectedIndex;
        if (_selectedModIndex < 0)
            return;

        LoadDiffView();
        UpdateStatus();
    }

    private void SyncScroll_Left(object sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll) return;
        _isSyncingScroll = true;

        RightDiffView.ScrollToVerticalOffset(e.VerticalOffset);
        RightDiffView.ScrollToHorizontalOffset(e.HorizontalOffset);

        _isSyncingScroll = false;
    }

    private void SyncScroll_Right(object sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll) return;
        _isSyncingScroll = true;

        LeftDiffView.ScrollToVerticalOffset(e.VerticalOffset);
        LeftDiffView.ScrollToHorizontalOffset(e.HorizontalOffset);

        _isSyncingScroll = false;
    }

    private void UseVanilla_Click(object sender, RoutedEventArgs e)
    {
        if (_conflict.VanillaContentPath is null)
            return;

        MergeResultEditor.Text = Encoding.ANSI1250.GetString(File.ReadAllBytes(_conflict.VanillaContentPath));
        UpdateStatus();
    }

    private void UseMod_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedModIndex < 0 || _selectedModIndex >= _conflict.ModVersions.Count)
            return;

        MergeResultEditor.Text = Encoding.ANSI1250.GetString(File.ReadAllBytes(_conflict.ModVersions[_selectedModIndex].ContentPath));
        UpdateStatus();
    }

    private void CopyFromVanilla_Click(object sender, RoutedEventArgs e) => UseVanilla_Click(sender, e);

    private void CopyFromMod_Click(object sender, RoutedEventArgs e) => UseMod_Click(sender, e);

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        MergeAccepted = false;
        DialogResult = false;
        Close();
    }

    private void SaveMerge_Click(object sender, RoutedEventArgs e)
    {
        MergeAccepted = true;
        MergedContent = Encoding.UTF8.GetBytes(MergeResultEditor.Text);
        DialogResult = true;
        Close();
    }

    private void UpdateStatus()
    {
        var vanillaLines = _conflict.VanillaContentPath is not null
            ? Encoding.UTF8.GetString(File.ReadAllBytes(_conflict.VanillaContentPath)).Split('\n').Length
            : 0;

        var modLines = _selectedModIndex >= 0 && _selectedModIndex < _conflict.ModVersions.Count
            ? Encoding.UTF8.GetString(File.ReadAllBytes(_conflict.ModVersions[_selectedModIndex].ContentPath)).Split('\n').Length
            : 0;

        var mergeLines = MergeResultEditor.Text.Split('\n').Length;

        StatusText.Text = $"Vanilla: {vanillaLines} lines | Mod: {modLines} lines | Merge result: {mergeLines} lines";
    }
}
