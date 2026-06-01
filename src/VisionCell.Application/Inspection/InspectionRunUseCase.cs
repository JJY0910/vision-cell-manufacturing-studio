using System.Diagnostics;
using VisionCell.Application.Interlocks;
using VisionCell.Application.Motion;
using VisionCell.Application.Recipes;
using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Cameras;
using VisionCell.Equipment.Controllers;
using VisionCell.Motion.Commands;
using VisionCell.Motion.Teaching;

namespace VisionCell.Application.Inspection;

public sealed class InspectionRunUseCase : IInspectionRunUseCase
{
    private const string LoadRecipeStep = "Load Recipe";
    private const string SafetyStep = "Safety Interlock";
    private const string StartSequenceStep = "Start Sequence";
    private const string MoveToCameraStep = "Move To Camera";
    private const string GrabImageStep = "Grab Image";

    private static readonly string[] StepNames =
    {
        LoadRecipeStep,
        SafetyStep,
        StartSequenceStep,
        MoveToCameraStep,
        GrabImageStep,
        "Inspect 2D",
        "Inspect 3D",
        "Judge",
        "Persist Result"
    };

    private readonly IActiveRecipeContext _activeRecipeContext;
    private readonly IRecipeDocumentStore _recipeDocumentStore;
    private readonly IEquipmentController _controller;
    private readonly IMotionCommandUseCase _motionCommandUseCase;
    private readonly ICameraDevice _cameraDevice;
    private readonly Func<DateTimeOffset> _clock;

