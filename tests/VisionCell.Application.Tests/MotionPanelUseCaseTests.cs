using FluentAssertions;
using VisionCell.Application.Interlocks;
using VisionCell.Application.Motion;
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
using Xunit;

namespace VisionCell_Application_Tests;

public sealed class MotionPanelUseCaseTests
{
    [Fact]
    public async Task RefreshSnapshotAsync_Should_Return_Snapshot_State()
    {
        var controller = new FakeEquipmentController(CreateSnapshot(connected: true, servoOn: true));
        var useCase = CreateUseCase(controller);

        var result = await useCase.RefreshSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        result.Status.Should().Be(MotionSnapshotRefreshStatus.Refreshed);
        result.HasSnapshot.Should().BeTrue();
        result.Snapshot!.IsConnected.Should().BeTrue();
        result.Message.Should().Be("Motion snapshot refreshed");
    }

    [Fact]
    public async Task RefreshSnapshotAsync_Should_Surface_Timeout_Result()
    {
        var controller = new FakeEquipmentController(CreateSnapshot())
        {
            SnapshotHandler = (_, _) => throw new OperationCanceledException("snapshot timeout")
        };
        var useCase = CreateUseCase(controller);

        var result = await useCase.RefreshSnapshotAsync(TimeSpan.FromMilliseconds(1), CancellationToken.None);

        result.Status.Should().Be(MotionSnapshotRefreshStatus.Timeout);
        result.HasSnapshot.Should().BeFalse();
        result.Message.Should().Be("Snapshot refresh timed out");
    }

    [Fact]
    public void GetCommandAvailability_Should_Use_Application_Interlock_Service()
    {
        var useCase = CreateUseCase(new FakeEquipmentController(CreateSnapshot()));

        var availability = useCase.GetCommandAvailability(
            CommandKind.Jog,
            ReadyManualContext() with { ServoOn = false });

        availability.IsEnabled.Should().BeFalse();
        availability.DisabledReason.Should().Contain("servo on");
    }

    private static MotionPanelUseCase CreateUseCase(FakeEquipmentController controller)
    {
        return new MotionPanelUseCase(controller, new CommandInterlockService());
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
        AlarmSnapshot? alarm = null)
    {
        var timestamp = new DateTimeOffset(2026, 6, 2, 10, 30, 0, TimeSpan.Zero);
        return new EquipmentSnapshot(
            connected,
            connected ? mode : MachineMode.Offline,
            new SafetySnapshot(DoorClosed: true, EmergencyStopActive: false, AirPressureOk: true, VacuumOn: false, ServoEnabled: servoOn),
            AxisDefaults.CreatePowerOffAxes().Select(axis => axis with { ServoOn = servoOn }).ToArray(),
            new IoSnapshot(Array.Empty<IoBitSnapshot>(), timestamp),
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
        public Func<TimeSpan, CancellationToken, Task<EquipmentSnapshot>>? SnapshotHandler { get; init; }

        public Task<EquipmentSnapshot> GetSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return SnapshotHandler is not null
                ? SnapshotHandler(timeout, cancellationToken)
                : Task.FromResult(Snapshot);
        }

        public Task<MachineCommandResult> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(MachineCommandResult.Success("Connected.", TimeSpan.Zero, CorrelationId.New()));
        }

        public Task<MachineCommandResult> DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(MachineCommandResult.Success("Disconnected.", TimeSpan.Zero, CorrelationId.New()));
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
            return Task.FromResult(MachineCommandResult.Success($"{command} completed.", TimeSpan.Zero, CorrelationId.New()));
        }
    }
}
