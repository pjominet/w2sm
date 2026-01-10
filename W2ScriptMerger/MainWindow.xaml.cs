using System.Linq;
using System.Windows;
using System.Windows.Controls;
using W2ScriptMerger.Models;

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
    }

    private void ConflictTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not ViewModels.MainViewModel vm || e.NewValue is null)
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