    public InspectionRunUseCase(
        IActiveRecipeContext activeRecipeContext,
        IRecipeDocumentStore recipeDocumentStore,
        IEquipmentController controller,
        IMotionCommandUseCase motionCommandUseCase,
        ICameraDevice cameraDevice,
        Func<DateTimeOffset>? clock = null)
    {
        _activeRecipeContext = activeRecipeContext ?? throw new ArgumentNullException(nameof(activeRecipeContext));
        _recipeDocumentStore = recipeDocumentStore ?? throw new ArgumentNullException(nameof(recipeDocumentStore));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _motionCommandUseCase = motionCommandUseCase ?? throw new ArgumentNullException(nameof(motionCommandUseCase));
        _cameraDevice = cameraDevice ?? throw new ArgumentNullException(nameof(cameraDevice));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<InspectionRunResult> RunAsync(
        InspectionRunRequest request,
        IProgress<InspectionSequenceStepRecord>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsurePositiveTimeout(request.SnapshotTimeout, nameof(request.SnapshotTimeout));
        EnsurePositiveTimeout(request.CommandTimeout, nameof(request.CommandTimeout));
        EnsurePositiveTimeout(request.GrabTimeout, nameof(request.GrabTimeout));

        var startedAt = _clock();
        var recorder = new StepRecorder(progress);
        RecipeIndexEntry? recipe = null;
        RecipeDefinition? recipeDocument = null;
        MachineCommandRequest? commandRequest = null;
        MachineCommandResult? commandResult = null;
        MotionCommandExecutionResult? moveToCameraResult = null;
        CameraGrabResult? cameraGrabResult = null;

        try
        {
            var recipeStopwatch = Stopwatch.StartNew();
            recorder.Update(LoadRecipeStep, InspectionSequenceStepStatus.Running, "Reading active Recipe context and document.", null);
            var activeRecipe = await _activeRecipeContext.GetActiveAsync(cancellationToken).ConfigureAwait(false);

            if (!activeRecipe.IsSuccess || activeRecipe.Entry is null)
            {
                recipeStopwatch.Stop();
                recorder.Update(LoadRecipeStep, InspectionSequenceStepStatus.Failed, activeRecipe.Message, recipeStopwatch.Elapsed);
                recorder.SkipPending("Blocked before sequence start.");
                return Finish(MapActiveRecipeStatus(activeRecipe.Status), activeRecipe.Message);
            }

            recipe = activeRecipe.Entry;
            var documentResult = await _recipeDocumentStore
                .LoadAsync(recipe.RecipeId, recipe.Version, cancellationToken)
                .ConfigureAwait(false);
            recipeStopwatch.Stop();

            if (!documentResult.IsSuccess || documentResult.Recipe is null)
            {
                var message = FormatRecipeDocumentFailure(recipe, documentResult);
                recorder.Update(LoadRecipeStep, InspectionSequenceStepStatus.Failed, message, recipeStopwatch.Elapsed);
                recorder.SkipPending("Blocked before sequence start.");
                return Finish(MapRecipeDocumentStatus(documentResult.Status), message);
            }

            recipeDocument = documentResult.Recipe;
            recorder.Update(LoadRecipeStep, InspectionSequenceStepStatus.Success, $"Active Recipe document loaded: {recipe.RecipeId} v{recipe.Version}.", recipeStopwatch.Elapsed);

            var safetyStopwatch = Stopwatch.StartNew();
            recorder.Update(SafetyStep, InspectionSequenceStepStatus.Running, "Reading equipment snapshot and evaluating inspection interlocks.", null);
            var snapshot = await _controller.GetSnapshotAsync(request.SnapshotTimeout, cancellationToken).ConfigureAwait(false);
            var context = EquipmentSnapshotInterlockContextFactory.Create(snapshot) with { RecipeLoaded = true };
            var availability = _controller.GetCommandAvailability(CommandKind.RunInspection, context);
            safetyStopwatch.Stop();

            if (!availability.IsEnabled)
            {
                recorder.Update(SafetyStep, InspectionSequenceStepStatus.Failed, availability.DisabledReason, safetyStopwatch.Elapsed);
                recorder.SkipPending("Skipped because inspection interlock failed.");
                return Finish(InspectionRunStatus.InterlockRejected, availability.DisabledReason);
            }

            recorder.Update(SafetyStep, InspectionSequenceStepStatus.Success, "Inspection interlocks passed.", safetyStopwatch.Elapsed);

            var commandStopwatch = Stopwatch.StartNew();
            commandRequest = CreateCommandRequest(recipe, request.CommandTimeout);
            recorder.Update(StartSequenceStep, InspectionSequenceStepStatus.Running, "Submitting Run Inspection command to equipment controller.", null);
            var rawResult = await _controller.ExecuteCommandAsync(
                CommandKind.RunInspection,
                context,
                commandRequest,
                cancellationToken).ConfigureAwait(false);
            commandStopwatch.Stop();

            commandResult = rawResult with { CorrelationId = commandRequest.CorrelationId };
            if (!commandResult.IsSuccess)
            {
                var failedStatus = MapCommandStatus(commandResult.Status);
                recorder.Update(StartSequenceStep, MapStepStatus(commandResult.Status), commandResult.Message, commandStopwatch.Elapsed);
                recorder.SkipPending("Skipped because controller did not accept inspection sequence execution.");
                return Finish(failedStatus, commandResult.Message);
            }

            recorder.Update(StartSequenceStep, InspectionSequenceStepStatus.Success, commandResult.Message, commandStopwatch.Elapsed);

            var cameraPoint = FindCameraTeachingPoint(recipeDocument);
            if (cameraPoint is null)
            {
                const string message = "Active Recipe does not contain a Camera teaching point.";
                recorder.Update(MoveToCameraStep, InspectionSequenceStepStatus.Failed, message, TimeSpan.Zero);
                recorder.SkipPending("Skipped because camera position is unavailable.");
                return Finish(InspectionRunStatus.ActiveRecipeInvalid, message);
            }

            var moveStopwatch = Stopwatch.StartNew();
            recorder.Update(MoveToCameraStep, InspectionSequenceStepStatus.Running, $"Moving to Camera teaching point '{cameraPoint.Name}'.", null);
            moveToCameraResult = await _motionCommandUseCase.ExecuteAsync(
                CreateMoveToCameraRequest(context, cameraPoint, request.CommandTimeout, commandRequest.CorrelationId),
                cancellationToken).ConfigureAwait(false);
            moveStopwatch.Stop();

            if (!moveToCameraResult.CommandResult.IsSuccess)
            {
                recorder.Update(
                    MoveToCameraStep,
                    MapStepStatus(moveToCameraResult.CommandResult.Status),
                    moveToCameraResult.CommandResult.Message,
                    moveStopwatch.Elapsed);
                recorder.SkipPending("Skipped because Move To Camera did not complete.");
                return Finish(MapMoveStatus(moveToCameraResult.CommandResult.Status), moveToCameraResult.CommandResult.Message);
            }

            recorder.Update(MoveToCameraStep, InspectionSequenceStepStatus.Success, moveToCameraResult.CommandResult.Message, moveStopwatch.Elapsed);

            var grabStopwatch = Stopwatch.StartNew();
            recorder.Update(GrabImageStep, InspectionSequenceStepStatus.Running, "Grabbing camera frame.", null);
            cameraGrabResult = await _cameraDevice.GrabAsync(
                CreateCameraGrabRequest(recipeDocument, commandRequest.CorrelationId, request.GrabTimeout),
                cancellationToken).ConfigureAwait(false);
            grabStopwatch.Stop();

            if (!cameraGrabResult.IsSuccess)
            {
                recorder.Update(GrabImageStep, MapCameraStepStatus(cameraGrabResult.Status), cameraGrabResult.Message, grabStopwatch.Elapsed);
                recorder.SkipPending("Skipped because camera grab did not produce a frame.");
                return Finish(MapCameraStatus(cameraGrabResult.Status), cameraGrabResult.Message);
            }

            recorder.Update(GrabImageStep, InspectionSequenceStepStatus.Success, cameraGrabResult.Message, grabStopwatch.Elapsed);
            recorder.SkipPending("Pending implementation in vision, judge, and persistence slices.");

            return Finish(
                InspectionRunStatus.Accepted,
                $"Inspection sequence accepted and image grabbed for recipe '{recipe.RecipeId}' v{recipe.Version}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            recorder.CancelRunning("Inspection run cancelled by operator.");
            recorder.SkipPending("Skipped because inspection run was cancelled.");
            return Finish(InspectionRunStatus.CommandCancelled, "Inspection run cancelled by operator.");
        }
        catch (Exception ex)
        {
            recorder.FailRunning($"Inspection run failed: {ex.Message}");
            recorder.SkipPending("Skipped because inspection run failed.");
            return Finish(InspectionRunStatus.Failed, $"Inspection run failed: {ex.Message}");
        }

        InspectionRunResult Finish(InspectionRunStatus status, string message)
        {
            return new InspectionRunResult(
                status,
                message,
                recipe,
                commandRequest,
                commandResult,
                moveToCameraResult,
                cameraGrabResult,
                recorder.Snapshot(),
                startedAt,
                _clock());
        }
    }

    private CameraGrabRequest CreateCameraGrabRequest(
        RecipeDefinition recipe,
        CorrelationId correlationId,
        TimeSpan timeout)
    {
        return new CameraGrabRequest(
            correlationId,
            timeout,
            _clock(),
            recipe.RecipeId,
            recipe.Version,
            recipe.Camera.ExposureMs,
            recipe.Camera.Gain,
            recipe.Camera.LightIntensity);
    }

    private static MotionCommandExecutionRequest CreateMoveToCameraRequest(
        InterlockContext context,
        RecipeTeachingPoint point,
        TimeSpan timeout,
        CorrelationId parentCorrelationId)
    {
        var arrivalTolerance = new[]
        {
            point.Tolerance.X,
            point.Tolerance.Y,
            point.Tolerance.Z,
            point.Tolerance.Theta
        }.Min();

        var target = new AbsoluteMoveTarget(
            point.Position.X,
            point.Position.Y,
            point.Position.Z,
            point.Position.Theta,
            MotionProfilePreset.Standard.Velocity,
            MotionProfilePreset.Standard.Acceleration,
            MotionProfilePreset.Standard.Deceleration,
            MotionProfilePreset.Standard.Jerk,
            arrivalTolerance,
            MotionProfilePreset.Standard.Name);

        var parameters = new Dictionary<string, string>(target.ToParameters(), StringComparer.Ordinal)
        {
            ["TeachingPointId"] = point.Id,
            ["TeachingPointName"] = point.Name,
            ["ParentCorrelationId"] = parentCorrelationId.ToString()
        };

        var sequenceContext = context with { SequenceRunning = true };
        return new MotionCommandExecutionRequest(
            CommandKind.SequenceMoveToCamera,
            sequenceContext,
            timeout,
            parameters);
    }

    private static RecipeTeachingPoint? FindCameraTeachingPoint(RecipeDefinition recipe)
    {
        return recipe.Motion.TeachingPoints.FirstOrDefault(point => point.Role == TeachingRole.Camera);
    }

    private static string FormatRecipeDocumentFailure(
        RecipeIndexEntry recipe,
        RecipeDocumentLoadResult result)
    {
        var detail = result.ValidationIssues.Count == 0
            ? result.Message
            : $"{result.Message} {string.Join("; ", result.ValidationIssues.Select(issue => issue.Message))}";
        return $"Unable to load active Recipe document '{recipe.RecipeId}' v{recipe.Version}: {detail}";
    }

    private MachineCommandRequest CreateCommandRequest(RecipeIndexEntry recipe, TimeSpan timeout)
    {
        return new MachineCommandRequest(
            "Run Inspection",
            CorrelationId.New(),
            timeout,
            _clock(),
            new Dictionary<string, string>
            {
                ["RecipeId"] = recipe.RecipeId,
                ["RecipeVersion"] = recipe.Version,
                ["ProductName"] = recipe.ProductName
            });
    }

    private static InspectionRunStatus MapActiveRecipeStatus(ActiveRecipeContextStatus status)
    {
        return status switch
        {
            ActiveRecipeContextStatus.NotSelected => InspectionRunStatus.ActiveRecipeNotSelected,
            ActiveRecipeContextStatus.InvalidRecipe => InspectionRunStatus.ActiveRecipeInvalid,
            ActiveRecipeContextStatus.RepositoryUnavailable => InspectionRunStatus.ActiveRecipeUnavailable,
            _ => InspectionRunStatus.Failed
        };
    }

    private static InspectionRunStatus MapRecipeDocumentStatus(RecipeDocumentOperationStatus status)
    {
        return status switch
        {
            RecipeDocumentOperationStatus.ValidationFailed => InspectionRunStatus.ActiveRecipeInvalid,
            RecipeDocumentOperationStatus.InvalidDocument => InspectionRunStatus.ActiveRecipeInvalid,
            _ => InspectionRunStatus.RecipeDocumentUnavailable
        };
    }

    private static InspectionRunStatus MapCommandStatus(CommandStatus status)
    {
        return status switch
        {
            CommandStatus.Rejected => InspectionRunStatus.CommandRejected,
            CommandStatus.Timeout => InspectionRunStatus.CommandTimeout,
            CommandStatus.Cancelled => InspectionRunStatus.CommandCancelled,
            CommandStatus.Failed => InspectionRunStatus.CommandFailed,
            _ => InspectionRunStatus.Failed
        };
    }

    private static InspectionRunStatus MapMoveStatus(CommandStatus status)
    {
        return status switch
        {
            CommandStatus.Timeout => InspectionRunStatus.CommandTimeout,
            CommandStatus.Cancelled => InspectionRunStatus.CommandCancelled,
            _ => InspectionRunStatus.MoveToCameraFailed
        };
    }

    private static InspectionRunStatus MapCameraStatus(CameraGrabStatus status)
    {
        return status switch
        {
            CameraGrabStatus.Cancelled => InspectionRunStatus.CommandCancelled,
            _ => InspectionRunStatus.CameraGrabFailed
        };
    }

    private static InspectionSequenceStepStatus MapStepStatus(CommandStatus status)
    {
        return status switch
        {
            CommandStatus.Cancelled => InspectionSequenceStepStatus.Cancelled,
            _ => InspectionSequenceStepStatus.Failed
        };
    }

    private static InspectionSequenceStepStatus MapCameraStepStatus(CameraGrabStatus status)
    {
        return status switch
        {
            CameraGrabStatus.Cancelled => InspectionSequenceStepStatus.Cancelled,
            _ => InspectionSequenceStepStatus.Failed
        };
    }

    private static void EnsurePositiveTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, timeout, "Inspection run timeout must be greater than zero.");
        }
    }

    private sealed class StepRecorder
    {
        private readonly IProgress<InspectionSequenceStepRecord>? _progress;
        private readonly List<InspectionSequenceStepRecord> _steps = StepNames
            .Select(step => new InspectionSequenceStepRecord(
                step,
                InspectionSequenceStepStatus.Pending,
                "Pending",
                null))
            .ToList();

        public StepRecorder(IProgress<InspectionSequenceStepRecord>? progress)
        {
            _progress = progress;
        }

        public IReadOnlyList<InspectionSequenceStepRecord> Snapshot()
        {
            return _steps.ToArray();
        }

        public void Update(
            string name,
            InspectionSequenceStepStatus status,
            string message,
            TimeSpan? elapsed)
        {
            var index = _steps.FindIndex(step => string.Equals(step.Name, name, StringComparison.Ordinal));
            if (index < 0)
            {
                return;
            }

            var update = _steps[index] with
            {
                Status = status,
                Message = message,
                Elapsed = elapsed
            };
            _steps[index] = update;
            _progress?.Report(update);
        }

        public void SkipPending(string message)
        {
            for (var index = 0; index < _steps.Count; index++)
            {
                if (_steps[index].Status != InspectionSequenceStepStatus.Pending)
                {
                    continue;
                }

                var skipped = _steps[index] with
                {
                    Status = InspectionSequenceStepStatus.Skipped,
                    Message = message
                };
                _steps[index] = skipped;
                _progress?.Report(skipped);
            }
        }

        public void CancelRunning(string message)
        {
            UpdateRunning(InspectionSequenceStepStatus.Cancelled, message);
        }

        public void FailRunning(string message)
        {
            UpdateRunning(InspectionSequenceStepStatus.Failed, message);
        }

        private void UpdateRunning(InspectionSequenceStepStatus status, string message)
        {
            var running = _steps.FirstOrDefault(step => step.Status == InspectionSequenceStepStatus.Running);
            if (running is null)
            {
                return;
            }

            Update(running.Name, status, message, running.Elapsed);
        }
    }
}
