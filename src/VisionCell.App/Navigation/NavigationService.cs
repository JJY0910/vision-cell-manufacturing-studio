using CommunityToolkit.Mvvm.ComponentModel;

namespace VisionCell.App.Navigation;

public sealed partial class NavigationService : ObservableObject, INavigationService
{
    private readonly Dictionary<string, object> _viewModels = new(StringComparer.Ordinal);

    [ObservableProperty]
    private string _currentKey = string.Empty;

    [ObservableProperty]
    private object? _currentViewModel;

    public void Register(string key, object viewModel)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Navigation key is required.", nameof(key));
        }

        _viewModels[key] = viewModel;
    }

    public void Navigate(string key)
    {
        if (!_viewModels.TryGetValue(key, out var viewModel))
        {
            throw new InvalidOperationException($"Navigation target '{key}' is not registered.");
        }

        CurrentKey = key;
        CurrentViewModel = viewModel;
    }
}
