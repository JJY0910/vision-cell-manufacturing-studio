using FluentAssertions;
using VisionCell.Application.Alarms;
using VisionCell.Application.Equipment;
using VisionCell.Application.Interlocks;
using VisionCell.Core.Alarms;
using VisionCell.Core.Commands;
using VisionCell.Core.Errors;
using VisionCell.Core.Events;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Alarms;
using VisionCell.Equipment.Cameras;
using VisionCell.Equipment.Controllers;
using VisionCell.Equipment.Faults;
using VisionCell.Equipment.Io;
using VisionCell.Equipment.Safety;
using VisionCell.Motion.Axes;
using Xunit;

namespace VisionCell_Application_Tests;

public sealed class EquipmentDashboardUseCaseTests
{
    [Fact]
    public async Task ConnectAsync_Should_Return_Command_Event_And_Refreshed_Snapshot()
    {
        var controller = new FakeEquipmentController(CreateSnapshot(connected: false));
        controller.ConnectHandler = (_, _) =>
        {
            controller.Snapshot = CreateSnapshot(connected: true);
            return Task.FromResult(MachineCommandResult.Success(
                "Virtual controller connected.",
                TimeSpan.FromMilliseconds(12),
                CorrelationId.New()));
        };
        var useCase = CreateUseCase(controller);

        var result = await useCase.ConnectAsync(
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        result.CommandResult.Status.Should().Be(CommandStatus.Success);
        result.CommandEvent.EventType.Should().Be("Connect");
        result.SnapshotResult.HasSnapshot.Should().BeTrue();
        result.SnapshotResult.Snapshot!.IsConnected.Should().BeTrue();
        result.SnapshotResult.Event.EventType.Should().Be("Snapshot");
    }

    [Fact]
    public async Task RefreshAsync_Should_Surface_Snapshot_Timeout_Event()
    {
        var controller = new FakeEquipmentController(CreateSnapshot());
        controller.SnapshotHandler = (_, _) => throw new OperationCanceledException("snapshot timeout");
        var useCase = CreateUseCase(controller);

        var result = await useCase.RefreshAsync(TimeSpan.FromMilliseconds(1), CancellationToken.None);

        result.HasSnapshot.Should().BeFalse();
        result.Event.EventType.Should().Be("SnapshotTimeout");
        result.Event.Severity.Should().Be(SystemEventSeverity.Alarm);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Call_Controller_And_Project_Command_Event()
    {
        var controller = new FakeEquipmentController(CreateSnapshot(connected: true, mode: MachineMode.Auto));
        var useCase = CreateUseCase(controller);

        var result = await useCase.ExecuteCommandAsync(
            CommandKind.EnterAutoMode,
            ReadyManualContext(),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        controller.LastCommand.Should().Be(CommandKind.EnterAutoMode);
        result.CommandResult.Status.Should().Be(CommandStatus.Success);
        result.CommandEvent.EventType.Should().Be("Enter Auto");
        result.SnapshotResult.Snapshot!.Mode.Should().Be(MachineMode.Auto);
    }

    [Fact]
    public void GetCommandAvailability_Should_Use_Application_Interlock_Service()
    {
        var useCase = CreateUseCase(new FakeEquipmentController(CreateSnapshot()));

        var availability = useCase.GetCommandAvailability(
            CommandKind.Home,
            ReadyManualContext() with { ServoOn = false });

        availability.IsEnabled.Should().BeFalse();
        availability.DisabledReason.Should().Contain("servo on");
    }

    [Fact]
    public async Task FaultInjectionUseCase_Should_Record_Alarm_And_Return_Refreshed_Snapshot()
    {
        var controller = new FakeEquipmentController(CreateSnapshot(
            connected: true,
            ioBits:
            [
                new IoBitSnapshot("DI_AIR_PRESSURE_OK", "X002", IoBitDirection.Input, true, false)
            ]));
        var faultInjector = new FakeFaultInjector
        {
            Handler = (request, _) =>
            {
                controller.Snapshot = CreateSnapshot(
                    connected: true,
                    mode: MachineMode.Alarm,
                    alarm: new AlarmSnapshot(ErrorCode.AirPressureLow, "Air pressure low fault is active.", DateTimeOffset.UtcNow),
                    ioBits:
                    [
                        new IoBitSnapshot("DI_AIR_PRESSURE_OK", "X002", IoBitDirection.Input, false, true)
                    ]);
                return Task.FromResult(MachineCommandResult.Success(
                    "Air Pressure Low fault injected.",
                    TimeSpan.FromMilliseconds(7),
                    request.CommandRequest.CorrelationId));
            }
        };
        var alarmRecorder = new FakeEquipmentAlarmRecorder();
        var transitionRepository = new FakeEquipmentIoTransitionRepository();
        var useCase = new EquipmentFaultInjectionUseCase(faultInjector, controller, alarmRecorder, transitionRepository);

        var result = await useCase.ApplyAsync(
            new EquipmentFaultInjectionCommand(
                EquipmentFaultKind.AirPressureLow,
                isActive: true,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromMilliseconds(500),
                "Air pressure input forced low."),
            CancellationToken.None);

        faultInjector.Requests.Should().ContainSingle();
        result.Request.Parameters.Should().Contain("FaultKind", "AirPressureLow");
        result.CommandResult.Status.Should().Be(CommandStatus.Success);
        result.CommandEvent.EventType.Should().Be("Fault Injection");
        result.SnapshotResult.Snapshot!.Mode.Should().Be(MachineMode.Alarm);
        alarmRecorder.Failures.Should().ContainSingle(failure =>
            failure.ErrorCode.Code == "EQP-008" &&
            failure.Area == EquipmentArea.Safety &&
            failure.CorrelationId == result.CommandResult.CorrelationId.ToString());
        transitionRepository.Transitions.Should().ContainSingle(transition =>
            transition.Name == "DI_AIR_PRESSURE_OK" &&
            transition.Address == "X002" &&
            transition.PreviousValue &&
            !transition.CurrentValue &&
            !transition.PreviousForced &&
            transition.CurrentForced &&
            transition.Source == "Fault Injection Air Pressure Low On" &&
            transition.CorrelationId == result.CommandResult.CorrelationId.ToString() &&
            transition.OperatorMemo == "Air pressure input forced low.");
    }

    private static EquipmentDashboardUseCase CreateUseCase(FakeEquipmentController controller)
    {
        return new EquipmentDashboardUseCase(controller, new CommandInterlockService());
    }

    private static InterlockContext ReadyManualContext()
    {
        return new InterlockContext(
            Connected: true,
            ControllerBusy: false,
            SequenceRunning: false,
            EmergencyStopActive: false,
            DoorClosed: true,
            SafetyOk: true,
            ManualMode: true,
            AutoMode: false,
            ServoOn: true,
            AxisHomed: true,
            AllRequiredAxesHomed: true,
            AxisBusy: false,
            AxisAlarm: false,
            WithinSoftLimit: true,
            RecipeLoaded: true,
            CameraConnected: true,
            IoReady: true,
            AlarmActive: false);
    }

    private static EquipmentSnapshot CreateSnapshot(
        bool connected = true,
        MachineMode mode = MachineMode.Manual,
        bool servoOn = true,
        AlarmSnapshot? alarm = null,
        IReadOnlyList<IoBitSnapshot>? ioBits = null)
    {
        var timestamp = new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);
        return new EquipmentSnapshot(
            connected,
            connected ? mode : MachineMode.Offline,
            new SafetySnapshot(DoorClosed: true, EmergencyStopActive: false, AirPressureOk: true, VacuumOn: true, ServoEnabled: servoOn),
            AxisDefaults.CreatePowerOffAxes().Select(axis => axis with { ServoOn = servoOn }).ToArray(),
            new IoSnapshot(ioBits ?? Array.Empty<IoBitSnapshot>(), timestamp),
            new CameraSnapshot(connected, "Virtual 3D camera", timestamp),
            alarm,
            timestamp);
    }

    private sealed class FakeEquipmentController : IEquipmentController
    {
        public FakeEquipmentController(EquipmentSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public EquipmentSnapshot Snapshot { get; set; }
        public CommandKind? LastCommand { get; private set; }

        public Func<TimeSpan, CancellationToken, Task<EquipmentSnapshot>>? SnapshotHandler { get; set; }
        public Func<TimeSpan, CancellationToken, Task<MachineCommandResult>>? ConnectHandler { get; set; }
        public Func<TimeSpan, CancellationToken, Task<MachineCommandResult>>? DisconnectHandler { get; init; }
        public Func<CommandKind, InterlockContext, TimeSpan, CancellationToken, Task<MachineCommandResult>>? ExecuteHandler { get; init; }

        public Task<EquipmentSnapshot> GetSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return SnapshotHandler is not null
                ? SnapshotHandler(timeout, cancellationToken)
                : Task.FromResult(Snapshot);
        }

        public Task<MachineCommandResult> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ConnectHandler is not null
                ? ConnectHandler(timeout, cancellationToken)
                : Task.FromResult(MachineCommandResult.Success("Connected.", TimeSpan.Zero, CorrelationId.New()));
        }

        public Task<MachineCommandResult> DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return DisconnectHandler is not null
                ? DisconnectHandler(timeout, cancellationToken)
                : Task.FromResult(MachineCommandResult.Success("Disconnected.", TimeSpan.Zero, CorrelationId.New()));
        }

        public CommandAvailability GetCommandAvailability(CommandKind command, InterlockContext context)
        {
            return CommandInterlockRules.Evaluate(command, context);
        }

        public Task<MachineCommandResult> ExecuteCommandAsync(
            CommandKind command,
            InterlockContext context,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            LastCommand = command;
            return ExecuteHandler is not null
                ? ExecuteHandler(command, context, timeout, cancellationToken)
                : Task.FromResult(MachineCommandResult.Success($"{command} completed.", TimeSpan.FromMilliseconds(10), CorrelationId.New()));
        }
    }

