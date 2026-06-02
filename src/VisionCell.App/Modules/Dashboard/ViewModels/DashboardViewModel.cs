using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.Application.Equipment;
using VisionCell.Application.Interlocks;
using VisionCell.Core.Commands;
using VisionCell.Core.Events;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Cameras;
using VisionCell.Equipment.Controllers;
using VisionCell.Equipment.Io;
using VisionCell.Equipment.Safety;
using VisionCell.Motion.Axes;

namespace VisionCell.App.Modules.Dashboard.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private static readonly TimeSpan ControllerCommandTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SnapshotTimeout = TimeSpan.FromMilliseconds(500);
    private readonly IEquipmentDashboardUseCase _equipmentUseCase;
    private InterlockContext _interlockContext = InterlockContext.Disconnected;

    public DashboardViewModel(IEquipmentDashboardUseCase equipmentUseCase)
    {
        _equipmentUseCase = equipmentUseCase ?? throw new ArgumentNullException(nameof(equipmentUseCase));

        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => IsCommandEnabled(CommandKind.Connect));
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => IsCommandEnabled(CommandKind.Disconnect));
        EnterManualModeCommand = new AsyncRelayCommand(EnterManualModeAsync, () => IsCommandEnabled(CommandKind.EnterManualMode));
        EnterAutoModeCommand = new AsyncRelayCommand(EnterAutoModeAsync, () => IsCommandEnabled(CommandKind.EnterAutoMode));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);

        ApplySnapshot(CreateDisconnectedSnapshot(DateTimeOffset.UtcNow));
        AddEvent(SystemEvent.Create(SystemEventSeverity.Info, "App", "DashboardInitialized", "Dashboard state initialized from simulator defaults."));
    }

    public ObservableCollection<StatusPillViewModel> StatusPills { get; } = new();
    public ObservableCollection<AxisStatusViewModel> Axes { get; } = new();
    public ObservableCollection<IoBitStatusViewModel> IoBits { get; } = new();
    public ObservableCollection<CommandAvailabilityViewModel> CommandAvailabilities { get; } = new();
    public ObservableCollection<SystemEvent> Events { get; } = new();

    public IAsyncRelayCommand ConnectCommand { get; }
    public IAsyncRelayCommand DisconnectCommand { get; }
    public IAsyncRelayCommand EnterManualModeCommand { get; }
    public IAsyncRelayCommand EnterAutoModeCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private string _modeStatus = "Offline";

    [ObservableProperty]
    private string _alarmStatus = "None";

    [ObservableProperty]
    private string _activeRecipeStatus = "Recipe: None";

    [ObservableProperty]
    private string _controllerLatencyStatus = "Latency: n/a";

    [ObservableProperty]
    private string _safetyStatus = "Door Closed / EStop Off / Servo Off";

    [ObservableProperty]
    private string _cameraStatus = "Camera Offline";

    [ObservableProperty]
    private DateTimeOffset _lastSnapshotAt;

    public string ConnectDisabledReason => GetCommandAvailability(CommandKind.Connect).DisabledReason;
    public string DisconnectDisabledReason => GetCommandAvailability(CommandKind.Disconnect).DisabledReason;
    public string EnterManualModeDisabledReason => GetCommandAvailability(CommandKind.EnterManualMode).DisabledReason;
    public string EnterAutoModeDisabledReason => GetCommandAvailability(CommandKind.EnterAutoMode).DisabledReason;

    public CommandAvailabilityViewModel GetCommandAvailability(CommandKind command)
    {
        return CommandAvailabilities.FirstOrDefault(item => item.Command == command)
            ?? CreateAvailabilityViewModel(_equipmentUseCase.GetCommandAvailability(command, _interlockContext));
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var result = await _equipmentUseCase
            .ConnectAsync(ControllerCommandTimeout, SnapshotTimeout, cancellationToken)
            .ConfigureAwait(true);

        ApplyCommandResult(result);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        var result = await _equipmentUseCase
            .DisconnectAsync(ControllerCommandTimeout, SnapshotTimeout, cancellationToken)
            .ConfigureAwait(true);

        ApplyCommandResult(result);
    }

    public Task EnterManualModeAsync(CancellationToken cancellationToken)
    {
        return ExecuteControllerCommandAsync(CommandKind.EnterManualMode, cancellationToken);
    }

    public Task EnterAutoModeAsync(CancellationToken cancellationToken)
    {
        return ExecuteControllerCommandAsync(CommandKind.EnterAutoMode, cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var result = await _equipmentUseCase.RefreshAsync(SnapshotTimeout, cancellationToken).ConfigureAwait(true);
        ApplySnapshotResult(result);
    }

    private void ApplySnapshot(EquipmentSnapshot snapshot)
    {
        _interlockContext = EquipmentSnapshotInterlockContextFactory.Create(snapshot);
        IsConnected = snapshot.IsConnected;
        ConnectionStatus = snapshot.IsConnected ? "Connected" : "Disconnected";
        ModeStatus = snapshot.Mode.ToString();
        AlarmStatus = snapshot.Alarm?.Message ?? "None";
        ActiveRecipeStatus = "Recipe: None";
        LastSnapshotAt = snapshot.Timestamp;
        CameraStatus = snapshot.Camera.IsReady ? $"{snapshot.Camera.Name}: Ready" : $"{snapshot.Camera.Name}: Offline";
        SafetyStatus = FormatSafety(snapshot.Safety);

        StatusPills.Clear();
        StatusPills.Add(new StatusPillViewModel("Controller", ConnectionStatus, snapshot.IsConnected ? SystemEventSeverity.Info : SystemEventSeverity.Warning));
        StatusPills.Add(new StatusPillViewModel("Camera", snapshot.Camera.IsReady ? "Ready" : "Offline", snapshot.Camera.IsReady ? SystemEventSeverity.Info : SystemEventSeverity.Warning));
        StatusPills.Add(new StatusPillViewModel("Mode", ModeStatus, snapshot.Mode == MachineMode.Alarm ? SystemEventSeverity.Alarm : SystemEventSeverity.Info));
        StatusPills.Add(new StatusPillViewModel("Alarm", AlarmStatus, snapshot.Alarm is null ? SystemEventSeverity.Info : SystemEventSeverity.Alarm));

        Axes.Clear();
        foreach (var axis in snapshot.Axes)
        {
            Axes.Add(new AxisStatusViewModel(
                axis.AxisId,
                FormatAxis(axis.AxisId),
                axis.Position,
                axis.Target,
                axis.SoftLimit.Unit,
                axis.IsHomed,
                axis.ServoOn,
                axis.IsMoving,
                axis.Alarm?.Message ?? "None"));
        }

        IoBits.Clear();
        foreach (var bit in snapshot.Io.Bits)
        {
            IoBits.Add(new IoBitStatusViewModel(bit.Name, bit.Address, bit.Direction, bit.Value, bit.IsForced));
        }

        RefreshCommandAvailabilities();
    }

    private static string FormatSafety(SafetySnapshot safety)
    {
        var door = safety.DoorClosed ? "Door Closed" : "Door Open";
        var estop = safety.EmergencyStopActive ? "EStop On" : "EStop Off";
        var air = safety.AirPressureOk ? "Air OK" : "Air Low";
        var servo = safety.ServoEnabled ? "Servo On" : "Servo Off";
        var vacuum = safety.VacuumOn ? "Vacuum On" : "Vacuum Off";
        return $"{door} / {estop} / {air} / {servo} / {vacuum}";
    }

    private static string FormatAxis(AxisId axisId)
    {
        return axisId == AxisId.Theta ? "T" : axisId.ToString();
    }

    private void AddEvent(SystemEvent systemEvent)
    {
        Events.Insert(0, systemEvent);
        while (Events.Count > 100)
        {
            Events.RemoveAt(Events.Count - 1);
        }
    }

    private void RefreshCommandAvailabilities()
    {
        CommandAvailabilities.Clear();
        foreach (var command in Enum.GetValues<CommandKind>())
        {
            CommandAvailabilities.Add(CreateAvailabilityViewModel(_equipmentUseCase.GetCommandAvailability(command, _interlockContext)));
        }

        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        EnterManualModeCommand.NotifyCanExecuteChanged();
        EnterAutoModeCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ConnectDisabledReason));
        OnPropertyChanged(nameof(DisconnectDisabledReason));
        OnPropertyChanged(nameof(EnterManualModeDisabledReason));
        OnPropertyChanged(nameof(EnterAutoModeDisabledReason));
    }

    private async Task ExecuteControllerCommandAsync(CommandKind command, CancellationToken cancellationToken)
    {
        var result = await _equipmentUseCase
            .ExecuteCommandAsync(command, _interlockContext, ControllerCommandTimeout, SnapshotTimeout, cancellationToken)
            .ConfigureAwait(true);

        ApplyCommandResult(result);
    }

    private bool IsCommandEnabled(CommandKind command)
    {
        return _equipmentUseCase.GetCommandAvailability(command, _interlockContext).IsEnabled;
    }

    private void ApplyCommandResult(EquipmentDashboardCommandResult result)
    {
        AddEvent(result.CommandEvent);
        ControllerLatencyStatus = $"Latency: {result.CommandResult.Elapsed.TotalMilliseconds:0} ms";
        ApplySnapshotResult(result.SnapshotResult);
    }

    private void ApplySnapshotResult(EquipmentDashboardSnapshotResult result)
    {
        if (result.Snapshot is not null)
        {
            ApplySnapshot(result.Snapshot);
        }

        AddEvent(result.Event);
    }

    private static CommandAvailabilityViewModel CreateAvailabilityViewModel(CommandAvailability availability)
    {
        return new CommandAvailabilityViewModel(
            availability.Command,
            FormatCommand(availability.Command),
            availability.IsEnabled,
            availability.DisabledReason);
    }

    private static string FormatCommand(CommandKind command)
    {
        return command switch
        {
            CommandKind.ServoOn => "Servo On",
            CommandKind.ServoOff => "Servo Off",
            CommandKind.MoveAbsolute => "Move Absolute",
            CommandKind.ResetAlarm => "Reset Alarm",
            CommandKind.EnterManualMode => "Enter Manual",
            CommandKind.EnterAutoMode => "Enter Auto",
            CommandKind.RunInspection => "Run Inspection",
            _ => command.ToString()
        };
    }

    private static EquipmentSnapshot CreateDisconnectedSnapshot(DateTimeOffset timestamp)
    {
        var io = new IoSnapshot(
            new[]
            {
                new IoBitSnapshot("DI_DOOR_CLOSED", "X000", IoBitDirection.Input, true, false),
                new IoBitSnapshot("DI_ESTOP_ON", "X001", IoBitDirection.Input, false, false),
                new IoBitSnapshot("DI_AIR_PRESSURE_OK", "X002", IoBitDirection.Input, true, false),
                new IoBitSnapshot("DI_CAMERA_READY", "X004", IoBitDirection.Input, false, false),
                new IoBitSnapshot("DO_SERVO_ENABLE", "Y000", IoBitDirection.Output, false, false),
                new IoBitSnapshot("DO_RING_LIGHT_ON", "Y002", IoBitDirection.Output, false, false)
            },
            timestamp);

        return new EquipmentSnapshot(
            false,
            MachineMode.Offline,
            new SafetySnapshot(true, false, true, false, false),
            AxisDefaults.CreatePowerOffAxes(),
            io,
            new CameraSnapshot(false, "Virtual 3D camera", timestamp),
            null,
            timestamp);
    }
}
