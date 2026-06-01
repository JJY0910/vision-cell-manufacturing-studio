using FluentAssertions;
using VisionCell.Application.Motion;
using VisionCell.Core.Commands;
using VisionCell.Core.Errors;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Controllers;
using Xunit;

namespace VisionCell_Application_Tests;

public sealed class MotionCommandUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Call_Controller_And_Save_Correlated_History()
    {
        var controller = new FakeEquipmentController
        {
            ExecuteHandler = (_, _, _, _) => Task.FromResult(
                MachineCommandResult.Success("Move completed.", TimeSpan.FromMilliseconds(42), CorrelationId.New()))
        };
        var history = new CapturingMotionCommandHistoryRepository();
        var useCase = CreateUseCase(controller, history);
        var parameters = new Dictionary<string, string>
        {
            ["axis"] = "X",
            ["target"] = "10.000"
        };

        var execution = await useCase.ExecuteAsync(
            new MotionCommandExecutionRequest(CommandKind.MoveAbsolute, ReadyManualContext(), TimeSpan.FromSeconds(2), parameters),
            CancellationToken.None);

        controller.ExecuteCount.Should().Be(1);
        controller.LastCommand.Should().Be(CommandKind.MoveAbsolute);
        controller.LastTimeout.Should().Be(TimeSpan.FromSeconds(2));
        controller.LastRequest.Should().Be(execution.Request);
        execution.Request.CommandName.Should().Be("Move Absolute");
        execution.Request.Parameters.Should().Contain("axis", "X");
        execution.CommandResult.Status.Should().Be(CommandStatus.Success);
        execution.CommandResult.CorrelationId.Should().Be(execution.Request.CorrelationId);
        history.Entries.Should().ContainSingle();
        history.Entries[0].Request.Should().Be(execution.Request);
        history.Entries[0].CommandResult.Should().Be(execution.CommandResult);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Save_Rejected_Result_When_Controller_Rejects_Command()
    {
        var controller = new FakeEquipmentController
        {
            ExecuteHandler = (_, _, _, _) => Task.FromResult(
                MachineCommandResult.Rejected(ErrorCode.ServoOff, "Servo is off.", TimeSpan.Zero, CorrelationId.New()))
        };
        var history = new CapturingMotionCommandHistoryRepository();
        var useCase = CreateUseCase(controller, history);

        var execution = await useCase.ExecuteAsync(
            new MotionCommandExecutionRequest(CommandKind.Home, ReadyManualContext(), TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        execution.CommandResult.Status.Should().Be(CommandStatus.Rejected);
        execution.CommandResult.ErrorCode.Should().Be(ErrorCode.ServoOff);
        execution.CommandResult.CorrelationId.Should().Be(execution.Request.CorrelationId);
        history.Entries.Should().ContainSingle(entry => entry.CommandResult.Status == CommandStatus.Rejected);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Save_Timeout_Result_With_Request_Correlation()
    {
        var controller = new FakeEquipmentController
        {
            ExecuteHandler = (_, _, _, _) => Task.FromResult(
                MachineCommandResult.Timeout(ErrorCode.MotionTimeout, "Move timed out.", TimeSpan.FromSeconds(1), CorrelationId.New()))
        };
        var history = new CapturingMotionCommandHistoryRepository();
        var useCase = CreateUseCase(controller, history);

        var execution = await useCase.ExecuteAsync(
            new MotionCommandExecutionRequest(CommandKind.MoveAbsolute, ReadyManualContext(), TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        execution.CommandResult.Status.Should().Be(CommandStatus.Timeout);
        execution.CommandResult.ErrorCode.Should().Be(ErrorCode.MotionTimeout);
        execution.CommandResult.CorrelationId.Should().Be(execution.Request.CorrelationId);
        history.Entries.Should().ContainSingle(entry => entry.CommandResult.Status == CommandStatus.Timeout);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Reject_Unsupported_Command_Without_Controller_Call_And_Save_History()
    {
        var controller = new FakeEquipmentController();
        var history = new CapturingMotionCommandHistoryRepository();
        var useCase = CreateUseCase(controller, history);

        var execution = await useCase.ExecuteAsync(
            new MotionCommandExecutionRequest(CommandKind.RunInspection, ReadyManualContext(), TimeSpan.FromSeconds(1)),
            CancellationToken.None);

        controller.ExecuteCount.Should().Be(0);
        execution.Request.CommandName.Should().Be("Run Inspection");
        execution.CommandResult.Status.Should().Be(CommandStatus.Rejected);
        execution.CommandResult.ErrorCode.Should().Be(ErrorCode.CommandRejected);
        execution.CommandResult.Message.Should().Contain("not a motion command");
        history.Entries.Should().ContainSingle(entry => entry.CommandResult.Status == CommandStatus.Rejected);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Pass_Cancellation_Token_To_Controller_And_History()
    {
        var controllerToken = CancellationToken.None;
        var historyToken = CancellationToken.None;
        var controller = new FakeEquipmentController
        {
            ExecuteHandler = (_, _, _, token) =>
            {
                controllerToken = token;
                return Task.FromResult(
                    MachineCommandResult.Cancelled(ErrorCode.CommandCancelled, "Cancelled.", TimeSpan.FromMilliseconds(5), CorrelationId.New()));
            }
        };
        var history = new CapturingMotionCommandHistoryRepository
        {
            OnSave = (_, token) =>
            {
                historyToken = token;
                return Task.CompletedTask;
            }
        };
        var useCase = CreateUseCase(controller, history);
        using var cancellation = new CancellationTokenSource();

        await useCase.ExecuteAsync(
            new MotionCommandExecutionRequest(CommandKind.Stop, ReadyManualContext() with { AxisBusy = true }, TimeSpan.FromSeconds(1)),
            cancellation.Token);

        controllerToken.Should().Be(cancellation.Token);
        historyToken.Should().Be(cancellation.Token);
    }

    private static MotionCommandUseCase CreateUseCase(
        IEquipmentController controller,
        IMotionCommandHistoryRepository historyRepository)
    {
        return new MotionCommandUseCase(
            controller,
            historyRepository,
            () => new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
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

    private sealed class CapturingMotionCommandHistoryRepository : IMotionCommandHistoryRepository
    {
        public List<MotionCommandHistoryEntry> Entries { get; } = new();
        public Func<MotionCommandHistoryEntry, CancellationToken, Task>? OnSave { get; init; }

        public async Task SaveAsync(MotionCommandHistoryEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);

            if (OnSave is not null)
            {
                await OnSave(entry, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class FakeEquipmentController : IEquipmentController
    {
        public int ExecuteCount { get; private set; }
        public CommandKind? LastCommand { get; private set; }
        public TimeSpan? LastTimeout { get; private set; }
        public MachineCommandRequest? LastRequest { get; private set; }

        public Func<CommandKind, InterlockContext, TimeSpan, CancellationToken, Task<MachineCommandResult>> ExecuteHandler { get; init; } =
            (_, _, _, _) => Task.FromResult(
                MachineCommandResult.Success("OK", TimeSpan.Zero, CorrelationId.New()));

        public Task<EquipmentSnapshot> GetSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
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
            var request = new MachineCommandRequest(command.ToString(), CorrelationId.New(), timeout, DateTimeOffset.UtcNow, new Dictionary<string, string>());
            return ExecuteCommandAsync(command, context, request, cancellationToken);
        }

        public Task<MachineCommandResult> ExecuteCommandAsync(
            CommandKind command,
            InterlockContext context,
            MachineCommandRequest request,
            CancellationToken cancellationToken)
        {
            ExecuteCount++;
            LastCommand = command;
            LastTimeout = request.Timeout;
            LastRequest = request;
            return ExecuteHandler(command, context, request.Timeout, cancellationToken);
        }
    }
}

