using FluentAssertions;
using VisionCell.Application.Inspection;
using VisionCell.Application.Recipes;
using VisionCell.Core.Commands;
using VisionCell.Core.Errors;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Cameras;
using VisionCell.Equipment.Controllers;
using VisionCell.Equipment.Io;
using VisionCell.Equipment.Safety;
using VisionCell.Motion.Axes;
using Xunit;

namespace VisionCell_Application_Tests;

public sealed class InspectionRunUseCaseTests
{
    [Fact]
    public async Task RunAsync_Should_Submit_RunInspection_When_ActiveRecipe_And_Interlocks_Are_Ready()
    {
        var recipe = CreateRecipeIndexEntry("RCP-AUTO", "1.0.0");
        var controller = new FakeEquipmentController(CreateSnapshot(MachineMode.Auto, connected: true, servoOn: true, homed: true))
        {
            ExecuteHandler = (_, _, request, _) => Task.FromResult(
                MachineCommandResult.Success("Run Inspection accepted.", TimeSpan.FromMilliseconds(11), request.CorrelationId))
        };
        var useCase = CreateUseCase(new FakeActiveRecipeContext(ActiveRecipeContextResult.Success(recipe)), controller);
        var progress = new CapturingProgress();

        var result = await useCase.RunAsync(CreateRequest(), progress, CancellationToken.None);

        result.Status.Should().Be(InspectionRunStatus.Accepted);
        result.IsAccepted.Should().BeTrue();
        result.Recipe.Should().Be(recipe);
        result.Request.Should().NotBeNull();
        result.Request!.CommandName.Should().Be("Run Inspection");
        result.Request.Parameters.Should().Contain("RecipeId", "RCP-AUTO");
        result.CommandResult.Should().NotBeNull();
        result.CommandResult!.CorrelationId.Should().Be(result.Request.CorrelationId);
        result.CameraGrabResult.Should().NotBeNull();
        result.CameraGrabResult!.IsSuccess.Should().BeTrue();
        result.CameraGrabResult.Frame!.Metadata.Should().Contain("RecipeId", "RCP-AUTO");
        controller.ExecuteCount.Should().Be(1);
        controller.LastCommand.Should().Be(CommandKind.RunInspection);
        controller.LastContext!.RecipeLoaded.Should().BeTrue();
        controller.LastContext.AutoMode.Should().BeTrue();
        result.Steps.Should().Contain(step => step.Name == "Load Recipe" && step.Status == InspectionSequenceStepStatus.Success);
        result.Steps.Should().Contain(step => step.Name == "Safety Interlock" && step.Status == InspectionSequenceStepStatus.Success);
        result.Steps.Should().Contain(step => step.Name == "Start Sequence" && step.Status == InspectionSequenceStepStatus.Success);
        result.Steps.Should().Contain(step => step.Name == "Move To Camera" && step.Status == InspectionSequenceStepStatus.Skipped);
        result.Steps.Should().Contain(step => step.Name == "Grab Image" && step.Status == InspectionSequenceStepStatus.Success);
        progress.Updates.Should().Contain(update => update.Status == InspectionSequenceStepStatus.Running);
    }

    [Fact]
    public async Task RunAsync_Should_Fail_Grab_Image_When_Camera_Timeouts()
    {
        var recipe = CreateRecipeIndexEntry("RCP-CAM-TIMEOUT", "1.0.0");
        var controller = new FakeEquipmentController(CreateSnapshot(MachineMode.Auto, connected: true, servoOn: true, homed: true));
        var camera = new FakeCameraDevice
        {
            GrabHandler = request => Task.FromResult(CameraGrabResult.Timeout(
                "Virtual camera grab timed out after 10 ms.",
                TimeSpan.FromMilliseconds(10),
                request.CorrelationId))
        };
        var useCase = CreateUseCase(new FakeActiveRecipeContext(ActiveRecipeContextResult.Success(recipe)), controller, camera);

        var result = await useCase.RunAsync(CreateRequest() with { GrabTimeout = TimeSpan.FromMilliseconds(10) }, progress: null, CancellationToken.None);

        result.Status.Should().Be(InspectionRunStatus.CameraGrabFailed);
        result.Message.Should().Contain("timed out");
        result.CameraGrabResult.Should().NotBeNull();
        result.CameraGrabResult!.ErrorCode.Should().Be(ErrorCode.CameraGrabTimeout);
        controller.ExecuteCount.Should().Be(1);
        camera.Requests.Should().ContainSingle();
        camera.Requests[0].CorrelationId.Should().Be(result.Request!.CorrelationId);
        result.Steps.Should().Contain(step => step.Name == "Grab Image" && step.Status == InspectionSequenceStepStatus.Failed);
        result.Steps.Should().Contain(step => step.Name == "Inspect 2D" && step.Status == InspectionSequenceStepStatus.Skipped);
    }

