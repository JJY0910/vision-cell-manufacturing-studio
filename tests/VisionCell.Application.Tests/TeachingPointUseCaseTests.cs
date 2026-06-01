using FluentAssertions;
using VisionCell.Application.Motion;
using VisionCell.Application.Teaching;
using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Alarms;
using VisionCell.Equipment.Cameras;
using VisionCell.Equipment.Controllers;
using VisionCell.Equipment.Io;
using VisionCell.Equipment.Safety;
using VisionCell.Motion.Axes;
using VisionCell.Motion.Teaching;
using Xunit;

namespace VisionCell_Application_Tests;

public sealed class TeachingPointUseCaseTests
{
    [Fact]
    public async Task SaveCurrentPositionAsync_Should_Save_Point_From_Current_Axis_Snapshot()
    {
        var controller = new SnapshotEquipmentController(CreateSnapshot(10.0, 20.0, 30.0, 40.0));
        var repository = new InMemoryTeachingPointRepository();
        var useCase = CreateUseCase(controller, repository);

        var result = await useCase.SaveCurrentPositionAsync(
            new TeachingPointSaveRequest(
                " Camera Teach ",
                TeachingRole.Camera,
                new PositionTolerance(0.01, 0.02, 0.03, 0.04),
                " first pass ",
                TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Point.Should().NotBeNull();
        result.Point!.Name.Should().Be("Camera Teach");
        result.Point.Position.Should().Be(new Position4D(10.0, 20.0, 30.0, 40.0));
        result.Point.Tolerance.Should().Be(new PositionTolerance(0.01, 0.02, 0.03, 0.04));
        result.Point.Memo.Should().Be("first pass");
        result.Point.CreatedAt.Should().Be(new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero));
        repository.SavedPoints.Should().ContainSingle().Which.Should().Be(result.Point);
    }

    [Fact]
    public async Task SaveCurrentPositionAsync_Should_Reject_Duplicate_Name_Before_Save()
    {
        var controller = new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0));
        var repository = new InMemoryTeachingPointRepository();
        var existing = TeachingPointFactory.Create(
            "Park",
            TeachingRole.Park,
            new Position4D(0.0, 0.0, 0.0, 0.0),
            PositionTolerance.Default).Point!;
        repository.Points[existing.Id] = existing;
        var useCase = CreateUseCase(controller, repository);

