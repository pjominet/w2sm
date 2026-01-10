using System.IO;
using System.Windows;
using System.Windows.Controls;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Views;

public partial class DiffViewerWindow
{
    private readonly List<(DzipConflict Dzip, ScriptFileConflict Script)> _allScriptConflicts;

    private int _currentConflictIndex;
    private int _selectedModIndex;
    private bool _isSyncingScroll;
    private List<int> _diffLinePositions = [];
    private int _currentDiffIndex = -1;

    private DzipConflict CurrentDzipConflict => _allScriptConflicts[_currentConflictIndex].Dzip;
    private ScriptFileConflict CurrentScriptConflict => _allScriptConflicts[_currentConflictIndex].Script;

    public DiffViewerWindow(DzipConflict initialDzip, ScriptFileConflict initialScript, List<DzipConflict> allConflicts)
    {
        InitializeComponent();

        _allScriptConflicts = allConflicts
            .SelectMany(d => d.ScriptConflicts.Select(s => (d, s)))
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

        LoadDiffView();
        UpdateNavigationButtons();
        UpdateStatus();

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
        => DiffPositionText.Text = DiffRenderHelper.FormatDiffPositionText(_diffLinePositions, _currentDiffIndex);

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
        => DiffRenderHelper.ScrollToDiff(LeftDiffView, _diffLinePositions, diffIndex, paddingLines);

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

        StatusText.Text = $"Base: {vanillaLines} lines | Mod: {modLines} lines | {_diffLinePositions.Count} difference(s)";
    }

    private void PrevConflict_Click(object sender, RoutedEventArgs e)
    {
        if (_currentConflictIndex <= 0)
            return;

        _currentConflictIndex--;
        LoadCurrentConflict();
    }

    private void NextConflict_Click(object sender, RoutedEventArgs e)
    {
        if (_currentConflictIndex >= _allScriptConflicts.Count - 1)
            return;

        _currentConflictIndex++;
        LoadCurrentConflict();
    }

    private void ModVersionSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ModVersionSelector.SelectedIndex < 0)
            return;

        _selectedModIndex = ModVersionSelector.SelectedIndex;
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

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
