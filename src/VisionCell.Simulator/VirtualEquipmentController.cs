using System.Diagnostics;
using VisionCell.Core.Commands;
using VisionCell.Core.Errors;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Alarms;
using VisionCell.Equipment.Cameras;
using VisionCell.Equipment.Controllers;
using VisionCell.Equipment.Io;
using VisionCell.Equipment.Safety;
using VisionCell.Motion.Axes;

namespace VisionCell.Simulator;

public sealed class VirtualEquipmentController : IEquipmentController
{
    private static readonly TimeSpan SnapshotLatency = TimeSpan.FromMilliseconds(50);
    private bool _connected;
    private AlarmSnapshot? _alarm;

    public async Task<EquipmentSnapshot> GetSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        await Task.Delay(SnapshotLatency, cts.Token).ConfigureAwait(false);

        return CreateSnapshot(DateTimeOffset.UtcNow);
    }

    public Task<MachineCommandResult> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return RunControllerCommandAsync(
            "Connect",
            TimeSpan.FromMilliseconds(150),
            timeout,
            cancellationToken,
            () =>
            {
                _connected = true;
                _alarm = null;
                return "Virtual controller connected.";
            });
    }

    public Task<MachineCommandResult> DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return RunControllerCommandAsync(
            "Disconnect",
            TimeSpan.FromMilliseconds(50),
            timeout,
            cancellationToken,
            () =>
            {
                _connected = false;
                return "Virtual controller disconnected.";
            });
    }

    public CommandAvailability GetCommandAvailability(CommandKind command, InterlockContext context)
    {
        return CommandInterlockRules.Evaluate(command, context);
    }

    public Task<MachineCommandResult> ExecuteCommandAsync(CommandKind command, InterlockContext context, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var availability = GetCommandAvailability(command, context);
        if (!availability.IsEnabled)
        {
            return Task.FromResult(new MachineCommandResult(
                CommandStatus.Rejected,
                ErrorCode.CommandRejected,
                availability.DisabledReason,
                TimeSpan.Zero,
                CorrelationId.New()));
        }

        return command switch
        {
            CommandKind.Connect => ConnectAsync(timeout, cancellationToken),
            CommandKind.Disconnect => DisconnectAsync(timeout, cancellationToken),
            _ => Task.FromResult(new MachineCommandResult(
                CommandStatus.Success,
                null,
                $"{command} interlock accepted. Hardware execution is deferred beyond Phase 1.",
                TimeSpan.Zero,
                CorrelationId.New()))
        };
    }

    private async Task<MachineCommandResult> RunControllerCommandAsync(
        string commandName,
        TimeSpan latency,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<string> applyState)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        var correlationId = CorrelationId.New();

        try
        {
            await Task.Delay(latency, cts.Token).ConfigureAwait(false);
            var message = applyState();
            return new MachineCommandResult(CommandStatus.Success, null, message, sw.Elapsed, correlationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new MachineCommandResult(
                CommandStatus.Cancelled,
                ErrorCode.CommandCancelled,
                $"{commandName} was cancelled.",
                sw.Elapsed,
                correlationId);
        }
        catch (OperationCanceledException)
        {
            _alarm = new AlarmSnapshot(ErrorCode.CommandTimeout, $"{commandName} exceeded {timeout.TotalMilliseconds:0} ms.", DateTimeOffset.UtcNow);
            return new MachineCommandResult(
                CommandStatus.Timeout,
                ErrorCode.CommandTimeout,
                $"{commandName} timed out after {timeout.TotalMilliseconds:0} ms.",
                sw.Elapsed,
                correlationId);
        }
    }

    private EquipmentSnapshot CreateSnapshot(DateTimeOffset timestamp)
    {
        var safety = new SafetySnapshot(
            DoorClosed: true,
            EmergencyStopActive: false,
            AirPressureOk: true,
            VacuumOn: false,
            ServoEnabled: false);

        return new EquipmentSnapshot(
            _connected,
            _connected ? MachineMode.Manual : MachineMode.Offline,
            safety,
            AxisDefaults.CreatePowerOffAxes(),
            CreateIoSnapshot(timestamp),
            new CameraSnapshot(_connected, "Virtual 3D camera", timestamp),
            _alarm,
            timestamp);
    }

    private static IoSnapshot CreateIoSnapshot(DateTimeOffset timestamp)
    {
        return new IoSnapshot(
            new[]
            {
                new IoBitSnapshot("DI_DOOR_CLOSED", "X000", IoBitDirection.Input, true, false),
                new IoBitSnapshot("DI_ESTOP_ON", "X001", IoBitDirection.Input, false, false),
                new IoBitSnapshot("DI_AIR_PRESSURE_OK", "X002", IoBitDirection.Input, true, false),
                new IoBitSnapshot("DI_PRODUCT_PRESENT", "X003", IoBitDirection.Input, true, false),
                new IoBitSnapshot("DI_CAMERA_READY", "X004", IoBitDirection.Input, true, false),
                new IoBitSnapshot("DO_SERVO_ENABLE", "Y000", IoBitDirection.Output, false, false),
                new IoBitSnapshot("DO_VACUUM_ON", "Y001", IoBitDirection.Output, false, false),
                new IoBitSnapshot("DO_RING_LIGHT_ON", "Y002", IoBitDirection.Output, false, false),
                new IoBitSnapshot("DO_BUZZER_ON", "Y003", IoBitDirection.Output, false, false),
                new IoBitSnapshot("DO_TOWER_GREEN", "Y004", IoBitDirection.Output, false, false),
                new IoBitSnapshot("DO_TOWER_YELLOW", "Y005", IoBitDirection.Output, false, false),
                new IoBitSnapshot("DO_TOWER_RED", "Y006", IoBitDirection.Output, false, false)
            },
            timestamp);
    }
}
