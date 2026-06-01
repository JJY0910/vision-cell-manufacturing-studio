using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.Application.Recipes;

namespace VisionCell.App.Modules.Recipe.ViewModels;

public sealed partial class RecipeViewModel : ObservableObject
{
    private const int RecipeIndexLimit = 50;
    private readonly IRecipeIndexRepository _recipeIndexRepository;

    public RecipeViewModel(IRecipeIndexRepository recipeIndexRepository)
    {
        _recipeIndexRepository = recipeIndexRepository ?? throw new ArgumentNullException(nameof(recipeIndexRepository));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
    }

    public ObservableCollection<RecipeIndexItemViewModel> Recipes { get; } = new();
    public IAsyncRelayCommand RefreshCommand { get; }

    [ObservableProperty]
    private string _statusText = "Recipe index not loaded";

    [ObservableProperty]
    private RecipeIndexItemViewModel? _selectedRecipe;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasRecipes;

    [ObservableProperty]
    private int _validRecipeCount;

    [ObservableProperty]
    private int _invalidRecipeCount;

    [ObservableProperty]
    private int _activeRecipeCount;

    [ObservableProperty]
    private string _activeRecipeText = "-";

    [ObservableProperty]
    private string _lastRefreshText = "-";

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var previousRecipeId = SelectedRecipe?.RecipeId;
        var previousVersion = SelectedRecipe?.Version;

        IsBusy = true;
        try
        {
            var entries = await _recipeIndexRepository.ListRecentAsync(RecipeIndexLimit, cancellationToken).ConfigureAwait(true);
            Recipes.Clear();
            foreach (var entry in entries)
            {
                Recipes.Add(new RecipeIndexItemViewModel(entry));
            }

            HasRecipes = Recipes.Count > 0;
            ValidRecipeCount = Recipes.Count(recipe => recipe.IsValid);
            InvalidRecipeCount = Recipes.Count(recipe => !recipe.IsValid);
            ActiveRecipeCount = Recipes.Count(recipe => recipe.IsActive);
            ActiveRecipeText = Recipes.FirstOrDefault(recipe => recipe.IsActive) is { } active
                ? $"{active.RecipeId} v{active.Version}"
                : "-";
            SelectedRecipe = SelectRecipe(previousRecipeId, previousVersion);
            LastRefreshText = DateTimeOffset.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            StatusText = HasRecipes
                ? $"{Recipes.Count} recipe index records loaded"
                : "No recipe index records";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Recipe index refresh cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Recipe index refresh failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
    }

    private RecipeIndexItemViewModel? SelectRecipe(string? previousRecipeId, string? previousVersion)
    {
        if (!string.IsNullOrWhiteSpace(previousRecipeId) && !string.IsNullOrWhiteSpace(previousVersion))
        {
            var previous = Recipes.FirstOrDefault(recipe =>
                string.Equals(recipe.RecipeId, previousRecipeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(recipe.Version, previousVersion, StringComparison.OrdinalIgnoreCase));
            if (previous is not null)
            {
                return previous;
            }
        }

        return Recipes.FirstOrDefault(recipe => recipe.IsActive) ?? Recipes.FirstOrDefault();
    }
}
