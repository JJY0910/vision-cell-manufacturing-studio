using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.Application.Inspection;
using VisionCell.Application.Recipes;
using VisionCell.Equipment.Cameras;

namespace VisionCell.App.Modules.Inspection.ViewModels;

public sealed partial class InspectionViewModel : ObservableObject
{
    private static readonly InspectionRunRequest DefaultRunRequest = new(
        SnapshotTimeout: TimeSpan.FromSeconds(2),
        CommandTimeout: TimeSpan.FromSeconds(5))
    {
        GrabTimeout = TimeSpan.FromSeconds(2)
    };

    private readonly IActiveRecipeContext _activeRecipeContext;
    private readonly IInspectionRunUseCase _inspectionRunUseCase;
    private CancellationTokenSource? _activeRunCancellation;

    public InspectionViewModel(
        IActiveRecipeContext activeRecipeContext,
        IInspectionRunUseCase inspectionRunUseCase)
    {
        _activeRecipeContext = activeRecipeContext ?? throw new ArgumentNullException(nameof(activeRecipeContext));
        _inspectionRunUseCase = inspectionRunUseCase ?? throw new ArgumentNullException(nameof(inspectionRunUseCase));
        RefreshActiveRecipeCommand = new AsyncRelayCommand(RefreshActiveRecipeAsync, () => !IsBusy);
        RunInspectionCommand = new AsyncRelayCommand(RunInspectionAsync, () => !IsBusy);
        StopInspectionCommand = new RelayCommand(StopInspection, () => IsBusy && _activeRunCancellation is not null);
    }

    public IAsyncRelayCommand RefreshActiveRecipeCommand { get; }
    public IAsyncRelayCommand RunInspectionCommand { get; }
    public IRelayCommand StopInspectionCommand { get; }
    public ObservableCollection<InspectionStepViewModel> SequenceSteps { get; } = new();

    [ObservableProperty]
    private string _statusText = "Inspection sequence not started";

    [ObservableProperty]
    private string _activeRecipeText = "-";

    [ObservableProperty]
    private string _precheckStatusText = "Active recipe not checked";

    [ObservableProperty]
    private string _lastCheckText = "-";

    [ObservableProperty]
    private string _lastRunCorrelationId = "-";

    [ObservableProperty]
    private string _lastGrabText = "No image grabbed";

    [ObservableProperty]
    private ImageSource? _lastGrabImageSource;

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
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeRunCancellation = linkedCancellation;
        StopInspectionCommand.NotifyCanExecuteChanged();
        SequenceSteps.Clear();

        try
        {
            var progress = new InspectionStepProgress(ApplyStepUpdate);
            var result = await _inspectionRunUseCase
                .RunAsync(DefaultRunRequest, progress, linkedCancellation.Token)
                .ConfigureAwait(true);

            ApplyInspectionRunResult(result);
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
            _activeRunCancellation = null;
            IsBusy = false;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshActiveRecipeCommand.NotifyCanExecuteChanged();
        RunInspectionCommand.NotifyCanExecuteChanged();
        StopInspectionCommand.NotifyCanExecuteChanged();
    }

    private void StopInspection()
    {
        _activeRunCancellation?.Cancel();
        StatusText = "Stop inspection requested";
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

    private void ApplyInspectionRunResult(InspectionRunResult result)
    {
        if (result.Recipe is not null)
        {
            ActiveRecipeText = $"{result.Recipe.RecipeId} v{result.Recipe.Version}";
            PrecheckStatusText = $"Active recipe '{result.Recipe.RecipeId}' v{result.Recipe.Version} is ready.";
        }
        else
        {
            ActiveRecipeText = "-";
            PrecheckStatusText = result.Message;
        }

        LastRunCorrelationId = result.Request?.CorrelationId.ToString() ?? "-";
        LastCheckText = result.CompletedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        ApplyCameraGrabResult(result.CameraGrabResult);
        StatusText = result.Status switch
        {
            InspectionRunStatus.Accepted => result.Message,
            InspectionRunStatus.CommandCancelled => result.Message,
            _ => $"Run inspection rejected: {result.Message}"
        };

        foreach (var step in result.Steps)
        {
            ApplyStepUpdate(step);
        }
    }

    private void ApplyStepUpdate(InspectionSequenceStepRecord step)
    {
        var existing = SequenceSteps.FirstOrDefault(item => string.Equals(item.Name, step.Name, StringComparison.Ordinal));
        if (existing is null)
        {
            SequenceSteps.Add(new InspectionStepViewModel(step));
            return;
        }

        existing.Apply(step);
    }

    private void ApplyCameraGrabResult(CameraGrabResult? result)
    {
        if (result?.Frame is not null)
        {
            LastGrabText = $"{result.Frame.Width} x {result.Frame.Height} {result.Frame.PixelFormat} | {result.Frame.CameraName}";
            LastGrabImageSource = CreateImageSource(result.Frame);
            return;
        }

        LastGrabText = result?.Message ?? "No camera frame grabbed";
        LastGrabImageSource = null;
    }

    private static ImageSource CreateImageSource(CameraFrame frame)
    {
        if (frame.PixelFormat != CameraPixelFormat.Gray8)
        {
            throw new NotSupportedException($"Unsupported camera pixel format: {frame.PixelFormat}.");
        }

        var bitmap = BitmapSource.Create(
            frame.Width,
            frame.Height,
            96,
            96,
            PixelFormats.Gray8,
            palette: null,
            frame.Pixels,
            frame.Stride);
        bitmap.Freeze();
        return bitmap;
    }

    private sealed class InspectionStepProgress : IProgress<InspectionSequenceStepRecord>
    {
        private readonly Action<InspectionSequenceStepRecord> _handler;

        public InspectionStepProgress(Action<InspectionSequenceStepRecord> handler)
        {
            _handler = handler;
        }

        public void Report(InspectionSequenceStepRecord value)
        {
            _handler(value);
        }
    }
}
