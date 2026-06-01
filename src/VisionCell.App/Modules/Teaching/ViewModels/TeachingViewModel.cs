using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.Application.Interlocks;
using VisionCell.Application.Teaching;
using VisionCell.App.Interaction;
using VisionCell.Equipment.Controllers;
using VisionCell.Motion.Teaching;

namespace VisionCell.App.Modules.Teaching.ViewModels;

public sealed partial class TeachingViewModel : ObservableObject
{
    private static readonly TimeSpan SnapshotTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);
    private const int TeachingPointLimit = 50;
    private const int TeachingHistoryLimit = 12;
    private readonly ITeachingPointUseCase _teachingPointUseCase;
    private readonly ITeachingHistoryRepository _historyRepository;
    private readonly IEquipmentController _equipmentController;
    private readonly IUserConfirmationService _confirmationService;
    private int _historyRefreshVersion;

    public TeachingViewModel(
        ITeachingPointUseCase teachingPointUseCase,
        ITeachingHistoryRepository historyRepository,
        IEquipmentController equipmentController,
        IUserConfirmationService confirmationService)
    {
        _teachingPointUseCase = teachingPointUseCase ?? throw new ArgumentNullException(nameof(teachingPointUseCase));
        _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
        _equipmentController = equipmentController ?? throw new ArgumentNullException(nameof(equipmentController));
        _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        RefreshSelectedPointHistoryCommand = new AsyncRelayCommand(RefreshSelectedPointHistoryAsync);
        SaveCurrentPositionCommand = new AsyncRelayCommand(SaveCurrentPositionAsync, () => !IsBusy);
        UpdateSelectedCommand = new AsyncRelayCommand(UpdateSelectedAsync, () => !IsBusy && SelectedPoint is not null);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => !IsBusy && SelectedPoint is not null);
        GoToSelectedCommand = new AsyncRelayCommand(GoToSelectedAsync, () => !IsBusy && SelectedPoint is not null);
    }

    public IReadOnlyList<TeachingRole> RoleOptions { get; } = new[]
    {
        TeachingRole.Load,
        TeachingRole.Camera,
        TeachingRole.Inspection,
        TeachingRole.Review,
        TeachingRole.Safe,
        TeachingRole.Park,
        TeachingRole.Custom
    };

    public ObservableCollection<TeachingPointItemViewModel> Points { get; } = new();
    public ObservableCollection<TeachingHistoryItemViewModel> SelectedPointHistory { get; } = new();
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand RefreshSelectedPointHistoryCommand { get; }
    public IAsyncRelayCommand SaveCurrentPositionCommand { get; }
    public IAsyncRelayCommand UpdateSelectedCommand { get; }
    public IAsyncRelayCommand DeleteSelectedCommand { get; }
    public IAsyncRelayCommand GoToSelectedCommand { get; }

    [ObservableProperty]
    private string _statusText = "Teaching points not loaded";

    [ObservableProperty]
    private string _nameText = "Camera Align";

    [ObservableProperty]
    private TeachingRole _selectedRole = TeachingRole.Camera;

    [ObservableProperty]
    private string _memoText = string.Empty;

    [ObservableProperty]
    private string _toleranceXText = "0.010";

    [ObservableProperty]
    private string _toleranceYText = "0.010";

    [ObservableProperty]
    private string _toleranceZText = "0.010";

    [ObservableProperty]
    private string _toleranceThetaText = "0.010";

    [ObservableProperty]
    private TeachingPointItemViewModel? _selectedPoint;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasPoints;

    [ObservableProperty]
    private bool _hasSelectedPointHistory;

    [ObservableProperty]
    private string _historyStatus = "Select a teaching point to view history";

    [ObservableProperty]
    private DateTimeOffset? _lastHistoryRefreshAt;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var points = await _teachingPointUseCase.ListAsync(TeachingPointLimit, cancellationToken).ConfigureAwait(true);
            Points.Clear();
            foreach (var point in points)
            {
                Points.Add(new TeachingPointItemViewModel(point));
            }

            var selectedId = SelectedPoint?.Id;
            HasPoints = Points.Count > 0;
            SelectedPoint = selectedId is null
                ? Points.FirstOrDefault()
                : Points.FirstOrDefault(point => point.Id == selectedId.Value) ?? Points.FirstOrDefault();
            StatusText = HasPoints ? $"{Points.Count} teaching points loaded" : "No teaching points saved";
            await RefreshSelectedPointHistoryAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Teaching point refresh cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Teaching point refresh failed: {ex.Message}";
        }
    }

    public async Task SaveCurrentPositionAsync(CancellationToken cancellationToken)
    {
        if (!TryCreateTolerance(out var tolerance, out var error))
        {
            StatusText = $"Teaching input rejected: {error}";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _teachingPointUseCase.SaveCurrentPositionAsync(
                new TeachingPointSaveRequest(NameText, SelectedRole, tolerance, MemoText, SnapshotTimeout),
                cancellationToken).ConfigureAwait(true);

            if (!result.IsSuccess || result.Point is null)
            {
                StatusText = result.ValidationIssues.Count > 0
                    ? $"Teaching save rejected: {string.Join(" ", result.ValidationIssues.Select(issue => issue.Message))}"
                    : $"Teaching save failed: {result.Message}";
                return;
            }

            StatusText = $"Saved teaching point '{result.Point.Name}'";
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            SelectedPoint = Points.FirstOrDefault(point => point.Id == result.Point.Id) ?? SelectedPoint;
            await RefreshSelectedPointHistoryAsync(cancellationToken).ConfigureAwait(true);
        }, cancellationToken).ConfigureAwait(true);
    }

    public async Task UpdateSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedPoint is null)
        {
            StatusText = "Select a teaching point before update";
            return;
        }

        if (!TryCreateTolerance(out var tolerance, out var error))
        {
            StatusText = $"Teaching input rejected: {error}";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var selected = SelectedPoint.Point;
            var result = await _teachingPointUseCase.UpdateAsync(
                new TeachingPointUpdateRequest(
                    selected.Id,
                    NameText,
                    SelectedRole,
                    selected.Position,
                    tolerance,
                    MemoText),
                cancellationToken).ConfigureAwait(true);

            if (!result.IsSuccess || result.Point is null)
            {
                StatusText = result.ValidationIssues.Count > 0
                    ? $"Teaching update rejected: {string.Join(" ", result.ValidationIssues.Select(issue => issue.Message))}"
                    : $"Teaching update failed: {result.Message}";
                return;
            }

            StatusText = $"Updated teaching point '{result.Point.Name}'";
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            SelectedPoint = Points.FirstOrDefault(point => point.Id == result.Point.Id) ?? SelectedPoint;
            await RefreshSelectedPointHistoryAsync(cancellationToken).ConfigureAwait(true);
        }, cancellationToken).ConfigureAwait(true);
    }

    public async Task DeleteSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedPoint is null)
        {
            StatusText = "Select a teaching point before delete";
            return;
        }

        var confirmed = await _confirmationService.ConfirmAsync(
            "Delete Teaching Point",
            $"Delete teaching point '{SelectedPoint.Name}'?",
            cancellationToken).ConfigureAwait(true);
        if (!confirmed)
        {
            StatusText = $"Teaching delete cancelled for '{SelectedPoint.Name}'";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var selectedId = SelectedPoint.Id;
            var selectedName = SelectedPoint.Name;
            var result = await _teachingPointUseCase.DeleteAsync(
                new TeachingPointDeleteRequest(selectedId),
                cancellationToken).ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                StatusText = $"Teaching delete failed: {result.Message}";
                return;
            }

            SelectedPoint = null;
            StatusText = $"Deleted teaching point '{selectedName}'";
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            await RefreshSelectedPointHistoryAsync(cancellationToken).ConfigureAwait(true);
        }, cancellationToken).ConfigureAwait(true);
    }

    public async Task GoToSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedPoint is null)
        {
            StatusText = "Select a teaching point before Go To";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var snapshot = await _equipmentController.GetSnapshotAsync(SnapshotTimeout, cancellationToken).ConfigureAwait(true);
            var result = await _teachingPointUseCase.GoToAsync(
                new TeachingPointGoToRequest(
                    SelectedPoint.Id,
                    EquipmentSnapshotInterlockContextFactory.Create(snapshot),
                    CommandTimeout),
                cancellationToken).ConfigureAwait(true);

            StatusText = result.IsSuccess
                ? $"Go To '{SelectedPoint.Name}' completed"
                : $"Go To '{SelectedPoint.Name}' failed: {result.Message}";
            await RefreshSelectedPointHistoryAsync(cancellationToken).ConfigureAwait(true);
        }, cancellationToken).ConfigureAwait(true);
    }

    public async Task RefreshSelectedPointHistoryAsync(CancellationToken cancellationToken)
    {
        var refreshVersion = Interlocked.Increment(ref _historyRefreshVersion);
        var selected = SelectedPoint;
        SelectedPointHistory.Clear();
        HasSelectedPointHistory = false;

        if (selected is null)
        {
            LastHistoryRefreshAt = null;
            HistoryStatus = "Select a teaching point to view history";
            return;
        }

        try
        {
            var entries = await _historyRepository.ListByPointAsync(
                selected.Id,
                TeachingHistoryLimit,
                cancellationToken).ConfigureAwait(true);

            if (refreshVersion != _historyRefreshVersion || SelectedPoint?.Id != selected.Id)
            {
                return;
            }

            SelectedPointHistory.Clear();
            foreach (var entry in entries)
            {
                SelectedPointHistory.Add(new TeachingHistoryItemViewModel(entry));
            }

            HasSelectedPointHistory = SelectedPointHistory.Count > 0;
            LastHistoryRefreshAt = DateTimeOffset.UtcNow;
            HistoryStatus = HasSelectedPointHistory
                ? $"{SelectedPointHistory.Count} teaching history records loaded"
                : $"No teaching history for '{selected.Name}'";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (refreshVersion == _historyRefreshVersion)
            {
                HistoryStatus = "Teaching history refresh cancelled";
            }
        }
        catch (Exception ex)
        {
            if (refreshVersion == _historyRefreshVersion)
            {
                HistoryStatus = $"Teaching history refresh failed: {ex.Message}";
            }
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        SaveCurrentPositionCommand.NotifyCanExecuteChanged();
        UpdateSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        GoToSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPointChanged(TeachingPointItemViewModel? value)
    {
        UpdateSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        GoToSelectedCommand.NotifyCanExecuteChanged();

        if (value is not null)
        {
            LoadSelectedPointIntoEditor(value.Point);
            RefreshSelectedPointHistoryCommand.Execute(null);
        }
        else
        {
            Interlocked.Increment(ref _historyRefreshVersion);
            SelectedPointHistory.Clear();
            HasSelectedPointHistory = false;
            LastHistoryRefreshAt = null;
            HistoryStatus = "Select a teaching point to view history";
        }
    }

    private async Task RunBusyAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Teaching command cancelled";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Teaching command timed out";
        }
        catch (Exception ex)
        {
            StatusText = $"Teaching command failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryCreateTolerance(out PositionTolerance tolerance, out string error)
    {
        tolerance = PositionTolerance.Default;
        if (!TryParsePositive(ToleranceXText, "X tolerance", out var x, out error) ||
            !TryParsePositive(ToleranceYText, "Y tolerance", out var y, out error) ||
            !TryParsePositive(ToleranceZText, "Z tolerance", out var z, out error) ||
            !TryParsePositive(ToleranceThetaText, "Theta tolerance", out var theta, out error))
        {
            return false;
        }

        tolerance = new PositionTolerance(x, y, z, theta);
        return true;
    }

    private void LoadSelectedPointIntoEditor(TeachingPoint point)
    {
        NameText = point.Name;
        SelectedRole = point.Role;
        MemoText = point.Memo ?? string.Empty;
        ToleranceXText = FormatNumber(point.Tolerance.X);
        ToleranceYText = FormatNumber(point.Tolerance.Y);
        ToleranceZText = FormatNumber(point.Tolerance.Z);
        ToleranceThetaText = FormatNumber(point.Tolerance.Theta);
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static bool TryParsePositive(string text, string label, out double value, out string error)
    {
        error = string.Empty;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
            !double.IsFinite(value))
        {
            error = $"{label} must be a finite number.";
            return false;
        }

        if (value <= 0.0)
        {
            error = $"{label} must be greater than zero.";
            return false;
        }

        return true;
    }
}
