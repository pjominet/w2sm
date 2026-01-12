using System.Windows;
using System.Windows.Controls;
using W2ScriptMerger.Models;
using W2ScriptMerger.ViewModels;

namespace W2ScriptMerger;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();

#if DEBUG
        DebugMergeEditorButton.Visibility = Visibility.Visible;
        ViewDiffButton.Visibility = Visibility.Collapsed;
#else
        DebugMergeEditorButton.Visibility = Visibility.Collapsed;
        ViewDiffButton.Visibility = Visibility.Visible;
#endif

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is MainViewModel { IsGamePathValid: false } vm)
                await vm.OpenSettingsCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.ScrollToEnd();
    }

    private void ConflictTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not MainViewModel vm || e.NewValue is null)
            return;

        switch (e.NewValue)
        {
            case DzipConflict dzipConflict:
                vm.SelectedDzipConflict = dzipConflict;
                vm.SelectedScriptConflict = dzipConflict.ScriptConflicts.FirstOrDefault();
                break;
            case ScriptFileConflict scriptConflict:
                vm.SelectedScriptConflict = scriptConflict;
                vm.SelectedDzipConflict = vm.DzipConflicts.FirstOrDefault(d => d.ScriptConflicts.Contains(scriptConflict));
                break;
        }
    }
}
