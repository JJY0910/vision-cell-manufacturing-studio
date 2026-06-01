namespace VisionCell.App.Navigation;

public interface INavigationService
{
    string CurrentKey { get; }
    object? CurrentViewModel { get; }
    void Register(string key, object viewModel);
    void Navigate(string key);
}
