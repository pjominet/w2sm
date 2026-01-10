using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using W2ScriptMerger.Extensions;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Views;

public partial class DiffMergeWindow
{
    private readonly List<(DzipConflict Dzip, ScriptFileConflict Script)> _allScriptConflicts;

    private int _currentConflictIndex;
    private int _selectedModIndex;
    private bool _isSyncingScroll;
    private List<int> _diffLinePositions = [];
    private int _currentDiffIndex = -1;

    public bool MergeAccepted { get; private set; }
    public List<ResolvedConflict> ResolvedConflicts { get; } = [];

    private DzipConflict CurrentDzipConflict => _allScriptConflicts[_currentConflictIndex].Dzip;
    private ScriptFileConflict CurrentScriptConflict => _allScriptConflicts[_currentConflictIndex].Script;

    public DiffMergeWindow(DzipConflict initialDzip, ScriptFileConflict initialScript, List<DzipConflict> allConflicts)
    {
        InitializeComponent();

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
        FilePathText.Text = $"{CurrentDzipConflict.DzipName}\\{script.ScriptRelativePath.Replace('/', '\\')}";
        ConflictIndexText.Text = $" ({_currentConflictIndex + 1}/{_allScriptConflicts.Count})";

        ModVersionSelector.Items.Clear();
        foreach (var mod in script.ModVersions)
            ModVersionSelector.Items.Add(string.IsNullOrEmpty(mod.DisplayName) ? mod.ModName : mod.DisplayName);

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
        UpdateMergeLineNumbers();

        // Jump to first diff with padding
        if (_diffLinePositions.Count <= 0)
            return;

        _currentDiffIndex = 0;
        Dispatcher.BeginInvoke(() => ScrollToDiffWithPadding(0, 3), System.Windows.Threading.DispatcherPriority.Loaded);
        UpdateDiffPositionText();
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

        _diffLinePositions = DiffRenderHelper.RenderDiff(LeftDiffView, vanillaText, modText, isLeft: true);
        _currentDiffIndex = -1;
        DiffRenderHelper.RenderDiff(RightDiffView, vanillaText, modText, isLeft: false);
        UpdateDiffPositionText();
    }

    private void UpdateDiffPositionText()
    {
        DiffPositionText.Text = DiffRenderHelper.FormatDiffPositionText(_diffLinePositions, _currentDiffIndex);
    }

    private void PrevDiff_Click(object sender, RoutedEventArgs e)
    {
        if (_diffLinePositions.Count == 0) return;

        _currentDiffIndex = _currentDiffIndex <= 0
            ? _diffLinePositions.Count - 1
            : _currentDiffIndex - 1;

        DiffRenderHelper.ScrollToDiff(LeftDiffView, _diffLinePositions, _currentDiffIndex);
        UpdateDiffPositionText();
    }

    private void NextDiff_Click(object sender, RoutedEventArgs e)
    {
        if (_diffLinePositions.Count == 0) return;

        _currentDiffIndex = _currentDiffIndex >= _diffLinePositions.Count - 1
            ? 0
            : _currentDiffIndex + 1;

        DiffRenderHelper.ScrollToDiff(LeftDiffView, _diffLinePositions, _currentDiffIndex);
        UpdateDiffPositionText();
    }

    private void ScrollToDiffWithPadding(int diffIndex, int paddingLines = 2)
    {
        DiffRenderHelper.ScrollToDiff(LeftDiffView, _diffLinePositions, diffIndex, paddingLines);
    }

    private void UpdateMergeLineNumbers()
    {
        var lineCount = MergeResultEditor.Text.Split('\n').Length;
        var sb = new StringBuilder();
        for (var i = 1; i <= lineCount; i++)
            sb.AppendLine(i.ToString());
        MergeLineNumbers.Text = sb.ToString().TrimEnd();
    }

    private void MergeResultEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateMergeLineNumbers();
    }

    private void MergeResultEditor_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var scrollViewer = GetScrollViewer(MergeResultEditor);
        var lineNumbersScrollViewer = GetScrollViewer(MergeLineNumbers);
        if (scrollViewer != null && lineNumbersScrollViewer != null)
        {
            lineNumbersScrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset);
        }
    }

    private static ScrollViewer? GetScrollViewer(DependencyObject o)
    {
        if (o is ScrollViewer sv) return sv;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
        {
            var child = VisualTreeHelper.GetChild(o, i);
            var result = GetScrollViewer(child);
            if (result != null) return result;
        }
        return null;
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
