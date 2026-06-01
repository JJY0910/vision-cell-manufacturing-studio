using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.Application.Interlocks;
using VisionCell.Application.Motion;
using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Controllers;
using VisionCell.Motion.Axes;
using VisionCell.Motion.Commands;

namespace VisionCell.App.Modules.Motion.ViewModels;

public sealed partial class MotionViewModel : ObservableObject
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SnapshotTimeout = TimeSpan.FromMilliseconds(500);
    private const int RecentHistoryLimit = 25;
    private readonly IMotionCommandUseCase _commandUseCase;
    private readonly IMotionCommandHistoryReader _historyReader;
    private readonly IEquipmentController _equipmentController;
    private InterlockContext _interlockContext = InterlockContext.Disconnected;

    public MotionViewModel(
        IMotionCommandUseCase commandUseCase,
        IMotionCommandHistoryReader historyReader,
        IEquipmentController equipmentController)
    {
        _commandUseCase = commandUseCase ?? throw new ArgumentNullException(nameof(commandUseCase));
        _historyReader = historyReader ?? throw new ArgumentNullException(nameof(historyReader));
        _equipmentController = equipmentController ?? throw new ArgumentNullException(nameof(equipmentController));
        RefreshSnapshotCommand = new AsyncRelayCommand(RefreshSnapshotAsync);
        RefreshHistoryCommand = new AsyncRelayCommand(RefreshHistoryAsync);
        ServoOnCommand = new AsyncRelayCommand(ExecuteServoOnAsync, () => CanExecuteMotionCommand(CommandKind.ServoOn));
        ServoOffCommand = new AsyncRelayCommand(ExecuteServoOffAsync, () => CanExecuteMotionCommand(CommandKind.ServoOff));
        HomeCommand = new AsyncRelayCommand(ExecuteHomeAsync, () => CanExecuteMotionCommand(CommandKind.Home));
        JogPositiveCommand = new AsyncRelayCommand(ExecuteJogPositiveAsync, () => CanExecuteMotionCommand(CommandKind.Jog));
        JogNegativeCommand = new AsyncRelayCommand(ExecuteJogNegativeAsync, () => CanExecuteMotionCommand(CommandKind.Jog));
        MoveAbsoluteCommand = new AsyncRelayCommand(ExecuteMoveAbsoluteAsync, () => CanExecuteMotionCommand(CommandKind.MoveAbsolute));
        StopCommand = new AsyncRelayCommand(ExecuteStopAsync, () => CanExecuteMotionCommand(CommandKind.Stop));
    }

    public IReadOnlyList<string> JogAxisOptions { get; } = new[] { "X", "Y", "Z", "Theta" };
    public ObservableCollection<MotionAxisStatusViewModel> Axes { get; } = new();
    public ObservableCollection<MotionCommandHistoryItemViewModel> RecentCommands { get; } = new();

    public IAsyncRelayCommand RefreshSnapshotCommand { get; }
    public IAsyncRelayCommand RefreshHistoryCommand { get; }
    public IAsyncRelayCommand ServoOnCommand { get; }
    public IAsyncRelayCommand ServoOffCommand { get; }
    public IAsyncRelayCommand HomeCommand { get; }
    public IAsyncRelayCommand JogPositiveCommand { get; }
    public IAsyncRelayCommand JogNegativeCommand { get; }
    public IAsyncRelayCommand JogXPositiveCommand => JogPositiveCommand;
    public IAsyncRelayCommand MoveAbsoluteCommand { get; }
    public IAsyncRelayCommand StopCommand { get; }

    public string ServoOnDisabledReason => GetDisabledReason(CommandKind.ServoOn);
    public string ServoOffDisabledReason => GetDisabledReason(CommandKind.ServoOff);
    public string HomeDisabledReason => GetDisabledReason(CommandKind.Home);
    public string JogDisabledReason => GetDisabledReason(CommandKind.Jog);
    public string MoveAbsoluteDisabledReason => GetDisabledReason(CommandKind.MoveAbsolute);
    public string StopDisabledReason => GetDisabledReason(CommandKind.Stop);

    [ObservableProperty]
    private string _historyStatus = "History not loaded";

    [ObservableProperty]
    private string _commandStatus = "No motion command executed";

    [ObservableProperty]
    private string _controllerStatus = "Controller: Disconnected";

    [ObservableProperty]
    private string _servoStatus = "Servo: Off";

    [ObservableProperty]
    private string _axisStatus = "Axes: Snapshot not loaded";

    [ObservableProperty]
    private string _lastCommandCorrelationId = "-";

    [ObservableProperty]
    private DateTimeOffset? _lastHistoryRefreshAt;

    [ObservableProperty]
    private DateTimeOffset? _lastSnapshotAt;

    [ObservableProperty]
    private bool _hasHistory;

    [ObservableProperty]
    private bool _isCommandExecuting;

    [ObservableProperty]
    private string _selectedJogAxis = "X";

    [ObservableProperty]
    private string _jogStepText = "1.000";

    [ObservableProperty]
    private string _moveTargetXText = "10.000";

    [ObservableProperty]
    private string _moveTargetYText = "20.000";

    [ObservableProperty]
    private string _moveTargetZText = "5.000";

    [ObservableProperty]
    private string _moveTargetThetaText = "0.000";

    [ObservableProperty]
    private string _moveVelocityText = "50.000";

    [ObservableProperty]
    private string _moveAccelerationText = "200.000";

    [ObservableProperty]
    private string _moveDecelerationText = "200.000";

    [ObservableProperty]
    private string _moveJerkText = "1000.000";

    [ObservableProperty]
    private string _moveArrivalToleranceText = "0.010";

    public async Task RefreshSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _equipmentController.GetSnapshotAsync(SnapshotTimeout, cancellationToken).ConfigureAwait(true);
            ApplySnapshot(snapshot);
            CommandStatus = "Motion snapshot refreshed";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            CommandStatus = "Snapshot refresh cancelled";
        }
        catch (OperationCanceledException)
        {
            CommandStatus = "Snapshot refresh timed out";
        }
        catch (Exception ex)
        {
            CommandStatus = $"Snapshot refresh failed: {ex.Message}";
        }
    }

    public async Task RefreshHistoryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var records = await _historyReader.ListRecentAsync(RecentHistoryLimit, cancellationToken).ConfigureAwait(true);
            RecentCommands.Clear();
            foreach (var record in records)
            {
                RecentCommands.Add(CreateItem(record));
            }

            HasHistory = RecentCommands.Count > 0;
            LastHistoryRefreshAt = DateTimeOffset.UtcNow;
            HistoryStatus = HasHistory
                ? $"{RecentCommands.Count} command records loaded"
                : "No motion command history";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            HistoryStatus = "History refresh cancelled";
        }
        catch (Exception ex)
        {
            HistoryStatus = $"History refresh failed: {ex.Message}";
        }
    }

    public Task ExecuteServoOnAsync(CancellationToken cancellationToken)
    {
        return ExecuteMotionCommandAsync(CommandKind.ServoOn, CreateServoParameters("On"), cancellationToken);
    }

    public Task ExecuteServoOffAsync(CancellationToken cancellationToken)
    {
        return ExecuteMotionCommandAsync(CommandKind.ServoOff, CreateServoParameters("Off"), cancellationToken);
    }

    public Task ExecuteHomeAsync(CancellationToken cancellationToken)
    {
        return ExecuteMotionCommandAsync(CommandKind.Home, new Dictionary<string, string>
        {
            ["Axis"] = "All",
            ["HomeMode"] = "SimulatorAllAxes"
        }, cancellationToken);
    }

    public Task ExecuteJogPositiveAsync(CancellationToken cancellationToken)
    {
        if (!TryCreateJogTarget(MotionDirection.Positive, out var jogTarget, out var error))
        {
            CommandStatus = $"Jog input rejected: {error}";
            return Task.CompletedTask;
        }

        return ExecuteMotionCommandAsync(CommandKind.Jog, jogTarget.ToParameters(), cancellationToken);
    }

    public Task ExecuteJogNegativeAsync(CancellationToken cancellationToken)
    {
        if (!TryCreateJogTarget(MotionDirection.Negative, out var jogTarget, out var error))
        {
            CommandStatus = $"Jog input rejected: {error}";
            return Task.CompletedTask;
        }

        return ExecuteMotionCommandAsync(CommandKind.Jog, jogTarget.ToParameters(), cancellationToken);
    }

    public Task ExecuteJogXPositiveAsync(CancellationToken cancellationToken)
    {
        SelectedJogAxis = "X";
        return ExecuteJogPositiveAsync(cancellationToken);
    }

    public Task ExecuteMoveAbsoluteAsync(CancellationToken cancellationToken)
    {
        if (!TryCreateMoveTarget(out var moveTarget, out var error))
        {
            CommandStatus = $"Move input rejected: {error}";
            return Task.CompletedTask;
        }

        return ExecuteMotionCommandAsync(CommandKind.MoveAbsolute, moveTarget.ToParameters(), cancellationToken);
    }

    public Task ExecuteStopAsync(CancellationToken cancellationToken)
    {
        return ExecuteMotionCommandAsync(CommandKind.Stop, new Dictionary<string, string>
        {
            ["Scope"] = "ActiveMotion"
        }, cancellationToken);
    }

    private async Task ExecuteMotionCommandAsync(
        CommandKind command,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        IsCommandExecuting = true;
        NotifyCommandStateChanged();

        try
        {
            var snapshot = await _equipmentController.GetSnapshotAsync(SnapshotTimeout, cancellationToken).ConfigureAwait(true);
            ApplySnapshot(snapshot);

            var execution = await _commandUseCase.ExecuteAsync(
                new MotionCommandExecutionRequest(command, _interlockContext, CommandTimeout, parameters),
                cancellationToken).ConfigureAwait(true);

            LastCommandCorrelationId = execution.CommandResult.CorrelationId.ToString();
            CommandStatus = $"{execution.Request.CommandName} {execution.CommandResult.Status}: {execution.CommandResult.Message}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            CommandStatus = $"{FormatCommand(command)} cancelled";
        }
        catch (OperationCanceledException)
        {
            CommandStatus = $"{FormatCommand(command)} timed out";
        }
        catch (Exception ex)
        {
            CommandStatus = $"{FormatCommand(command)} failed: {ex.Message}";
        }
        finally
        {
            await RefreshHistoryAsync(CancellationToken.None).ConfigureAwait(true);
            await RefreshSnapshotAfterCommandAsync().ConfigureAwait(true);
            IsCommandExecuting = false;
            NotifyCommandStateChanged();
        }
    }

    partial void OnIsCommandExecutingChanged(bool value)
    {
        NotifyCommandStateChanged();
    }

    private async Task RefreshSnapshotAfterCommandAsync()
    {
        try
        {
            var snapshot = await _equipmentController.GetSnapshotAsync(SnapshotTimeout, CancellationToken.None).ConfigureAwait(true);
            ApplySnapshot(snapshot);
        }
        catch (OperationCanceledException)
        {
            CommandStatus = "Command completed, but snapshot refresh timed out";
        }
        catch (Exception ex)
        {
            CommandStatus = $"Command completed, but snapshot refresh failed: {ex.Message}";
        }
    }

    private void ApplySnapshot(EquipmentSnapshot snapshot)
    {
        _interlockContext = EquipmentSnapshotInterlockContextFactory.Create(snapshot);
        LastSnapshotAt = snapshot.Timestamp;
        ControllerStatus = snapshot.IsConnected ? "Controller: Connected" : "Controller: Disconnected";
        ServoStatus = _interlockContext.ServoOn ? "Servo: On" : "Servo: Off";
        Axes.Clear();
        foreach (var axis in snapshot.Axes)
        {
            Axes.Add(CreateAxisItem(axis));
        }

        AxisStatus = FormatAxisSummary(snapshot);
        NotifyCommandStateChanged();
    }

    private bool CanExecuteMotionCommand(CommandKind command)
    {
        return !IsCommandExecuting && _equipmentController.GetCommandAvailability(command, _interlockContext).IsEnabled;
    }

    private string GetDisabledReason(CommandKind command)
    {
        if (IsCommandExecuting)
        {
            return "A motion command is already running.";
        }

        var availability = _equipmentController.GetCommandAvailability(command, _interlockContext);
        return availability.IsEnabled ? "Ready" : availability.DisabledReason;
    }

    private void NotifyCommandStateChanged()
    {
        ServoOnCommand.NotifyCanExecuteChanged();
        ServoOffCommand.NotifyCanExecuteChanged();
        HomeCommand.NotifyCanExecuteChanged();
        JogPositiveCommand.NotifyCanExecuteChanged();
        JogNegativeCommand.NotifyCanExecuteChanged();
        MoveAbsoluteCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ServoOnDisabledReason));
        OnPropertyChanged(nameof(ServoOffDisabledReason));
        OnPropertyChanged(nameof(HomeDisabledReason));
        OnPropertyChanged(nameof(JogDisabledReason));
        OnPropertyChanged(nameof(MoveAbsoluteDisabledReason));
        OnPropertyChanged(nameof(StopDisabledReason));
    }

    private static MotionCommandHistoryItemViewModel CreateItem(MotionCommandHistoryRecord record)
    {
        return new MotionCommandHistoryItemViewModel(
            record.CommandName,
            string.IsNullOrWhiteSpace(record.AxisId) ? "-" : record.AxisId,
            record.Status,
            string.IsNullOrWhiteSpace(record.ErrorCode) ? "-" : record.ErrorCode,
            record.Message,
            record.Elapsed.TotalMilliseconds,
            record.CreatedAt,
            record.CorrelationId);
    }

    private static MotionAxisStatusViewModel CreateAxisItem(AxisSnapshot axis)
    {
        return new MotionAxisStatusViewModel(
            axis.AxisId,
            FormatAxis(axis.AxisId),
            axis.Position,
            axis.Target,
            axis.SoftLimit.Unit,
            axis.SoftLimit.Minimum,
            axis.SoftLimit.Maximum,
            axis.IsHomed,
            axis.ServoOn,
            axis.IsMoving,
            axis.Alarm?.Message ?? "None");
    }

    private bool TryCreateJogTarget(MotionDirection direction, out JogMotionTarget target, out string error)
    {
        target = new JogMotionTarget(AxisId.X, direction, 1.0);

        if (!TryParseAxis(SelectedJogAxis, out var axisId, out error))
        {
            return false;
        }

        if (!TryParseFiniteNumber(JogStepText, "Jog step", out var step, out error))
        {
            return false;
        }

        if (step <= 0.0)
        {
            error = "Jog step must be greater than zero.";
            return false;
        }

        target = new JogMotionTarget(axisId, direction, step);
        return true;
    }

    private bool TryCreateMoveTarget(out AbsoluteMoveTarget target, out string error)
    {
        target = new AbsoluteMoveTarget(10.0, 20.0, 5.0, 0.0);

        if (!TryParseFiniteNumber(MoveTargetXText, "X target", out var x, out error) ||
            !TryParseFiniteNumber(MoveTargetYText, "Y target", out var y, out error) ||
            !TryParseFiniteNumber(MoveTargetZText, "Z target", out var z, out error) ||
            !TryParseFiniteNumber(MoveTargetThetaText, "Theta target", out var theta, out error) ||
            !TryParsePositiveNumber(MoveVelocityText, "Velocity", out var velocity, out error) ||
            !TryParsePositiveNumber(MoveAccelerationText, "Acceleration", out var acceleration, out error) ||
            !TryParsePositiveNumber(MoveDecelerationText, "Deceleration", out var deceleration, out error) ||
            !TryParsePositiveNumber(MoveJerkText, "Jerk", out var jerk, out error) ||
            !TryParsePositiveNumber(MoveArrivalToleranceText, "Arrival tolerance", out var arrivalTolerance, out error))
        {
            return false;
        }

        target = new AbsoluteMoveTarget(x, y, z, theta, velocity, acceleration, deceleration, jerk, arrivalTolerance);
        return true;
    }

    private static IReadOnlyDictionary<string, string> CreateServoParameters(string state)
    {
        return new Dictionary<string, string>
        {
            ["ServoState"] = state
        };
    }

    private static string FormatAxisSummary(EquipmentSnapshot snapshot)
    {
        if (snapshot.Axes.Count == 0)
        {
            return "Axes: No axis snapshot";
        }

        var movingCount = snapshot.Axes.Count(axis => axis.IsMoving);
        var homedCount = snapshot.Axes.Count(axis => axis.IsHomed);
        var alarmCount = snapshot.Axes.Count(axis => axis.Alarm is not null);
        return $"Axes: {homedCount}/{snapshot.Axes.Count} homed, {movingCount} moving, {alarmCount} alarms";
    }

    private static string FormatCommand(CommandKind command)
    {
        return command switch
        {
            CommandKind.ServoOn => "Servo On",
            CommandKind.ServoOff => "Servo Off",
            CommandKind.MoveAbsolute => "Move Absolute",
            _ => command.ToString()
        };
    }

    private static string FormatAxis(AxisId axisId)
    {
        return axisId == AxisId.Theta ? "T" : axisId.ToString();
    }

    private static bool TryParseAxis(string text, out AxisId axisId, out string error)
    {
        axisId = AxisId.X;
        error = string.Empty;

        if (string.Equals(text, "T", StringComparison.OrdinalIgnoreCase))
        {
            axisId = AxisId.Theta;
            return true;
        }

        if (Enum.TryParse<AxisId>(text, ignoreCase: true, out axisId))
        {
            return true;
        }

        error = $"Unsupported axis '{text}'.";
        return false;
    }

    private static bool TryParseFiniteNumber(string text, string label, out double value, out string error)
    {
        error = string.Empty;

        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) &&
            double.IsFinite(value))
        {
            return true;
        }

        error = $"{label} must be a finite number.";
        return false;
    }

    private static bool TryParsePositiveNumber(string text, string label, out double value, out string error)
    {
        if (!TryParseFiniteNumber(text, label, out value, out error))
        {
            return false;
        }

        if (value > 0.0)
        {
            return true;
        }

        error = $"{label} must be greater than zero.";
        return false;
    }
}
