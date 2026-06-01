using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.App.Modules.Dashboard.ViewModels;
using VisionCell.App.Modules.Equipment.ViewModels;
using VisionCell.App.Modules.Inspection.ViewModels;
using VisionCell.App.Modules.Motion.ViewModels;
using VisionCell.App.Modules.OfflineDebug.ViewModels;
using VisionCell.App.Modules.Recipe.ViewModels;
using VisionCell.App.Modules.Reports.ViewModels;
using VisionCell.App.Modules.Settings.ViewModels;
using VisionCell.App.Modules.Teaching.ViewModels;
using VisionCell.App.Navigation;
using VisionCell.Core.Events;

namespace VisionCell.App.Shell;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly DashboardViewModel _dashboardViewModel;

    public ShellViewModel(
        INavigationService navigationService,
        DashboardViewModel dashboardViewModel,
        EquipmentViewModel equipmentViewModel,
        MotionViewModel motionViewModel,
        TeachingViewModel teachingViewModel,
        RecipeViewModel recipeViewModel,
        InspectionViewModel inspectionViewModel,
        OfflineDebugViewModel offlineDebugViewModel,
        ReportsViewModel reportsViewModel,
        SettingsViewModel settingsViewModel)
    {
        _navigationService = navigationService;
        _dashboardViewModel = dashboardViewModel;

        RegisterNavigationTargets(
            dashboardViewModel,
            equipmentViewModel,
            motionViewModel,
            teachingViewModel,
            recipeViewModel,
            inspectionViewModel,
            offlineDebugViewModel,
            reportsViewModel,
            settingsViewModel);

        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            CreateNavigationItem("Dashboard", "Dashboard"),
            CreateNavigationItem("Equipment", "Equipment"),
            CreateNavigationItem("Motion", "Motion"),
            CreateNavigationItem("Teaching", "Teaching"),
            CreateNavigationItem("Recipe", "Recipe"),
            CreateNavigationItem("Inspection", "Inspection"),
            CreateNavigationItem("OfflineDebug", "Offline Debug"),
            CreateNavigationItem("Reports", "Reports"),
            CreateNavigationItem("Settings", "Settings")
        };

        if (_navigationService is INotifyPropertyChanged propertyChanged)
        {
            propertyChanged.PropertyChanged += OnNavigationPropertyChanged;
        }

        _dashboardViewModel.PropertyChanged += OnDashboardPropertyChanged;
        _navigationService.Navigate("Dashboard");
        UpdateSelectedNavigation();
    }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }
    public ObservableCollection<SystemEvent> Events => _dashboardViewModel.Events;
    public object? CurrentViewModel => _navigationService.CurrentViewModel;
    public string CurrentScreenTitle => _navigationService.CurrentKey;
    public string ConnectionStatus => _dashboardViewModel.ConnectionStatus;
    public string ModeStatus => _dashboardViewModel.ModeStatus;
    public string AlarmStatus => _dashboardViewModel.AlarmStatus;
    public string ActiveRecipeStatus => _dashboardViewModel.ActiveRecipeStatus;
    public string ClockStatus => DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");

    private NavigationItemViewModel CreateNavigationItem(string key, string title)
    {
        return new NavigationItemViewModel(key, title, new RelayCommand(() => _navigationService.Navigate(key)));
    }

    private void RegisterNavigationTargets(
        DashboardViewModel dashboardViewModel,
        EquipmentViewModel equipmentViewModel,
        MotionViewModel motionViewModel,
        TeachingViewModel teachingViewModel,
        RecipeViewModel recipeViewModel,
        InspectionViewModel inspectionViewModel,
        OfflineDebugViewModel offlineDebugViewModel,
        ReportsViewModel reportsViewModel,
        SettingsViewModel settingsViewModel)
    {
        _navigationService.Register("Dashboard", dashboardViewModel);
        _navigationService.Register("Equipment", equipmentViewModel);
        _navigationService.Register("Motion", motionViewModel);
        _navigationService.Register("Teaching", teachingViewModel);
        _navigationService.Register("Recipe", recipeViewModel);
        _navigationService.Register("Inspection", inspectionViewModel);
        _navigationService.Register("OfflineDebug", offlineDebugViewModel);
        _navigationService.Register("Reports", reportsViewModel);
        _navigationService.Register("Settings", settingsViewModel);
    }

    private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(INavigationService.CurrentViewModel) or nameof(INavigationService.CurrentKey))
        {
            OnPropertyChanged(nameof(CurrentViewModel));
            OnPropertyChanged(nameof(CurrentScreenTitle));
            UpdateSelectedNavigation();
        }
    }

    private void OnDashboardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DashboardViewModel.ConnectionStatus)
            or nameof(DashboardViewModel.ModeStatus)
            or nameof(DashboardViewModel.AlarmStatus)
            or nameof(DashboardViewModel.ActiveRecipeStatus))
        {
            OnPropertyChanged(nameof(ConnectionStatus));
            OnPropertyChanged(nameof(ModeStatus));
            OnPropertyChanged(nameof(AlarmStatus));
            OnPropertyChanged(nameof(ActiveRecipeStatus));
        }
    }

    private void UpdateSelectedNavigation()
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = string.Equals(item.Key, _navigationService.CurrentKey, StringComparison.Ordinal);
        }
    }
}
