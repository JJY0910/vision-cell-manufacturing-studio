using FluentAssertions;
using VisionCell.Application.Inspection;
using VisionCell.Application.Motion;
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
using VisionCell.Motion.Teaching;
using VisionCell.Vision.Inspection;
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
        var motion = new FakeMotionCommandUseCase();
        var vision = new FakeVisionInspectionEngine();
        var useCase = CreateUseCase(
            new FakeActiveRecipeContext(ActiveRecipeContextResult.Success(recipe)),
            controller,
            motionCommandUseCase: motion,
            visionInspectionEngine: vision);
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
        result.MoveToCameraResult.Should().NotBeNull();
        result.MoveToCameraResult!.CommandResult.IsSuccess.Should().BeTrue();
        result.CameraGrabResult.Should().NotBeNull();
        result.CameraGrabResult!.IsSuccess.Should().BeTrue();
        result.CameraGrabResult.Frame!.Metadata.Should().Contain("RecipeId", "RCP-AUTO");
        result.VisionResult.Should().NotBeNull();
        result.VisionResult!.Judgment.Should().Be(Judgment.Pass);
        vision.Requests.Should().ContainSingle();
        vision.Requests[0].RecipeId.Should().Be("RCP-AUTO");
        vision.Requests[0].Rois.Should().ContainSingle(roi => roi.Id == "ROI-01");
        motion.Requests.Should().ContainSingle();
        motion.Requests[0].Command.Should().Be(CommandKind.SequenceMoveToCamera);
        motion.Requests[0].InterlockContext.AutoMode.Should().BeTrue();
        motion.Requests[0].InterlockContext.SequenceRunning.Should().BeTrue();
        motion.Requests[0].GetParameters().Should().Contain("X", "10");
        motion.Requests[0].GetParameters().Should().Contain("ParentCorrelationId", result.Request.CorrelationId.ToString());
        controller.ExecuteCount.Should().Be(1);
        controller.LastCommand.Should().Be(CommandKind.RunInspection);
        controller.LastContext!.RecipeLoaded.Should().BeTrue();
        controller.LastContext.AutoMode.Should().BeTrue();
        result.Steps.Should().Contain(step => step.Name == "Load Recipe" && step.Status == InspectionSequenceStepStatus.Success);
        result.Steps.Should().Contain(step => step.Name == "Safety Interlock" && step.Status == InspectionSequenceStepStatus.Success);
        result.Steps.Should().Contain(step => step.Name == "Start Sequence" && step.Status == InspectionSequenceStepStatus.Success);
        result.Steps.Should().Contain(step => step.Name == "Move To Camera" && step.Status == InspectionSequenceStepStatus.Success);
        result.Steps.Should().Contain(step => step.Name == "Grab Image" && step.Status == InspectionSequenceStepStatus.Success);
        result.Steps.Should().Contain(step => step.Name == "Inspect 2D" && step.Status == InspectionSequenceStepStatus.Success);
        result.Steps.Should().Contain(step => step.Name == "Judge" && step.Status == InspectionSequenceStepStatus.Success && step.Message.Contains("Pass", StringComparison.Ordinal));
        result.Steps.Should().Contain(step => step.Name == "Persist Result" && step.Status == InspectionSequenceStepStatus.Skipped);
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
        var useCase = CreateUseCase(new FakeActiveRecipeContext(ActiveRecipeContextResult.Success(recipe)), controller, cameraDevice: camera);

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
    public async Task RunAsync_Should_Fail_Inspect_2D_When_Vision_Result_Is_Invalid()
    {
        var recipe = CreateRecipeIndexEntry("RCP-VISION-INVALID", "1.0.0");
        var controller = new FakeEquipmentController(CreateSnapshot(MachineMode.Auto, connected: true, servoOn: true, homed: true));
        var vision = new FakeVisionInspectionEngine
        {
            InspectHandler = request => Task.FromResult(new VisionInspectionResult(
                Judgment.Invalid,
                new[] { new Defect("InvalidRoi", 1.0, 0, 0, 10, 10, "ROI is outside the frame.") },
                "2D inspection invalid: 1 ROI boundary issue(s).",
                request.RecipeId,
                request.RecipeVersion,
                TimeSpan.FromMilliseconds(2),
                request.RequestedAt.AddMilliseconds(2)))
        };
        var useCase = CreateUseCase(
            new FakeActiveRecipeContext(ActiveRecipeContextResult.Success(recipe)),
            controller,
            visionInspectionEngine: vision);

        var result = await useCase.RunAsync(CreateRequest(), progress: null, CancellationToken.None);

        result.Status.Should().Be(InspectionRunStatus.VisionInspectionFailed);
        result.Message.Should().Contain("2D inspection invalid");
        result.VisionResult.Should().NotBeNull();
        result.VisionResult!.Judgment.Should().Be(Judgment.Invalid);
        vision.Requests.Should().ContainSingle();
        result.Steps.Should().Contain(step => step.Name == "Inspect 2D" && step.Status == InspectionSequenceStepStatus.Failed);
        result.Steps.Should().Contain(step => step.Name == "Judge" && step.Status == InspectionSequenceStepStatus.Skipped);
    }

    [Fact]
    public async Task RunAsync_Should_Fail_When_Active_Recipe_Document_Cannot_Be_Loaded()
    {
        var recipe = CreateRecipeIndexEntry("RCP-MISSING-DOC", "1.0.0");
        var controller = new FakeEquipmentController(CreateSnapshot(MachineMode.Auto, connected: true, servoOn: true, homed: true));
        var documentStore = new FakeRecipeDocumentStore
        {
            LoadHandler = (_, _) => Task.FromResult(RecipeDocumentLoadResult.NotFound(@"C:\recipes\missing.recipe.json"))
        };
        var useCase = CreateUseCase(new FakeActiveRecipeContext(ActiveRecipeContextResult.Success(recipe)), controller, documentStore: documentStore);

        var result = await useCase.RunAsync(CreateRequest(), progress: null, CancellationToken.None);

        result.Status.Should().Be(InspectionRunStatus.RecipeDocumentUnavailable);
        result.Message.Should().Contain("Unable to load active Recipe document");
        controller.SnapshotCount.Should().Be(0);
        controller.ExecuteCount.Should().Be(0);
        documentStore.LoadRequests.Should().ContainSingle().Which.Should().Be(("RCP-MISSING-DOC", "1.0.0"));
        result.Steps.Should().Contain(step => step.Name == "Load Recipe" && step.Status == InspectionSequenceStepStatus.Failed);
    }

    [Fact]
    public async Task RunAsync_Should_Fail_Move_To_Camera_When_Recipe_Has_No_Camera_Teaching_Point()
    {
        var recipe = CreateRecipeIndexEntry("RCP-NO-CAMERA", "1.0.0");
        var controller = new FakeEquipmentController(CreateSnapshot(MachineMode.Auto, connected: true, servoOn: true, homed: true));
        var documentStore = new FakeRecipeDocumentStore(CreateRecipeDefinition("RCP-NO-CAMERA", "1.0.0", TeachingRole.Safe));
        var motion = new FakeMotionCommandUseCase();
        var useCase = CreateUseCase(
            new FakeActiveRecipeContext(ActiveRecipeContextResult.Success(recipe)),
            controller,
            documentStore: documentStore,
            motionCommandUseCase: motion);

        var result = await useCase.RunAsync(CreateRequest(), progress: null, CancellationToken.None);

        result.Status.Should().Be(InspectionRunStatus.ActiveRecipeInvalid);
        result.Message.Should().Contain("Camera teaching point");
        motion.Requests.Should().BeEmpty();
        result.Steps.Should().Contain(step => step.Name == "Move To Camera" && step.Status == InspectionSequenceStepStatus.Failed);
        result.Steps.Should().Contain(step => step.Name == "Grab Image" && step.Status == InspectionSequenceStepStatus.Skipped);
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
        IRecipeDocumentStore? documentStore = null,
        IMotionCommandUseCase? motionCommandUseCase = null,
        ICameraDevice? cameraDevice = null,
        IVisionInspectionEngine? visionInspectionEngine = null)
    {
        return new InspectionRunUseCase(
            activeRecipeContext,
            documentStore ?? new FakeRecipeDocumentStore(),
            controller,
            motionCommandUseCase ?? new FakeMotionCommandUseCase(),
            cameraDevice ?? new FakeCameraDevice(),
            visionInspectionEngine ?? new FakeVisionInspectionEngine(),
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

    private static RecipeDefinition CreateRecipeDefinition(
        string recipeId,
        string version,
        TeachingRole teachingRole = TeachingRole.Camera)
    {
        var timestamp = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        return new RecipeDefinition(
            recipeId,
            $"{recipeId} Product",
            version,
            timestamp.AddDays(-1),
            timestamp,
            new RecipeMotionSection(new[]
            {
                new RecipeTeachingPoint(
                    "CAMERA_POS_01",
                    "Camera Position 01",
                    teachingRole,
                    new Position4D(10.0, 20.0, 5.0, 0.0),
                    PositionTolerance.Default)
            }),
            new RecipeCameraSettings(7.5, 1.2, 70),
            new RecipeVisionSection(
                new[] { new RecipeRoi("ROI-01", "Main ROI", 10, 10, 100, 80) },
                new RecipeVisionParameters(0.75, 8, 0.65, 1.0, 0.15, 0.15)),
            new RecipeSequence(new[] { "SafetyCheck", "MoveToCamera", "Grab", "Inspect2D", "Inspect3D", "Judge", "Persist" }));
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

    private sealed class FakeRecipeDocumentStore : IRecipeDocumentStore
    {
        private readonly RecipeDefinition _recipe;

        public FakeRecipeDocumentStore()
            : this(CreateRecipeDefinition("RCP-AUTO", "1.0.0"))
        {
        }

        public FakeRecipeDocumentStore(RecipeDefinition recipe)
        {
            _recipe = recipe;
        }

        public List<(string RecipeId, string Version)> LoadRequests { get; } = new();

        public Func<string, string, Task<RecipeDocumentLoadResult>>? LoadHandler { get; init; }

        public Task<RecipeDocumentSaveResult> SaveAsync(RecipeDefinition recipe, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<RecipeDocumentLoadResult> LoadAsync(
            string recipeId,
            string version,
            CancellationToken cancellationToken)
        {
            LoadRequests.Add((recipeId, version));
            cancellationToken.ThrowIfCancellationRequested();
            if (LoadHandler is not null)
            {
                return LoadHandler(recipeId, version);
            }

            var recipe = _recipe with { RecipeId = recipeId, Version = version };
            return Task.FromResult(RecipeDocumentLoadResult.Success(recipe, $@"C:\recipes\{recipeId}.v{version}.recipe.json"));
        }
    }

    private sealed class FakeMotionCommandUseCase : IMotionCommandUseCase
    {
        public List<MotionCommandExecutionRequest> Requests { get; } = new();

        public Func<MotionCommandExecutionRequest, Task<MotionCommandExecutionResult>> ExecuteHandler { get; init; } =
            request =>
            {
                var commandRequest = new MachineCommandRequest(
                    "Sequence Move To Camera",
                    CorrelationId.New(),
                    request.Timeout,
                    DateTimeOffset.UtcNow,
                    request.GetParameters());
                return Task.FromResult(new MotionCommandExecutionResult(
                    commandRequest,
                    MachineCommandResult.Success("Sequence Move To Camera completed.", TimeSpan.FromMilliseconds(4), commandRequest.CorrelationId)));
            };

        public Task<MotionCommandExecutionResult> ExecuteAsync(
            MotionCommandExecutionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteHandler(request);
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

    private sealed class FakeVisionInspectionEngine : IVisionInspectionEngine
    {
        public List<VisionInspectionRequest> Requests { get; } = new();

        public Func<VisionInspectionRequest, Task<VisionInspectionResult>> InspectHandler { get; init; } =
            request => Task.FromResult(new VisionInspectionResult(
                Judgment.Pass,
                Array.Empty<Defect>(),
                "2D inspection Pass: 1 ROI(s) evaluated.",
                request.RecipeId,
                request.RecipeVersion,
                TimeSpan.FromMilliseconds(2),
                request.RequestedAt.AddMilliseconds(2)));

        public Task<VisionInspectionResult> InspectAsync(
            VisionInspectionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            cancellationToken.ThrowIfCancellationRequested();
            return InspectHandler(request);
        }
    }
}
