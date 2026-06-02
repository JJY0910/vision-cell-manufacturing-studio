using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.Application.Recipes;
using VisionCell.Core.Primitives;
using VisionCell.Motion.Teaching;

namespace VisionCell.App.Modules.Recipe.ViewModels;

public sealed partial class RecipeViewModel : ObservableObject
{
    private const int RecipeIndexLimit = 50;
    private readonly IRecipeLibraryUseCase _recipeLibraryUseCase;

    public RecipeViewModel(IRecipeLibraryUseCase recipeLibraryUseCase)
    {
        _recipeLibraryUseCase = recipeLibraryUseCase ?? throw new ArgumentNullException(nameof(recipeLibraryUseCase));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        SaveRecipeCommand = new AsyncRelayCommand(SaveRecipeAsync, () => !IsBusy);
        ActivateRecipeCommand = new AsyncRelayCommand(ActivateRecipeAsync, CanActivateRecipe);
    }

    public ObservableCollection<RecipeIndexItemViewModel> Recipes { get; } = new();
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand SaveRecipeCommand { get; }
    public IAsyncRelayCommand ActivateRecipeCommand { get; }

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

    [ObservableProperty]
    private string _recipeIdText = "PKG-MEMORY-MODULE";

    [ObservableProperty]
    private string _productNameText = "Memory Module Sample";

    [ObservableProperty]
    private string _versionText = "1.0.0";

    [ObservableProperty]
    private string _teachingPointIdText = "CAMERA_POS_01";

    [ObservableProperty]
    private string _teachingPointNameText = "Camera Position 01";

    [ObservableProperty]
    private string _teachingXText = "10.000";

    [ObservableProperty]
    private string _teachingYText = "25.000";

    [ObservableProperty]
    private string _teachingZText = "8.000";

    [ObservableProperty]
    private string _teachingThetaText = "0.000";

    [ObservableProperty]
    private string _cameraExposureText = "5.000";

    [ObservableProperty]
    private string _cameraGainText = "1.000";

    [ObservableProperty]
    private string _cameraLightText = "80";

    [ObservableProperty]
    private string _roiIdText = "IC_TOP";

    [ObservableProperty]
    private string _roiNameText = "IC Top";

    [ObservableProperty]
    private string _roiXText = "116";

    [ObservableProperty]
    private string _roiYText = "74";

    [ObservableProperty]
    private string _roiWidthText = "92";

    [ObservableProperty]
    private string _roiHeightText = "88";

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var previousRecipeId = SelectedRecipe?.RecipeId;
        var previousVersion = SelectedRecipe?.Version;

