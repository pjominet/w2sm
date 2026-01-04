using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Views;

public partial class InstallLocationDialog : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Message { get; set; }
    public InstallLocation SelectedLocation { get; private set; } = InstallLocation.CookedPC;

    public bool IsCookedPcSelected
    {
        get => SelectedLocation is InstallLocation.CookedPC;
        set
        {
            if (value)
                SelectedLocation = InstallLocation.CookedPC;
            OnPropertyChanged(nameof(IsCookedPcSelected));
            OnPropertyChanged(nameof(IsUserContentSelected));
        }
    }

    public bool IsUserContentSelected
    {
        get => SelectedLocation is InstallLocation.UserContent;
        set
        {
            if (value)
                SelectedLocation = InstallLocation.UserContent;
            OnPropertyChanged(nameof(IsCookedPcSelected));
            OnPropertyChanged(nameof(IsUserContentSelected));
        }
    }

    public RelayCommand OkCommand { get; }
    public RelayCommand CancelCommand { get; }

    public InstallLocationDialog(string modName)
    {
        InitializeComponent();
        DataContext = this;
        Message = $"Mod '{modName}' has unknown structure. Choose where to install it:";

        OkCommand = new RelayCommand(() =>
        {
            DialogResult = true;
            Close();
        });

        CancelCommand = new RelayCommand(() =>
        {
            DialogResult = false;
            Close();
        });
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