        var result = await useCase.SaveCurrentPositionAsync(
            new TeachingPointSaveRequest(" Park ", TeachingRole.Park, PositionTolerance.Default, null, TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(TeachingPointOperationStatus.ValidationFailed);
        result.ValidationIssues.Should().Contain(issue => issue.Code == "TeachingPoint.NameDuplicate");
        repository.SavedPoints.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveCurrentPositionAsync_Should_Return_Validation_When_Axis_Snapshot_Is_Missing()
    {
        var controller = new SnapshotEquipmentController(CreateSnapshot(1.0, 2.0, 3.0, 4.0) with
        {
            Axes = AxisDefaults.CreatePowerOffAxes().Where(axis => axis.AxisId != AxisId.Theta).ToArray()
        });
        var repository = new InMemoryTeachingPointRepository();
        var useCase = CreateUseCase(controller, repository);

        var result = await useCase.SaveCurrentPositionAsync(
            new TeachingPointSaveRequest("Missing Theta", TeachingRole.Safe, PositionTolerance.Default, null, TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(TeachingPointOperationStatus.ValidationFailed);
        result.ValidationIssues.Should().Contain(issue => issue.Code == "TeachingPoint.AxisSnapshotMissing");
        repository.SavedPoints.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveCurrentPositionAsync_Should_Return_Repository_Failure_When_Save_Throws()
    {
        var controller = new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0));
        var repository = new InMemoryTeachingPointRepository
        {
            SaveHandler = (_, _) => throw new InvalidOperationException("database unavailable")
        };
        var useCase = CreateUseCase(controller, repository);

        var result = await useCase.SaveCurrentPositionAsync(
            new TeachingPointSaveRequest("Load", TeachingRole.Load, PositionTolerance.Default, null, TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(TeachingPointOperationStatus.RepositoryUnavailable);
        result.Message.Should().Contain("database unavailable");
    }

    [Fact]
    public async Task GoToAsync_Should_Load_Point_And_Dispatch_MoveAbsolute_Command()
    {
        var point = TeachingPointFactory.Create(
            "Review",
            TeachingRole.Review,
            new Position4D(11.0, 12.0, 13.0, 14.0),
            new PositionTolerance(0.05, 0.04, 0.03, 0.02)).Point!;
        var repository = new InMemoryTeachingPointRepository();
        repository.Points[point.Id] = point;
        var motion = new CapturingMotionCommandUseCase();
        var useCase = CreateUseCase(new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0)), repository, motion);

        var result = await useCase.GoToAsync(
            new TeachingPointGoToRequest(point.Id, ReadyManualContext(), TimeSpan.FromSeconds(2)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Point.Should().Be(point);
        motion.Requests.Should().ContainSingle();
        motion.Requests[0].Command.Should().Be(CommandKind.MoveAbsolute);
        motion.Requests[0].Timeout.Should().Be(TimeSpan.FromSeconds(2));
        motion.Requests[0].Parameters.Should().Contain("X", "11");
        motion.Requests[0].Parameters.Should().Contain("Y", "12");
        motion.Requests[0].Parameters.Should().Contain("Z", "13");
        motion.Requests[0].Parameters.Should().Contain("Theta", "14");
        motion.Requests[0].Parameters.Should().Contain("ArrivalTolerance", "0.02");
        motion.Requests[0].Parameters.Should().Contain("ProfilePreset", "Standard");
    }

    [Fact]
    public async Task GoToAsync_Should_Return_NotFound_When_Point_Is_Missing()
    {
        var repository = new InMemoryTeachingPointRepository();
        var motion = new CapturingMotionCommandUseCase();
        var useCase = CreateUseCase(new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0)), repository, motion);

        var result = await useCase.GoToAsync(
            new TeachingPointGoToRequest(Guid.NewGuid(), ReadyManualContext(), TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(TeachingPointOperationStatus.NotFound);
        motion.Requests.Should().BeEmpty();
    }

    private static TeachingPointUseCase CreateUseCase(
        IEquipmentController controller,
        ITeachingPointRepository repository,
        IMotionCommandUseCase? motionCommandUseCase = null)
    {
        return new TeachingPointUseCase(
            controller,
            repository,
            motionCommandUseCase ?? new CapturingMotionCommandUseCase(),
            () => new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero));
    }

    private static EquipmentSnapshot CreateSnapshot(double x, double y, double z, double theta)
    {
        var axes = AxisDefaults.CreatePowerOffAxes()
            .Select(axis => axis with
            {
                Position = axis.AxisId switch
                {
                    AxisId.X => x,
                    AxisId.Y => y,
                    AxisId.Z => z,
                    AxisId.Theta => theta,
                    _ => axis.Position
                }
            })
            .ToArray();
        var timestamp = new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero);

        return new EquipmentSnapshot(
            IsConnected: true,
            MachineMode.Manual,
            new SafetySnapshot(
                DoorClosed: true,
                EmergencyStopActive: false,
                AirPressureOk: true,
                VacuumOn: false,
                ServoEnabled: true),
            axes,
            new IoSnapshot(Array.Empty<IoBitSnapshot>(), timestamp),
            new CameraSnapshot(true, "Virtual camera", timestamp),
            Alarm: null,
            timestamp);
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

    private sealed class InMemoryTeachingPointRepository : ITeachingPointRepository
    {
        public Dictionary<Guid, TeachingPoint> Points { get; } = new();
        public List<TeachingPoint> SavedPoints { get; } = new();
        public Func<TeachingPoint, CancellationToken, Task>? SaveHandler { get; init; }

        public Task<TeachingPoint?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Points.TryGetValue(id, out var point) ? point : null);
        }

        public Task<TeachingPoint?> FindByNameAsync(string name, CancellationToken cancellationToken)
        {
            var point = Points.Values.SingleOrDefault(
                item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(point);
        }

        public async Task SaveAsync(TeachingPoint point, CancellationToken cancellationToken)
        {
            if (SaveHandler is not null)
            {
                await SaveHandler(point, cancellationToken).ConfigureAwait(false);
                return;
            }

            Points[point.Id] = point;
            SavedPoints.Add(point);
        }
    }

    private sealed class CapturingMotionCommandUseCase : IMotionCommandUseCase
    {
        public List<MotionCommandExecutionRequest> Requests { get; } = new();

        public Task<MotionCommandExecutionResult> ExecuteAsync(
            MotionCommandExecutionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var commandRequest = new MachineCommandRequest(
                "Move Absolute",
                CorrelationId.New(),
                request.Timeout,
                DateTimeOffset.UtcNow,
                request.GetParameters());
            var result = MachineCommandResult.Success("Move completed.", TimeSpan.FromMilliseconds(25), commandRequest.CorrelationId);
            return Task.FromResult(new MotionCommandExecutionResult(commandRequest, result));
        }
    }

    private sealed class SnapshotEquipmentController : IEquipmentController
    {
        private readonly EquipmentSnapshot _snapshot;

        public SnapshotEquipmentController(EquipmentSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<EquipmentSnapshot> GetSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(_snapshot);
        }

        public Task<MachineCommandResult> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<MachineCommandResult> DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public CommandAvailability GetCommandAvailability(CommandKind command, InterlockContext context)
        {
            return CommandAvailability.Available(command);
        }

        public Task<MachineCommandResult> ExecuteCommandAsync(
            CommandKind command,
            InterlockContext context,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<MachineCommandResult> ExecuteCommandAsync(
            CommandKind command,
            InterlockContext context,
            MachineCommandRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