    [Fact]
    public async Task RunAsync_Should_Reject_When_No_ActiveRecipe_Is_Selected()
    {
        var controller = new FakeEquipmentController(CreateSnapshot(MachineMode.Auto, connected: true, servoOn: true, homed: true));
        var useCase = CreateUseCase(new FakeActiveRecipeContext(ActiveRecipeContextResult.NotSelected()), controller);

        var result = await useCase.RunAsync(CreateRequest(), progress: null, CancellationToken.None);

        result.Status.Should().Be(InspectionRunStatus.ActiveRecipeNotSelected);
        result.Message.Should().Contain("No active recipe");
        controller.SnapshotCount.Should().Be(0);
        controller.ExecuteCount.Should().Be(0);
        result.Steps.Should().Contain(step => step.Name == "Load Recipe" && step.Status == InspectionSequenceStepStatus.Failed);
    }

    [Fact]
    public async Task RunAsync_Should_Reject_When_Equipment_Is_Not_In_Auto_Mode()
    {
        var recipe = CreateRecipeIndexEntry("RCP-MANUAL", "1.0.0");
        var controller = new FakeEquipmentController(CreateSnapshot(MachineMode.Manual, connected: true, servoOn: true, homed: true));
        var useCase = CreateUseCase(new FakeActiveRecipeContext(ActiveRecipeContextResult.Success(recipe)), controller);

        var result = await useCase.RunAsync(CreateRequest(), progress: null, CancellationToken.None);

        result.Status.Should().Be(InspectionRunStatus.InterlockRejected);
        result.Message.Should().Contain("Run Inspection requires Auto mode");
        controller.ExecuteCount.Should().Be(0);
        result.Steps.Should().Contain(step => step.Name == "Safety Interlock" && step.Status == InspectionSequenceStepStatus.Failed);
    }

    [Fact]
    public async Task RunAsync_Should_Return_Cancelled_When_Operator_Cancels()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var recipe = CreateRecipeIndexEntry("RCP-CANCEL", "1.0.0");
        var controller = new FakeEquipmentController(CreateSnapshot(MachineMode.Auto, connected: true, servoOn: true, homed: true));
        var useCase = CreateUseCase(new FakeActiveRecipeContext(ActiveRecipeContextResult.Success(recipe)), controller);

        var result = await useCase.RunAsync(CreateRequest(), progress: null, cancellation.Token);

