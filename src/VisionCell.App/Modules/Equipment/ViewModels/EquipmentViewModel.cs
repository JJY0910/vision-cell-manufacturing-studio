using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.Application.Equipment;
using VisionCell.Core.Errors;
using VisionCell.Core.Events;
using VisionCell.Equipment.Controllers;
using VisionCell.Equipment.Faults;
using VisionCell.App.Modules.Dashboard.ViewModels;

namespace VisionCell.App.Modules.Equipment.ViewModels;

public sealed partial class EquipmentViewModel : ObservableObject
{
    private static readonly TimeSpan FaultCommandTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SnapshotTimeout = TimeSpan.FromMilliseconds(500);
    private readonly IEquipmentDashboardUseCase _dashboardUseCase;
    private readonly IEquipmentFaultInjectionUseCase _faultInjectionUseCase;

    public EquipmentViewModel(
        IEquipmentDashboardUseCase dashboardUseCase,
        IEquipmentFaultInjectionUseCase faultInjectionUseCase)
    {
        _dashboardUseCase = dashboardUseCase ?? throw new ArgumentNullException(nameof(dashboardUseCase));
        _faultInjectionUseCase = faultInjectionUseCase ?? throw new ArgumentNullException(nameof(faultInjectionUseCase));

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        InjectEmergencyStopCommand = CreateFaultCommand(EquipmentFaultKind.EmergencyStop, true, "EStop input forced on.");
        ClearEmergencyStopCommand = CreateFaultCommand(EquipmentFaultKind.EmergencyStop, false, "EStop input restored.");
        OpenDoorCommand = CreateFaultCommand(EquipmentFaultKind.DoorOpen, true, "Door input forced open.");
        CloseDoorCommand = CreateFaultCommand(EquipmentFaultKind.DoorOpen, false, "Door input restored closed.");
        DropAirPressureCommand = CreateFaultCommand(EquipmentFaultKind.AirPressureLow, true, "Air pressure input forced low.");
        RestoreAirPressureCommand = CreateFaultCommand(EquipmentFaultKind.AirPressureLow, false, "Air pressure input restored.");
        DropVacuumCommand = CreateFaultCommand(EquipmentFaultKind.VacuumLoss, true, "Vacuum sensor forced loss.");
        RestoreVacuumCommand = CreateFaultCommand(EquipmentFaultKind.VacuumLoss, false, "Vacuum sensor restored.");
        DisableCameraReadyCommand = CreateFaultCommand(EquipmentFaultKind.CameraNotReady, true, "Camera ready input forced off.");
        RestoreCameraReadyCommand = CreateFaultCommand(EquipmentFaultKind.CameraNotReady, false, "Camera ready input restored.");
        InjectServoAlarmCommand = CreateFaultCommand(EquipmentFaultKind.ServoAlarm, true, "Servo alarm input forced on.");
        ClearServoAlarmCommand = CreateFaultCommand(EquipmentFaultKind.ServoAlarm, false, "Servo alarm input restored.");
        ClearAllFaultsCommand = CreateFaultCommand(EquipmentFaultKind.ClearAll, false, "All simulator fault inputs restored.");

        ApplyEmptyState();
    }

    public ObservableCollection<IoBitStatusViewModel> IoBits { get; } = new();
    public ObservableCollection<EquipmentFaultStatusViewModel> Faults { get; } = new();
    public ObservableCollection<SystemEvent> Events { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand InjectEmergencyStopCommand { get; }
    public IAsyncRelayCommand ClearEmergencyStopCommand { get; }
    public IAsyncRelayCommand OpenDoorCommand { get; }
    public IAsyncRelayCommand CloseDoorCommand { get; }
    public IAsyncRelayCommand DropAirPressureCommand { get; }
    public IAsyncRelayCommand RestoreAirPressureCommand { get; }
    public IAsyncRelayCommand DropVacuumCommand { get; }
    public IAsyncRelayCommand RestoreVacuumCommand { get; }
    public IAsyncRelayCommand DisableCameraReadyCommand { get; }
    public IAsyncRelayCommand RestoreCameraReadyCommand { get; }
    public IAsyncRelayCommand InjectServoAlarmCommand { get; }
    public IAsyncRelayCommand ClearServoAlarmCommand { get; }
    public IAsyncRelayCommand ClearAllFaultsCommand { get; }

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private string _modeStatus = "Offline";

    [ObservableProperty]
    private string _safetyStatus = "Door Closed / EStop Off / Air OK / Vacuum OK / Servo Off";

    [ObservableProperty]
    private string _cameraStatus = "Camera Offline";

    [ObservableProperty]
    private string _alarmStatus = "Alarm: None";

    [ObservableProperty]
    private string _injectionStatus = "Connect the simulator before injecting faults.";

    [ObservableProperty]
    private string _lastSnapshotText = "Last snapshot: -";

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var result = await _dashboardUseCase.RefreshAsync(SnapshotTimeout, cancellationToken).ConfigureAwait(true);
        ApplySnapshotResult(result);
    }

    private IAsyncRelayCommand CreateFaultCommand(
        EquipmentFaultKind kind,
        bool isActive,
        string memo)
    {
        return new AsyncRelayCommand(
            cancellationToken => ApplyFaultAsync(kind, isActive, memo, cancellationToken),
            () => IsConnected);
    }

