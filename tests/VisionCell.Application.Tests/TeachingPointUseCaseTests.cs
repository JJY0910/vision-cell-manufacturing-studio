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
        var historyRepository = new InMemoryTeachingHistoryRepository();
        var useCase = CreateUseCase(controller, repository, historyRepository: historyRepository);

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
        historyRepository.Entries.Should().ContainSingle();
        historyRepository.Entries[0].TeachingPointId.Should().Be(result.Point.Id);
        historyRepository.Entries[0].RecipeId.Should().BeNull();
        historyRepository.Entries[0].Action.Should().Be(TeachingHistoryAction.Created);
        historyRepository.Entries[0].BeforeJson.Should().BeNull();
        historyRepository.Entries[0].AfterJson.Should().Contain("\"name\":\"Camera Teach\"");
        historyRepository.Entries[0].AfterJson.Should().Contain("\"role\":\"Camera\"");
    }

    [Fact]
    public async Task ListAsync_Should_Delegate_To_Repository_With_Limit()
    {
        var repository = new InMemoryTeachingPointRepository();
        var point = TeachingPointFactory.Create(
            "Camera",
            TeachingRole.Camera,
            new Position4D(0.0, 0.0, 0.0, 0.0),
            PositionTolerance.Default).Point!;
        repository.Points[point.Id] = point;
        var useCase = CreateUseCase(new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0)), repository);

        var points = await useCase.ListAsync(5, CancellationToken.None);

        points.Should().ContainSingle().Which.Should().Be(point);
        repository.LastListLimit.Should().Be(5);
    }

    [Fact]
    public async Task SaveCurrentPositionAsync_Should_Write_Recipe_Context_To_History()
    {
        var controller = new SnapshotEquipmentController(CreateSnapshot(1.0, 2.0, 3.0, 4.0));
        var repository = new InMemoryTeachingPointRepository();
        var historyRepository = new InMemoryTeachingHistoryRepository();
        var useCase = CreateUseCase(controller, repository, historyRepository: historyRepository);

        var result = await useCase.SaveCurrentPositionAsync(
            new TeachingPointSaveRequest(
                "Camera",
                TeachingRole.Camera,
                PositionTolerance.Default,
                null,
                TimeSpan.FromSeconds(1),
                " RCP-001 "),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        historyRepository.Entries.Should().ContainSingle();
        historyRepository.Entries[0].RecipeId.Should().Be("RCP-001");
    }

    [Fact]
    public async Task ListAsync_Should_Reject_NonPositive_Limit()
    {
        var useCase = CreateUseCase(
            new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0)),
            new InMemoryTeachingPointRepository());

        var act = async () => await useCase.ListAsync(0, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
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
    public async Task SaveCurrentPositionAsync_Should_Not_Write_History_When_Duplicate_Name_Is_Rejected()
    {
        var controller = new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0));
        var repository = new InMemoryTeachingPointRepository();
        var historyRepository = new InMemoryTeachingHistoryRepository();
        var existing = TeachingPointFactory.Create(
            "Park",
            TeachingRole.Park,
            new Position4D(0.0, 0.0, 0.0, 0.0),
            PositionTolerance.Default).Point!;
        repository.Points[existing.Id] = existing;
        var useCase = CreateUseCase(controller, repository, historyRepository: historyRepository);

        var result = await useCase.SaveCurrentPositionAsync(
            new TeachingPointSaveRequest(" Park ", TeachingRole.Park, PositionTolerance.Default, null, TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        historyRepository.Entries.Should().BeEmpty();
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
    public async Task SaveCurrentPositionAsync_Should_Return_Repository_Failure_When_History_Save_Throws()
    {
        var controller = new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0));
        var repository = new InMemoryTeachingPointRepository();
        var historyRepository = new InMemoryTeachingHistoryRepository
        {
            SaveHandler = (_, _) => throw new InvalidOperationException("history unavailable")
        };
        var useCase = CreateUseCase(controller, repository, historyRepository: historyRepository);

        var result = await useCase.SaveCurrentPositionAsync(
            new TeachingPointSaveRequest("Load", TeachingRole.Load, PositionTolerance.Default, null, TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(TeachingPointOperationStatus.RepositoryUnavailable);
        result.Message.Should().Contain("history unavailable");
        repository.SavedPoints.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateAsync_Should_Save_Updated_Point_And_Write_History()
    {
        var existing = TeachingPointFactory.Create(
            "Camera",
            TeachingRole.Camera,
            new Position4D(1.0, 2.0, 3.0, 4.0),
            PositionTolerance.Default,
            "before",
            Guid.Parse("35404bf2-1c41-4d72-a753-b7cced61aebb"),
            DateTimeOffset.Parse("2026-06-01T06:00:00Z")).Point!;
        var repository = new InMemoryTeachingPointRepository();
        repository.Points[existing.Id] = existing;
        var historyRepository = new InMemoryTeachingHistoryRepository();
        var useCase = CreateUseCase(
            new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0)),
            repository,
            historyRepository: historyRepository);

        var result = await useCase.UpdateAsync(
            new TeachingPointUpdateRequest(
                existing.Id,
                " Camera Updated ",
                TeachingRole.Review,
                new Position4D(5.0, 6.0, 7.0, 8.0),
                new PositionTolerance(0.02, 0.03, 0.04, 0.05),
                " after ",
                "RCP-EDIT"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Point.Should().NotBeNull();
        result.Point!.Id.Should().Be(existing.Id);
        result.Point.Name.Should().Be("Camera Updated");
        result.Point.Role.Should().Be(TeachingRole.Review);
        result.Point.CreatedAt.Should().Be(existing.CreatedAt);
        result.Point.UpdatedAt.Should().Be(new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero));
        repository.Points[existing.Id].Should().Be(result.Point);
        historyRepository.Entries.Should().ContainSingle();
        historyRepository.Entries[0].RecipeId.Should().Be("RCP-EDIT");
        historyRepository.Entries[0].Action.Should().Be(TeachingHistoryAction.Updated);
        historyRepository.Entries[0].BeforeJson.Should().Contain("\"name\":\"Camera\"");
        historyRepository.Entries[0].AfterJson.Should().Contain("\"name\":\"Camera Updated\"");
    }

    [Fact]
    public async Task UpdateAsync_Should_Reject_Duplicate_Name_Belonging_To_Another_Point()
    {
        var point = TeachingPointFactory.Create(
            "Camera",
            TeachingRole.Camera,
            new Position4D(1.0, 2.0, 3.0, 4.0),
            PositionTolerance.Default).Point!;
        var duplicate = TeachingPointFactory.Create(
            "Review",
            TeachingRole.Review,
            new Position4D(5.0, 6.0, 7.0, 8.0),
            PositionTolerance.Default).Point!;
        var repository = new InMemoryTeachingPointRepository();
        repository.Points[point.Id] = point;
        repository.Points[duplicate.Id] = duplicate;
        var historyRepository = new InMemoryTeachingHistoryRepository();
        var useCase = CreateUseCase(
            new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0)),
            repository,
            historyRepository: historyRepository);

        var result = await useCase.UpdateAsync(
            new TeachingPointUpdateRequest(
                point.Id,
                "review",
                TeachingRole.Camera,
                point.Position,
                point.Tolerance,
                null),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(TeachingPointOperationStatus.ValidationFailed);
        result.ValidationIssues.Should().Contain(issue => issue.Code == "TeachingPoint.NameDuplicate");
        historyRepository.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_Should_Return_NotFound_When_Point_Is_Missing()
    {
        var repository = new InMemoryTeachingPointRepository();
        var historyRepository = new InMemoryTeachingHistoryRepository();
        var useCase = CreateUseCase(
            new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0)),
            repository,
            historyRepository: historyRepository);

        var result = await useCase.UpdateAsync(
            new TeachingPointUpdateRequest(
                Guid.NewGuid(),
                "Missing",
                TeachingRole.Camera,
                new Position4D(1.0, 2.0, 3.0, 4.0),
                PositionTolerance.Default,
                null),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(TeachingPointOperationStatus.NotFound);
        historyRepository.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_Should_Delete_Point_And_Write_History()
    {
        var point = TeachingPointFactory.Create(
            "Delete Me",
            TeachingRole.Park,
            new Position4D(1.0, 2.0, 3.0, 4.0),
            PositionTolerance.Default).Point!;
        var repository = new InMemoryTeachingPointRepository();
        repository.Points[point.Id] = point;
        var historyRepository = new InMemoryTeachingHistoryRepository();
        var useCase = CreateUseCase(
            new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0)),
            repository,
            historyRepository: historyRepository);

        var result = await useCase.DeleteAsync(new TeachingPointDeleteRequest(point.Id, "RCP-DELETE"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.DeletedPoint.Should().Be(point);
        repository.Points.Should().NotContainKey(point.Id);
        repository.DeletedIds.Should().ContainSingle().Which.Should().Be(point.Id);
        historyRepository.Entries.Should().ContainSingle();
        historyRepository.Entries[0].RecipeId.Should().Be("RCP-DELETE");
        historyRepository.Entries[0].Action.Should().Be(TeachingHistoryAction.Deleted);
        historyRepository.Entries[0].BeforeJson.Should().Contain("\"name\":\"Delete Me\"");
        historyRepository.Entries[0].AfterJson.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Should_Return_NotFound_When_Point_Is_Missing()
    {
        var repository = new InMemoryTeachingPointRepository();
        var historyRepository = new InMemoryTeachingHistoryRepository();
        var useCase = CreateUseCase(
            new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0)),
            repository,
            historyRepository: historyRepository);

        var result = await useCase.DeleteAsync(new TeachingPointDeleteRequest(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(TeachingPointOperationStatus.NotFound);
        repository.DeletedIds.Should().BeEmpty();
        historyRepository.Entries.Should().BeEmpty();
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
            new TeachingPointGoToRequest(point.Id, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Point.Should().Be(point);
        motion.Requests.Should().ContainSingle();
        motion.Requests[0].Command.Should().Be(CommandKind.MoveAbsolute);
        motion.Requests[0].Timeout.Should().Be(TimeSpan.FromSeconds(2));
        motion.Requests[0].InterlockContext.Connected.Should().BeTrue();
        motion.Requests[0].InterlockContext.ServoOn.Should().BeTrue();
        motion.Requests[0].InterlockContext.AllRequiredAxesHomed.Should().BeTrue();
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
            new TeachingPointGoToRequest(Guid.NewGuid(), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(TeachingPointOperationStatus.NotFound);
        motion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GoToAsync_Should_Return_SnapshotUnavailable_When_Snapshot_Read_Fails()
    {
        var point = TeachingPointFactory.Create(
            "Review",
            TeachingRole.Review,
            new Position4D(11.0, 12.0, 13.0, 14.0),
            PositionTolerance.Default).Point!;
        var repository = new InMemoryTeachingPointRepository();
        repository.Points[point.Id] = point;
        var motion = new CapturingMotionCommandUseCase();
        var controller = new SnapshotEquipmentController(CreateSnapshot(0.0, 0.0, 0.0, 0.0))
        {
            SnapshotHandler = (_, _) => throw new InvalidOperationException("controller unavailable")
        };
        var useCase = CreateUseCase(controller, repository, motion);

        var result = await useCase.GoToAsync(
            new TeachingPointGoToRequest(point.Id, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(TeachingPointOperationStatus.SnapshotUnavailable);
        result.Point.Should().Be(point);
        result.Message.Should().Contain("controller unavailable");
        motion.Requests.Should().BeEmpty();
    }

    private static TeachingPointUseCase CreateUseCase(
        IEquipmentController controller,
        ITeachingPointRepository repository,
        IMotionCommandUseCase? motionCommandUseCase = null,
        ITeachingHistoryRepository? historyRepository = null)
    {
        return new TeachingPointUseCase(
            controller,
            repository,
            historyRepository ?? new InMemoryTeachingHistoryRepository(),
            motionCommandUseCase ?? new CapturingMotionCommandUseCase(),
            () => new DateTimeOffset(2026, 6, 1, 7, 0, 0, TimeSpan.Zero));
    }

    private sealed class InMemoryTeachingHistoryRepository : ITeachingHistoryRepository
    {
        public List<TeachingHistoryEntry> Entries { get; } = new();
        public Func<TeachingHistoryEntry, CancellationToken, Task>? SaveHandler { get; init; }

        public async Task SaveAsync(TeachingHistoryEntry entry, CancellationToken cancellationToken)
        {
            if (SaveHandler is not null)
            {
                await SaveHandler(entry, cancellationToken).ConfigureAwait(false);
                return;
            }

            Entries.Add(entry);
        }

        public Task<IReadOnlyList<TeachingHistoryEntry>> ListByPointAsync(
            Guid teachingPointId,
            int limit,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TeachingHistoryEntry>>(
                Entries
                    .Where(entry => entry.TeachingPointId == teachingPointId)
                    .Take(limit)
                    .ToArray());
        }
    }

    private static EquipmentSnapshot CreateSnapshot(double x, double y, double z, double theta)
    {
        var axes = AxisDefaults.CreatePowerOffAxes()
            .Select(axis => axis with
            {
                ServoOn = true,
                IsHomed = true,
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

    private sealed class InMemoryTeachingPointRepository : ITeachingPointRepository
    {
        public Dictionary<Guid, TeachingPoint> Points { get; } = new();
        public List<TeachingPoint> SavedPoints { get; } = new();
        public List<Guid> DeletedIds { get; } = new();
        public int? LastListLimit { get; private set; }
        public Func<TeachingPoint, CancellationToken, Task>? SaveHandler { get; init; }

        public Task<IReadOnlyList<TeachingPoint>> ListAsync(int limit, CancellationToken cancellationToken)
        {
            LastListLimit = limit;
            return Task.FromResult<IReadOnlyList<TeachingPoint>>(Points.Values.Take(limit).ToArray());
        }

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

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            Points.Remove(id);
            DeletedIds.Add(id);
            return Task.CompletedTask;
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

        public Func<TimeSpan, CancellationToken, Task<EquipmentSnapshot>>? SnapshotHandler { get; init; }

        public Task<EquipmentSnapshot> GetSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return SnapshotHandler is not null
                ? SnapshotHandler(timeout, cancellationToken)
                : Task.FromResult(_snapshot);
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