        result.Status.Should().Be(InspectionRunStatus.CommandCancelled);
        result.Message.Should().Contain("cancelled");
        controller.ExecuteCount.Should().Be(0);
    }

    private static InspectionRunUseCase CreateUseCase(
        IActiveRecipeContext activeRecipeContext,
        IEquipmentController controller,
        ICameraDevice? cameraDevice = null)
    {
        return new InspectionRunUseCase(
            activeRecipeContext,
            controller,
            cameraDevice ?? new FakeCameraDevice(),
            () => new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private static InspectionRunRequest CreateRequest()
    {
        return new InspectionRunRequest(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
    }

    private static RecipeIndexEntry CreateRecipeIndexEntry(string recipeId, string version)
    {
        var timestamp = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        return new RecipeIndexEntry(
            Guid.NewGuid(),
            recipeId,
            version,
            $"{recipeId} Product",
            $@"assets\recipes\{recipeId}.v{version}.recipe.json",
            "0123456789abcdef",
            IsActive: true,
            IsValid: true,
            ValidationSummary: "Valid",
            CreatedAt: timestamp.AddMinutes(-10),
            UpdatedAt: timestamp);
    }

    private static EquipmentSnapshot CreateSnapshot(
        MachineMode mode,
        bool connected,
        bool servoOn,
        bool homed)
    {
        var timestamp = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var axes = AxisDefaults.CreatePowerOffAxes()
            .Select(axis => axis with
            {
                ServoOn = servoOn,
                IsHomed = homed,
                Target = 0.0,
                Position = 0.0
            })
            .ToArray();

        return new EquipmentSnapshot(
            connected,
            mode,
            new SafetySnapshot(DoorClosed: true, EmergencyStopActive: false, AirPressureOk: true, VacuumOn: false, ServoEnabled: servoOn),
            axes,
            new IoSnapshot(
                new[]
                {
                    new IoBitSnapshot("DI_DOOR_CLOSED", "X000", IoBitDirection.Input, true, false),
                    new IoBitSnapshot("DI_CAMERA_READY", "X004", IoBitDirection.Input, true, false)
                },
                timestamp),
            new CameraSnapshot(connected, "Virtual 3D camera", timestamp),
            null,
            timestamp);
    }

    private sealed class CapturingProgress : IProgress<InspectionSequenceStepRecord>
    {
        public List<InspectionSequenceStepRecord> Updates { get; } = new();

        public void Report(InspectionSequenceStepRecord value)
        {
            Updates.Add(value);
        }
    }

    private sealed class FakeActiveRecipeContext : IActiveRecipeContext
    {
        private readonly ActiveRecipeContextResult _result;

        public FakeActiveRecipeContext(ActiveRecipeContextResult result)
        {
            _result = result;
        }

        public Task<ActiveRecipeContextResult> GetActiveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeEquipmentController : IEquipmentController
    {
        private readonly EquipmentSnapshot _snapshot;

        public FakeEquipmentController(EquipmentSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public int SnapshotCount { get; private set; }
        public int ExecuteCount { get; private set; }
        public CommandKind? LastCommand { get; private set; }
        public InterlockContext? LastContext { get; private set; }

        public Func<CommandKind, InterlockContext, MachineCommandRequest, CancellationToken, Task<MachineCommandResult>> ExecuteHandler { get; init; } =
            (_, _, request, _) => Task.FromResult(
                MachineCommandResult.Success("Run Inspection accepted.", TimeSpan.Zero, request.CorrelationId));

        public Task<EquipmentSnapshot> GetSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            SnapshotCount++;
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
            return CommandInterlockRules.Evaluate(command, context);
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
            LastContext = context;
            return ExecuteHandler(command, context, request, cancellationToken);
        }
    }

    private sealed class FakeCameraDevice : ICameraDevice
    {
        public List<CameraGrabRequest> Requests { get; } = new();

        public Func<CameraGrabRequest, Task<CameraGrabResult>> GrabHandler { get; init; } =
            request => Task.FromResult(CameraGrabResult.Success(
                CreateCameraFrame(request),
                "Grabbed 16x12 Gray8 frame from Fake camera.",
                TimeSpan.FromMilliseconds(3),
                request.CorrelationId));

        public Task<CameraGrabResult> GrabAsync(CameraGrabRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            cancellationToken.ThrowIfCancellationRequested();
            return GrabHandler(request);
        }

        private static CameraFrame CreateCameraFrame(CameraGrabRequest request)
        {
            return new CameraFrame(
                "Fake camera",
                width: 16,
                height: 12,
                stride: 16,
                CameraPixelFormat.Gray8,
                Enumerable.Range(0, 16 * 12).Select(index => (byte)(index % 255)).ToArray(),
                request.RequestedAt,
                new Dictionary<string, string>
                {
                    ["RecipeId"] = request.RecipeId,
                    ["RecipeVersion"] = request.RecipeVersion
                });
        }
    }
}
