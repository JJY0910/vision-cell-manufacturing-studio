using VisionCell.Application.Alarms;
using VisionCell.Core.Alarms;
using VisionCell.Core.Commands;
using VisionCell.Core.Errors;
using VisionCell.Core.Events;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Controllers;
using VisionCell.Equipment.Faults;

namespace VisionCell.Application.Equipment;

public sealed class EquipmentFaultInjectionUseCase : IEquipmentFaultInjectionUseCase
{
    private readonly IEquipmentFaultInjector _faultInjector;
    private readonly IEquipmentController _controller;
    private readonly IEquipmentAlarmRecorder _alarmRecorder;
    private readonly Func<DateTimeOffset> _clock;

    public EquipmentFaultInjectionUseCase(
        IEquipmentFaultInjector faultInjector,
        IEquipmentController controller,
        IEquipmentAlarmRecorder? alarmRecorder = null,
        Func<DateTimeOffset>? clock = null)
    {
        _faultInjector = faultInjector ?? throw new ArgumentNullException(nameof(faultInjector));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _alarmRecorder = alarmRecorder ?? NoopEquipmentAlarmRecorder.Instance;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<EquipmentFaultInjectionResult> ApplyAsync(
        EquipmentFaultInjectionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = CreateRequest(command);
        var faultRequest = new EquipmentFaultInjectionRequest(
            command.Kind,
            command.IsActive,
            request,
            command.OperatorMemo);

        var result = await _faultInjector.ApplyFaultAsync(faultRequest, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && command.IsActive)
        {
            await RecordInjectedAlarmAsync(command, result, cancellationToken).ConfigureAwait(false);
        }

        var snapshotResult = await RefreshAsync(command.SnapshotTimeout, cancellationToken).ConfigureAwait(false);
        return new EquipmentFaultInjectionResult(
            request,
            result,
            result.ToSystemEvent("Equipment", "Fault Injection"),
            snapshotResult);
    }

    private MachineCommandRequest CreateRequest(EquipmentFaultInjectionCommand command)
    {
        var parameters = new Dictionary<string, string>
        {
            ["FaultKind"] = command.Kind.ToString(),
            ["IsActive"] = command.IsActive.ToString()
        };

        if (!string.IsNullOrWhiteSpace(command.OperatorMemo))
        {
            parameters["OperatorMemo"] = command.OperatorMemo!;
        }

        return new MachineCommandRequest(
            FormatCommand(command),
            CorrelationId.New(),
            command.CommandTimeout,
            _clock(),
            parameters);
    }

    private async Task<EquipmentDashboardSnapshotResult> RefreshAsync(
        TimeSpan snapshotTimeout,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _controller.GetSnapshotAsync(snapshotTimeout, cancellationToken).ConfigureAwait(false);
            return new EquipmentDashboardSnapshotResult(
                snapshot,
                SystemEvent.Create(SystemEventSeverity.Trace, "Equipment", "Snapshot", "Equipment snapshot refreshed."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EquipmentDashboardSnapshotResult(
                null,
                SystemEvent.Create(SystemEventSeverity.Warning, "Equipment", "SnapshotCancelled", "Snapshot refresh was cancelled."));
        }
        catch (OperationCanceledException)
        {
            return new EquipmentDashboardSnapshotResult(
                null,
                SystemEvent.Create(SystemEventSeverity.Alarm, "Equipment", "SnapshotTimeout", "Snapshot refresh timed out."));
        }
    }

    private Task RecordInjectedAlarmAsync(
        EquipmentFaultInjectionCommand command,
        MachineCommandResult result,
        CancellationToken cancellationToken)
    {
        var (errorCode, area) = MapAlarm(command.Kind);
        return _alarmRecorder.RecordFailureAsync(
            errorCode,
            area,
            result.Message,
            result.CorrelationId.ToString(),
            cancellationToken);
    }

    private static (ErrorCode ErrorCode, EquipmentArea Area) MapAlarm(EquipmentFaultKind kind)
    {
        return kind switch
        {
            EquipmentFaultKind.EmergencyStop => (ErrorCode.EmergencyStopActive, EquipmentArea.Safety),
            EquipmentFaultKind.DoorOpen => (ErrorCode.DoorOpen, EquipmentArea.Safety),
            EquipmentFaultKind.AirPressureLow => (ErrorCode.AirPressureLow, EquipmentArea.Safety),
            EquipmentFaultKind.VacuumLoss => (ErrorCode.VacuumLoss, EquipmentArea.Safety),
            EquipmentFaultKind.CameraNotReady => (ErrorCode.CameraNotReady, EquipmentArea.Camera),
            EquipmentFaultKind.ServoAlarm => (ErrorCode.ServoAlarm, EquipmentArea.Motion),
            _ => (ErrorCode.CommandRejected, EquipmentArea.Equipment)
        };
    }

    private static string FormatCommand(EquipmentFaultInjectionCommand command)
    {
        return command.Kind == EquipmentFaultKind.ClearAll
            ? "Fault Injection Clear All"
            : $"Fault Injection {FormatFault(command.Kind)} {(command.IsActive ? "On" : "Off")}";
    }

    private static string FormatFault(EquipmentFaultKind kind)
    {
        return kind switch
        {
            EquipmentFaultKind.EmergencyStop => "Emergency Stop",
            EquipmentFaultKind.DoorOpen => "Door Open",
            EquipmentFaultKind.AirPressureLow => "Air Pressure Low",
            EquipmentFaultKind.VacuumLoss => "Vacuum Loss",
            EquipmentFaultKind.CameraNotReady => "Camera Not Ready",
            EquipmentFaultKind.ServoAlarm => "Servo Alarm",
            EquipmentFaultKind.ClearAll => "Clear All",
            _ => kind.ToString()
        };
    }
}
