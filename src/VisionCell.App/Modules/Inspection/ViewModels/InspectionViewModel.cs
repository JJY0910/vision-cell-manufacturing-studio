using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.Application.Recipes;

namespace VisionCell.App.Modules.Inspection.ViewModels;

public sealed partial class InspectionViewModel : ObservableObject
{
    private readonly IActiveRecipeContext _activeRecipeContext;

    public InspectionViewModel(IActiveRecipeContext activeRecipeContext)
    {
        _activeRecipeContext = activeRecipeContext ?? throw new ArgumentNullException(nameof(activeRecipeContext));
        RefreshActiveRecipeCommand = new AsyncRelayCommand(RefreshActiveRecipeAsync, () => !IsBusy);
        RunInspectionCommand = new AsyncRelayCommand(RunInspectionAsync, () => !IsBusy);
    }

    public IAsyncRelayCommand RefreshActiveRecipeCommand { get; }
    public IAsyncRelayCommand RunInspectionCommand { get; }

    [ObservableProperty]
    private string _statusText = "Inspection sequence not started";

    [ObservableProperty]
    private string _activeRecipeText = "-";

    [ObservableProperty]
    private string _precheckStatusText = "Active recipe not checked";

    [ObservableProperty]
    private string _lastCheckText = "-";

    [ObservableProperty]
    private bool _isBusy;

    public async Task RefreshActiveRecipeAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var result = await _activeRecipeContext.GetActiveAsync(cancellationToken).ConfigureAwait(true);
            ApplyActiveRecipeResult(result);
            StatusText = result.IsSuccess
                ? "Inspection precheck ready"
                : $"Inspection precheck blocked: {result.Message}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Inspection precheck cancelled";
        }
        catch (Exception ex)
        {
            ActiveRecipeText = "-";
            PrecheckStatusText = $"Active recipe precheck failed: {ex.Message}";
            StatusText = PrecheckStatusText;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RunInspectionAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var result = await _activeRecipeContext.GetActiveAsync(cancellationToken).ConfigureAwait(true);
            ApplyActiveRecipeResult(result);

            if (!result.IsSuccess)
            {
                StatusText = $"Run inspection rejected: {result.Message}";
                return;
            }

            StatusText = $"Inspection ready for recipe '{result.RecipeId}' v{result.Version}; sequence execution is pending";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Run inspection cancelled";
        }
        catch (Exception ex)
        {
            ActiveRecipeText = "-";
            PrecheckStatusText = $"Active recipe precheck failed: {ex.Message}";
            StatusText = $"Run inspection failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshActiveRecipeCommand.NotifyCanExecuteChanged();
        RunInspectionCommand.NotifyCanExecuteChanged();
    }

    private void ApplyActiveRecipeResult(ActiveRecipeContextResult result)
    {
        if (result.IsSuccess)
        {
            ActiveRecipeText = $"{result.RecipeId} v{result.Version}";
            PrecheckStatusText = result.Message;
        }
        else
        {
            ActiveRecipeText = "-";
            PrecheckStatusText = result.Message;
        }

        LastCheckText = DateTimeOffset.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }
}
