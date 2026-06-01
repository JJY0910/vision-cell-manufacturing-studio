using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VisionCell.App.Shell;

public sealed partial class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(string key, string title, IRelayCommand navigateCommand)
    {
        Key = key;
        Title = title;
        NavigateCommand = navigateCommand;
    }

    public string Key { get; }
    public string Title { get; }
    public IRelayCommand NavigateCommand { get; }

    [ObservableProperty]
    private bool _isSelected;
}
