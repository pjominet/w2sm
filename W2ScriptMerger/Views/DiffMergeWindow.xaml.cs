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
    private readonly List<DzipConflict> _allDzipConflicts;
    private readonly List<(DzipConflict Dzip, ScriptFileConflict Script)> _allScriptConflicts;
    private readonly Differ _differ = new();
    
    private int _currentConflictIndex;
    private int _selectedModIndex;
    private bool _isSyncingScroll;

    public bool MergeAccepted { get; private set; }
    public List<ResolvedConflict> ResolvedConflicts { get; } = [];

    private DzipConflict CurrentDzipConflict => _allScriptConflicts[_currentConflictIndex].Dzip;
    private ScriptFileConflict CurrentScriptConflict => _allScriptConflicts[_currentConflictIndex].Script;

    public DiffMergeWindow(DzipConflict initialDzip, ScriptFileConflict initialScript, List<DzipConflict> allConflicts)
    {
        InitializeComponent();
        
        _allDzipConflicts = allConflicts;
        _allScriptConflicts = allConflicts
            .SelectMany(d => d.ScriptConflicts.Select(s => (d, s)))
            .Where(x => x.s.Status is ConflictStatus.Unresolved or ConflictStatus.NeedsManualResolution)
            .ToList();

        _currentConflictIndex = _allScriptConflicts.FindIndex(x => 
            x.Dzip == initialDzip && x.Script == initialScript);
        
        if (_currentConflictIndex < 0)
            _currentConflictIndex = 0;

        LoadCurrentConflict();
    }

    private void LoadCurrentConflict()
    {
        var script = CurrentScriptConflict;
        
        FileNameText.Text = script.ScriptFileName;
        FilePathText.Text = $"{CurrentDzipConflict.DzipName} / {script.ScriptRelativePath}";
        ConflictIndexText.Text = $" ({_currentConflictIndex + 1}/{_allScriptConflicts.Count})";

        ModVersionSelector.Items.Clear();
        foreach (var mod in script.ModVersions)
            ModVersionSelector.Items.Add(mod.ModName);

        if (ModVersionSelector.Items.Count > 0)
        {
            _selectedModIndex = 0;
            ModVersionSelector.SelectedIndex = 0;
        }

        if (script.MergedContent is not null)
            MergeResultEditor.Text = Encoding.ANSI1250.GetString(script.MergedContent);
        else if (script.ModVersions.Count > 0)
            MergeResultEditor.Text = Encoding.ANSI1250.GetString(File.ReadAllBytes(script.ModVersions[0].ScriptPath));
        else
            MergeResultEditor.Text = Encoding.ANSI1250.GetString(File.ReadAllBytes(script.VanillaScriptPath));

        LoadDiffView();
        UpdateNavigationButtons();
        UpdateStatus();
    }

    private void LoadDiffView()
    {
        var script = CurrentScriptConflict;
        
        var vanillaText = File.Exists(script.VanillaScriptPath)
            ? Extensions.EncodingExtensions.ReadFileWithEncoding(script.VanillaScriptPath)
            : string.Empty;

        var modText = _selectedModIndex < script.ModVersions.Count
            ? Extensions.EncodingExtensions.ReadFileWithEncoding(script.ModVersions[_selectedModIndex].ScriptPath)
            : string.Empty;

        DisplayDiffInRichTextBox(LeftDiffView, vanillaText, modText, isLeft: true);
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

            var lineNumRun = new Run($"{lineNumber,4} ")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60))
            };
            paragraph.Inlines.Add(lineNumRun);

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

    private void UpdateNavigationButtons()
    {
        PrevConflictButton.IsEnabled = _currentConflictIndex > 0;
        NextConflictButton.IsEnabled = _currentConflictIndex < _allScriptConflicts.Count - 1;
    }

    private void UpdateStatus()
    {
        var script = CurrentScriptConflict;
        
        var vanillaLines = File.Exists(script.VanillaScriptPath)
            ? File.ReadAllLines(script.VanillaScriptPath).Length
            : 0;

        var modLines = _selectedModIndex >= 0 && _selectedModIndex < script.ModVersions.Count
            ? File.ReadAllLines(script.ModVersions[_selectedModIndex].ScriptPath).Length
            : 0;

        var mergeLines = MergeResultEditor.Text.Split('\n').Length;

        StatusText.Text = $"Vanilla: {vanillaLines} lines | Mod: {modLines} lines | Merge result: {mergeLines} lines | Resolved: {ResolvedConflicts.Count}/{_allScriptConflicts.Count}";
    }

    private void SaveCurrentMerge()
    {
        var content = Encoding.ANSI1250.GetBytes(MergeResultEditor.Text);
        
        var existing = ResolvedConflicts.FirstOrDefault(r => r.Script == CurrentScriptConflict);
        if (existing is not null)
        {
            existing.MergedContent = content;
        }
        else
        {
            ResolvedConflicts.Add(new ResolvedConflict
            {
                Dzip = CurrentDzipConflict,
                Script = CurrentScriptConflict,
                MergedContent = content
            });
        }
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
        var script = CurrentScriptConflict;
        if (!File.Exists(script.VanillaScriptPath))
            return;

        MergeResultEditor.Text = Encoding.ANSI1250.GetString(File.ReadAllBytes(script.VanillaScriptPath));
        UpdateStatus();
    }

    private void UseMod_Click(object sender, RoutedEventArgs e)
    {
        var script = CurrentScriptConflict;
        if (_selectedModIndex < 0 || _selectedModIndex >= script.ModVersions.Count)
            return;

        MergeResultEditor.Text = Encoding.ANSI1250.GetString(File.ReadAllBytes(script.ModVersions[_selectedModIndex].ScriptPath));
        UpdateStatus();
    }

    private void ResetToVanilla_Click(object sender, RoutedEventArgs e) => UseVanilla_Click(sender, e);
    private void ResetToMod_Click(object sender, RoutedEventArgs e) => UseMod_Click(sender, e);

    private void PrevConflict_Click(object sender, RoutedEventArgs e)
    {
        if (_currentConflictIndex <= 0)
            return;

        SaveCurrentMerge();
        _currentConflictIndex--;
        LoadCurrentConflict();
    }

    private void NextConflict_Click(object sender, RoutedEventArgs e)
    {
        if (_currentConflictIndex >= _allScriptConflicts.Count - 1)
            return;

        SaveCurrentMerge();
        _currentConflictIndex++;
        LoadCurrentConflict();
    }

    private void AcceptAndContinue_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentMerge();

        if (_currentConflictIndex < _allScriptConflicts.Count - 1)
        {
            _currentConflictIndex++;
            LoadCurrentConflict();
        }
        else
        {
            MergeAccepted = true;
            DialogResult = true;
            Close();
        }
    }

    private void AcceptAllAndClose_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentMerge();
        MergeAccepted = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        MergeAccepted = false;
        DialogResult = false;
        Close();
    }
}

public class ResolvedConflict
{
    public required DzipConflict Dzip { get; init; }
    public required ScriptFileConflict Script { get; init; }
    public required byte[] MergedContent { get; set; }
}