    private sealed class FakeFaultInjector : IEquipmentFaultInjector
    {
        public List<EquipmentFaultInjectionRequest> Requests { get; } = new();
        public Func<EquipmentFaultInjectionRequest, CancellationToken, Task<MachineCommandResult>>? Handler { get; init; }

        public Task<MachineCommandResult> ApplyFaultAsync(
            EquipmentFaultInjectionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Handler is not null
                ? Handler(request, cancellationToken)
                : Task.FromResult(MachineCommandResult.Success(
                    "Fault injection accepted.",
                    TimeSpan.Zero,
                    request.CommandRequest.CorrelationId));
        }
    }

    private sealed class FakeEquipmentAlarmRecorder : IEquipmentAlarmRecorder
    {
        public List<(ErrorCode ErrorCode, EquipmentArea Area, string Message, string? CorrelationId)> Failures { get; } = new();

        public Task RecordAsync(EquipmentAlarm alarm, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(
            ErrorCode errorCode,
            EquipmentArea area,
            string message,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            Failures.Add((errorCode, area, message, correlationId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEquipmentIoTransitionRepository : IEquipmentIoTransitionRepository
    {
        public List<IoTransitionRecord> Transitions { get; } = new();

        public Task SaveAsync(IoTransitionRecord transition, CancellationToken cancellationToken)
        {
            Transitions.Add(transition);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IoTransitionRecord>> ListRecentAsync(int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<IoTransitionRecord>>(Transitions.Take(limit).ToArray());
        }
    }
}