    private async Task ApplyFaultAsync(
        EquipmentFaultKind kind,
        bool isActive,
        string memo,
        CancellationToken cancellationToken)
    {
        var command = new EquipmentFaultInjectionCommand(
            kind,
            isActive,
            FaultCommandTimeout,
            SnapshotTimeout,
            memo);
        var result = await _faultInjectionUseCase.ApplyAsync(command, cancellationToken).ConfigureAwait(true);

        AddEvent(result.CommandEvent);
        InjectionStatus = result.CommandResult.Message;
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

    private void ApplySnapshot(EquipmentSnapshot snapshot)
    {
        IsConnected = snapshot.IsConnected;
        ConnectionStatus = snapshot.IsConnected ? "Connected" : "Disconnected";
        ModeStatus = snapshot.Mode.ToString();
        SafetyStatus = FormatSafety(snapshot);
        CameraStatus = snapshot.Camera.IsReady ? $"{snapshot.Camera.Name}: Ready" : $"{snapshot.Camera.Name}: Not Ready";
        AlarmStatus = snapshot.Alarm is null ? "Alarm: None" : $"Alarm: {snapshot.Alarm.ErrorCode.Code} {snapshot.Alarm.Message}";
        LastSnapshotText = $"Last snapshot: {snapshot.Timestamp:yyyy-MM-dd HH:mm:ss}";

        IoBits.Clear();
        foreach (var bit in snapshot.Io.Bits)
        {
            IoBits.Add(new IoBitStatusViewModel(bit.Name, bit.Address, bit.Direction, bit.Value, bit.IsForced));
        }

        Faults.Clear();
        Faults.Add(new EquipmentFaultStatusViewModel("EStop", snapshot.Safety.EmergencyStopActive ? "Active" : "Released", snapshot.Safety.EmergencyStopActive));
        Faults.Add(new EquipmentFaultStatusViewModel("Door", snapshot.Safety.DoorClosed ? "Closed" : "Open", !snapshot.Safety.DoorClosed));
        Faults.Add(new EquipmentFaultStatusViewModel("Air Pressure", snapshot.Safety.AirPressureOk ? "OK" : "Low", !snapshot.Safety.AirPressureOk));
        Faults.Add(new EquipmentFaultStatusViewModel("Vacuum", snapshot.Safety.VacuumOn ? "OK" : "Loss", !snapshot.Safety.VacuumOn));
        Faults.Add(new EquipmentFaultStatusViewModel("Camera Ready", snapshot.Camera.IsReady ? "Ready" : "Not Ready", !snapshot.Camera.IsReady));
        Faults.Add(new EquipmentFaultStatusViewModel("Servo Alarm", HasServoAlarm(snapshot) ? "Active" : "Clear", HasServoAlarm(snapshot)));

        NotifyFaultCommands();
    }

    private void ApplyEmptyState()
    {
        Faults.Clear();
        Faults.Add(new EquipmentFaultStatusViewModel("EStop", "Released", false));
        Faults.Add(new EquipmentFaultStatusViewModel("Door", "Closed", false));
        Faults.Add(new EquipmentFaultStatusViewModel("Air Pressure", "OK", false));
        Faults.Add(new EquipmentFaultStatusViewModel("Vacuum", "Unknown", false));
        Faults.Add(new EquipmentFaultStatusViewModel("Camera Ready", "Offline", true));
        Faults.Add(new EquipmentFaultStatusViewModel("Servo Alarm", "Clear", false));
        NotifyFaultCommands();
    }

    private static string FormatSafety(EquipmentSnapshot snapshot)
    {
        var safety = snapshot.Safety;
        var door = safety.DoorClosed ? "Door Closed" : "Door Open";
        var estop = safety.EmergencyStopActive ? "EStop On" : "EStop Off";
        var air = safety.AirPressureOk ? "Air OK" : "Air Low";
        var vacuum = safety.VacuumOn ? "Vacuum OK" : "Vacuum Loss";
        var servo = safety.ServoEnabled ? "Servo On" : "Servo Off";
        var servoAlarm = HasServoAlarm(snapshot) ? "Servo Alarm" : "Servo Clear";
        return $"{door} / {estop} / {air} / {vacuum} / {servo} / {servoAlarm}";
    }

    private static bool HasServoAlarm(EquipmentSnapshot snapshot)
    {
        return snapshot.Axes.Any(axis => axis.Alarm?.ErrorCode.Code == ErrorCode.ServoAlarm.Code);
    }

    private void AddEvent(SystemEvent systemEvent)
    {
        Events.Insert(0, systemEvent);
        while (Events.Count > 50)
        {
            Events.RemoveAt(Events.Count - 1);
        }
    }

    private void NotifyFaultCommands()
    {
        InjectEmergencyStopCommand.NotifyCanExecuteChanged();
        ClearEmergencyStopCommand.NotifyCanExecuteChanged();
        OpenDoorCommand.NotifyCanExecuteChanged();
        CloseDoorCommand.NotifyCanExecuteChanged();
        DropAirPressureCommand.NotifyCanExecuteChanged();
        RestoreAirPressureCommand.NotifyCanExecuteChanged();
        DropVacuumCommand.NotifyCanExecuteChanged();
        RestoreVacuumCommand.NotifyCanExecuteChanged();
        DisableCameraReadyCommand.NotifyCanExecuteChanged();
        RestoreCameraReadyCommand.NotifyCanExecuteChanged();
        InjectServoAlarmCommand.NotifyCanExecuteChanged();
        ClearServoAlarmCommand.NotifyCanExecuteChanged();
        ClearAllFaultsCommand.NotifyCanExecuteChanged();
    }
}