        IsBusy = true;
        try
        {
            await LoadIndexAsync(previousRecipeId, previousVersion, cancellationToken).ConfigureAwait(true);
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

    public async Task SaveRecipeAsync(CancellationToken cancellationToken)
    {
        if (!TryCreateRecipe(out var recipe, out var error))
        {
            StatusText = $"Recipe input rejected: {error}";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _recipeLibraryUseCase.SaveAsync(
                new RecipeLibrarySaveRequest(recipe),
                cancellationToken).ConfigureAwait(true);

            if (!result.IsSuccess || result.Entry is null)
            {
                StatusText = result.ValidationIssues.Count > 0
                    ? $"Recipe save rejected: {string.Join(" ", result.ValidationIssues.Select(issue => issue.Message))}"
                    : $"Recipe save failed: {result.Message}";
                return;
            }

            try
            {
                await LoadIndexAsync(result.Entry.RecipeId, result.Entry.Version, cancellationToken).ConfigureAwait(true);
                StatusText = $"Saved recipe '{result.Entry.RecipeId}' v{result.Entry.Version}";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                StatusText = "Recipe save completed, but index refresh was cancelled";
            }
            catch (Exception ex)
            {
                StatusText = $"Recipe saved, but index refresh failed: {ex.Message}";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Recipe save cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Recipe save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ActivateRecipeAsync(CancellationToken cancellationToken)
    {
        if (SelectedRecipe is null)
        {
            StatusText = "Select a recipe to activate";
            return;
        }

        var recipeId = SelectedRecipe.RecipeId;
        var version = SelectedRecipe.Version;

        IsBusy = true;
        try
        {
            var activated = await _recipeLibraryUseCase.ActivateAsync(
                recipeId,
                version,
                cancellationToken).ConfigureAwait(true);

            if (!activated)
            {
                StatusText = $"Recipe activation rejected: '{recipeId}' v{version} was not found";
                return;
            }

            try
            {
                await LoadIndexAsync(recipeId, version, cancellationToken).ConfigureAwait(true);
                StatusText = $"Activated recipe '{recipeId}' v{version}";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                StatusText = "Recipe activation completed, but index refresh was cancelled";
            }
            catch (Exception ex)
            {
                StatusText = $"Recipe activated, but index refresh failed: {ex.Message}";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Recipe activation cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Recipe activation failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        SaveRecipeCommand.NotifyCanExecuteChanged();
        ActivateRecipeCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRecipeChanged(RecipeIndexItemViewModel? value)
    {
        ActivateRecipeCommand.NotifyCanExecuteChanged();
    }

    private bool CanActivateRecipe()
    {
        return !IsBusy && SelectedRecipe is not null;
    }

    private async Task LoadIndexAsync(
        string? preferredRecipeId,
        string? preferredVersion,
        CancellationToken cancellationToken)
    {
        var entries = await _recipeLibraryUseCase.ListRecentAsync(RecipeIndexLimit, cancellationToken).ConfigureAwait(true);
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
        SelectedRecipe = SelectRecipe(preferredRecipeId, preferredVersion);
        LastRefreshText = DateTimeOffset.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    private bool TryCreateRecipe(out RecipeDefinition recipe, out string error)
    {
        recipe = CreateFallbackRecipe();

        if (!TryParseDouble(TeachingXText, "Teaching X", out var x, out error) ||
            !TryParseDouble(TeachingYText, "Teaching Y", out var y, out error) ||
            !TryParseDouble(TeachingZText, "Teaching Z", out var z, out error) ||
            !TryParseDouble(TeachingThetaText, "Teaching Theta", out var theta, out error) ||
            !TryParseDouble(CameraExposureText, "Camera exposure", out var exposure, out error) ||
            !TryParseDouble(CameraGainText, "Camera gain", out var gain, out error) ||
            !TryParseInt(CameraLightText, "Camera light", out var light, out error) ||
            !TryParseInt(RoiXText, "ROI X", out var roiX, out error) ||
            !TryParseInt(RoiYText, "ROI Y", out var roiY, out error) ||
            !TryParseInt(RoiWidthText, "ROI width", out var roiWidth, out error) ||
            !TryParseInt(RoiHeightText, "ROI height", out var roiHeight, out error))
        {
            return false;
        }

        var timestamp = DateTimeOffset.UtcNow;
        recipe = new RecipeDefinition(
            RecipeIdText.Trim(),
            ProductNameText.Trim(),
            VersionText.Trim(),
            timestamp,
            timestamp,
            new RecipeMotionSection(new[]
            {
                new RecipeTeachingPoint(
                    TeachingPointIdText.Trim(),
                    TeachingPointNameText.Trim(),
                    TeachingRole.Camera,
                    new Position4D(x, y, z, theta),
                    new PositionTolerance(0.05, 0.05, 0.02, 0.1))
            }),
            new RecipeCameraSettings(exposure, gain, light),
            new RecipeVisionSection(
                new[]
                {
                    new RecipeRoi(
                        RoiIdText.Trim(),
                        RoiNameText.Trim(),
                        roiX,
                        roiY,
                        roiWidth,
                        roiHeight)
                },
                new RecipeVisionParameters(0.75, 8, 0.65, 1.0, 0.15, 0.15)),
            new RecipeSequence(new[] { "SafetyCheck", "MoveToCamera", "Grab", "Inspect2D", "Inspect3D", "Judge", "Persist" }));

        return true;
    }

    private static RecipeDefinition CreateFallbackRecipe()
    {
        var timestamp = DateTimeOffset.UnixEpoch;
        return new RecipeDefinition(
            string.Empty,
            string.Empty,
            "0.0.0",
            timestamp,
            timestamp,
            new RecipeMotionSection(Array.Empty<RecipeTeachingPoint>()),
            new RecipeCameraSettings(1.0, 0.0, 0),
            new RecipeVisionSection(
                Array.Empty<RecipeRoi>(),
                new RecipeVisionParameters(0.0, 0, 0.0, 1.0, 0.0, 0.0)),
            new RecipeSequence(Array.Empty<string>()));
    }

    private static bool TryParseDouble(string text, string label, out double value, out string error)
    {
        error = string.Empty;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            double.IsFinite(value))
        {
            return true;
        }

        error = $"{label} must be a finite number.";
        return false;
    }

    private static bool TryParseInt(string text, string label, out int value, out string error)
    {
        error = string.Empty;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        error = $"{label} must be an integer.";
        return false;
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
