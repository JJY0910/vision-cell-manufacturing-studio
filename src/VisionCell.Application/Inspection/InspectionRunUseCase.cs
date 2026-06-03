using System.Diagnostics;
using VisionCell.Application.Alarms;
using VisionCell.Application.Interlocks;
using VisionCell.Application.Motion;
using VisionCell.Application.Recipes;
using VisionCell.Core.Alarms;
using VisionCell.Core.Commands;
using VisionCell.Core.Errors;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Cameras;
using VisionCell.Equipment.Controllers;
using VisionCell.Motion.Commands;
using VisionCell.Motion.Teaching;
using VisionCell.Vision.Inspection;

namespace VisionCell.Application.Inspection;

public sealed class InspectionRunUseCase : IInspectionRunUseCase
{
    private const string LoadRecipeStep = "Load Recipe";
    private const string SafetyStep = "Safety Interlock";
    private const string StartSequenceStep = "Start Sequence";
    private const string MoveToCameraStep = "Move To Camera";
    private const string GrabImageStep = "Grab Image";
    private const string Inspect2DStep = "Inspect 2D";
    private const string Inspect3DStep = "Inspect 3D";
    private const string JudgeStep = "Judge";
    private const string PersistResultStep = "Persist Result";

    private static readonly string[] StepNames =
    {
        LoadRecipeStep,
        SafetyStep,
        StartSequenceStep,
        MoveToCameraStep,
        GrabImageStep,
        Inspect2DStep,
        Inspect3DStep,
        JudgeStep,
        PersistResultStep
    };

    private readonly IActiveRecipeContext _activeRecipeContext;
    private readonly IRecipeDocumentStore _recipeDocumentStore;
    private readonly IEquipmentController _controller;
    private readonly IMotionCommandUseCase _motionCommandUseCase;
    private readonly ICameraDevice _cameraDevice;
    private readonly IVisionInspectionEngine _visionInspectionEngine;
    private readonly IHeightMapInspectionEngine _heightMapInspectionEngine;
    private readonly SyntheticHeightMapFactory _syntheticHeightMapFactory;
    private readonly IInspectionArtifactWriter _inspectionArtifactWriter;
    private readonly IInspectionResultRepository _inspectionResultRepository;
    private readonly IEquipmentAlarmRecorder _alarmRecorder;
    private readonly Func<DateTimeOffset> _clock;

    public InspectionRunUseCase(
        IActiveRecipeContext activeRecipeContext,
        IRecipeDocumentStore recipeDocumentStore,
        IEquipmentController controller,
        IMotionCommandUseCase motionCommandUseCase,
        ICameraDevice cameraDevice,
        IVisionInspectionEngine visionInspectionEngine,
        IHeightMapInspectionEngine heightMapInspectionEngine,
        SyntheticHeightMapFactory syntheticHeightMapFactory,
        IInspectionArtifactWriter inspectionArtifactWriter,
        IInspectionResultRepository inspectionResultRepository,
        Func<DateTimeOffset>? clock = null,
        IEquipmentAlarmRecorder? alarmRecorder = null)
    {
        _activeRecipeContext = activeRecipeContext ?? throw new ArgumentNullException(nameof(activeRecipeContext));
        _recipeDocumentStore = recipeDocumentStore ?? throw new ArgumentNullException(nameof(recipeDocumentStore));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _motionCommandUseCase = motionCommandUseCase ?? throw new ArgumentNullException(nameof(motionCommandUseCase));
        _cameraDevice = cameraDevice ?? throw new ArgumentNullException(nameof(cameraDevice));
        _visionInspectionEngine = visionInspectionEngine ?? throw new ArgumentNullException(nameof(visionInspectionEngine));
        _heightMapInspectionEngine = heightMapInspectionEngine ?? throw new ArgumentNullException(nameof(heightMapInspectionEngine));
        _syntheticHeightMapFactory = syntheticHeightMapFactory ?? throw new ArgumentNullException(nameof(syntheticHeightMapFactory));
        _inspectionArtifactWriter = inspectionArtifactWriter ?? throw new ArgumentNullException(nameof(inspectionArtifactWriter));
        _inspectionResultRepository = inspectionResultRepository ?? throw new ArgumentNullException(nameof(inspectionResultRepository));
        _alarmRecorder = alarmRecorder ?? NoopEquipmentAlarmRecorder.Instance;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<ActiveRecipeContextResult> PrecheckActiveRecipeAsync(CancellationToken cancellationToken)
    {
        return _activeRecipeContext.GetActiveAsync(cancellationToken);
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
        EnsurePositiveTimeout(request.VisionTimeout, nameof(request.VisionTimeout));

        var startedAt = _clock();
        var recorder = new StepRecorder(progress);
        RecipeIndexEntry? recipe = null;
        RecipeDefinition? recipeDocument = null;
        MachineCommandRequest? commandRequest = null;
        MachineCommandResult? commandResult = null;
        MotionCommandExecutionResult? moveToCameraResult = null;
        CameraGrabResult? cameraGrabResult = null;
        VisionInspectionResult? visionResult = null;
        VisionInspectionResult? heightMapResult = null;
        VisionHeightMap? heightMap = null;
        Guid? persistedResultId = null;

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
                await RecordCommandAlarmAsync(commandResult, EquipmentArea.Equipment, cancellationToken).ConfigureAwait(false);
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
                await RecordCameraAlarmAsync(cameraGrabResult, cancellationToken).ConfigureAwait(false);
                return Finish(MapCameraStatus(cameraGrabResult.Status), cameraGrabResult.Message);
            }

            recorder.Update(GrabImageStep, InspectionSequenceStepStatus.Success, cameraGrabResult.Message, grabStopwatch.Elapsed);

            var visionStopwatch = Stopwatch.StartNew();
            recorder.Update(Inspect2DStep, InspectionSequenceStepStatus.Running, "Running deterministic 2D inspection.", null);
            try
            {
                visionResult = await _visionInspectionEngine
                    .InspectAsync(
                        CreateVisionInspectionRequest(
                            recipeDocument,
                            cameraGrabResult.Frame!,
                            commandRequest.CorrelationId,
                            request.VisionTimeout),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                visionStopwatch.Stop();
                var message = $"2D inspection failed: {ex.Message}";
                recorder.Update(Inspect2DStep, InspectionSequenceStepStatus.Failed, message, visionStopwatch.Elapsed);
                recorder.SkipPending("Skipped because 2D inspection did not complete.");
                await RecordInspectionAlarmAsync(message, commandRequest.CorrelationId.ToString(), cancellationToken).ConfigureAwait(false);
                return Finish(InspectionRunStatus.VisionInspectionFailed, message);
            }

            visionStopwatch.Stop();
            if (visionResult.Judgment == Judgment.Invalid)
            {
                recorder.Update(Inspect2DStep, InspectionSequenceStepStatus.Failed, visionResult.Message, visionStopwatch.Elapsed);
                recorder.SkipPending("Skipped because 2D inspection returned an invalid result.");
                await RecordInspectionAlarmAsync(visionResult.Message, commandRequest.CorrelationId.ToString(), cancellationToken).ConfigureAwait(false);
                return Finish(InspectionRunStatus.VisionInspectionFailed, visionResult.Message);
            }

            recorder.Update(Inspect2DStep, InspectionSequenceStepStatus.Success, visionResult.Message, visionStopwatch.Elapsed);

            var heightMapStopwatch = Stopwatch.StartNew();
            recorder.Update(Inspect3DStep, InspectionSequenceStepStatus.Running, "Running deterministic 3D height-map inspection.", null);
            try
            {
                var heightMapRequest = CreateHeightMapInspectionRequest(
                    recipeDocument,
                    cameraGrabResult.Frame!,
                    commandRequest.CorrelationId,
                    request.VisionTimeout);
                heightMap = heightMapRequest.HeightMap;
                heightMapResult = await _heightMapInspectionEngine
                    .InspectAsync(heightMapRequest, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                heightMapStopwatch.Stop();
                var message = $"3D inspection failed: {ex.Message}";
                recorder.Update(Inspect3DStep, InspectionSequenceStepStatus.Failed, message, heightMapStopwatch.Elapsed);
                recorder.SkipPending("Skipped because 3D inspection did not complete.");
                await RecordInspectionAlarmAsync(message, commandRequest.CorrelationId.ToString(), cancellationToken).ConfigureAwait(false);
                return Finish(InspectionRunStatus.HeightMapInspectionFailed, message);
            }

            heightMapStopwatch.Stop();
            if (heightMapResult.Judgment == Judgment.Invalid)
            {
                recorder.Update(Inspect3DStep, InspectionSequenceStepStatus.Failed, heightMapResult.Message, heightMapStopwatch.Elapsed);
                recorder.SkipPending("Skipped because 3D inspection returned an invalid result.");
                await RecordInspectionAlarmAsync(heightMapResult.Message, commandRequest.CorrelationId.ToString(), cancellationToken).ConfigureAwait(false);
                return Finish(InspectionRunStatus.HeightMapInspectionFailed, heightMapResult.Message);
            }

            recorder.Update(Inspect3DStep, InspectionSequenceStepStatus.Success, heightMapResult.Message, heightMapStopwatch.Elapsed);
            var finalJudgment = DetermineFinalJudgment(visionResult, heightMapResult);
            recorder.Update(JudgeStep, InspectionSequenceStepStatus.Success, FormatJudgeMessage(visionResult, heightMapResult), TimeSpan.Zero);

            var persistStopwatch = Stopwatch.StartNew();
            persistedResultId = Guid.NewGuid();
            recorder.Update(PersistResultStep, InspectionSequenceStepStatus.Running, "Persisting inspection result.", null);
            try
            {
                var lotId = CreateLotId(startedAt);
                var defects = CreateInspectionDefectRecords(visionResult, heightMapResult);
                var artifacts = await _inspectionArtifactWriter
                    .WriteAsync(
                        CreateInspectionArtifactWriteRequest(
                            persistedResultId.Value,
                            lotId,
                            recipeDocument,
                            commandRequest,
                            cameraGrabResult.Frame!,
                            heightMap!,
                            finalJudgment,
                            defects,
                            startedAt),
                        cancellationToken)
                    .ConfigureAwait(false);
                await _inspectionResultRepository
                    .SaveAsync(
                        CreateInspectionResultSaveRequest(
                            persistedResultId.Value,
                            lotId,
                            recipe,
                            commandRequest,
                            cameraGrabResult.Frame!,
                            visionResult,
                            heightMapResult,
                            finalJudgment,
                            artifacts,
                            defects,
                            recorder.Snapshot(),
                            startedAt),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                persistStopwatch.Stop();
                var message = $"Inspection result persistence failed: {ex.Message}";
                recorder.Update(PersistResultStep, InspectionSequenceStepStatus.Failed, message, persistStopwatch.Elapsed);
                await _alarmRecorder.RecordFailureAsync(
                    ErrorCode.PersistenceFailed,
                    EquipmentArea.Database,
                    message,
                    commandRequest.CorrelationId.ToString(),
                    cancellationToken).ConfigureAwait(false);
                return Finish(InspectionRunStatus.ResultPersistenceFailed, message);
            }

            persistStopwatch.Stop();
            recorder.Update(PersistResultStep, InspectionSequenceStepStatus.Success, $"Inspection result persisted: {persistedResultId.Value:N}.", persistStopwatch.Elapsed);

            return Finish(
                InspectionRunStatus.Accepted,
                $"Inspection sequence completed with judge {finalJudgment} and persisted result {persistedResultId.Value:N} for recipe '{recipe.RecipeId}' v{recipe.Version}.");
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
            await RecordInspectionAlarmAsync($"Inspection run failed: {ex.Message}", commandRequest?.CorrelationId.ToString(), cancellationToken).ConfigureAwait(false);
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
                visionResult,
                heightMapResult,
                persistedResultId,
                recorder.Snapshot(),
                startedAt,
                _clock());
        }
    }

    private Task RecordCommandAlarmAsync(
        MachineCommandResult result,
        EquipmentArea area,
        CancellationToken cancellationToken)
    {
        if (result.Status == CommandStatus.Cancelled || result.ErrorCode is not { } errorCode)
        {
            return Task.CompletedTask;
        }

        return _alarmRecorder.RecordFailureAsync(
            errorCode,
            area,
            result.Message,
            result.CorrelationId.ToString(),
            cancellationToken);
    }

    private Task RecordCameraAlarmAsync(
        CameraGrabResult result,
        CancellationToken cancellationToken)
    {
        if (result.Status == CameraGrabStatus.Cancelled || result.ErrorCode is not { } errorCode)
        {
            return Task.CompletedTask;
        }

        return _alarmRecorder.RecordFailureAsync(
            errorCode,
            EquipmentArea.Camera,
            result.Message,
            result.CorrelationId.ToString(),
            cancellationToken);
    }

    private Task RecordInspectionAlarmAsync(
        string message,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        return _alarmRecorder.RecordFailureAsync(
            ErrorCode.InspectionFailed,
            EquipmentArea.Inspection,
            message,
            correlationId,
            cancellationToken);
    }

    private VisionInspectionRequest CreateVisionInspectionRequest(
        RecipeDefinition recipe,
        CameraFrame frame,
        CorrelationId correlationId,
        TimeSpan timeout)
    {
        var image = CreateVisionImageFrame(frame);

        var rois = recipe.Vision.Rois
            .Select(roi => new VisionRoi(roi.Id, roi.Name, roi.X, roi.Y, roi.Width, roi.Height))
            .ToArray();

        var parameters = new VisionInspectionParameters(
            recipe.Vision.Parameters.MissingAreaThreshold,
            recipe.Vision.Parameters.OffsetTolerancePx,
            recipe.Vision.Parameters.ScratchThreshold);

        return new VisionInspectionRequest(
            correlationId,
            recipe.RecipeId,
            recipe.Version,
            image,
            rois,
            parameters,
            timeout,
            _clock());
    }

    private HeightMapInspectionRequest CreateHeightMapInspectionRequest(
        RecipeDefinition recipe,
        CameraFrame frame,
        CorrelationId correlationId,
        TimeSpan timeout)
    {
        var image = CreateVisionImageFrame(frame);
        var heightMap = _syntheticHeightMapFactory.CreateFromGray8(
            image,
            recipe.Vision.Parameters.ExpectedHeight,
            new Dictionary<string, string>
            {
                ["RecipeId"] = recipe.RecipeId,
                ["RecipeVersion"] = recipe.Version
            });
        var rois = recipe.Vision.Rois
            .Select(roi => new VisionRoi(roi.Id, roi.Name, roi.X, roi.Y, roi.Width, roi.Height))
            .ToArray();
        var parameters = new HeightMapInspectionParameters(
            recipe.Vision.Parameters.ExpectedHeight,
            recipe.Vision.Parameters.HeightToleranceLow,
            recipe.Vision.Parameters.HeightToleranceHigh,
            CalculateLeadBentGradientTolerance(recipe.Vision.Parameters));

        return new HeightMapInspectionRequest(
            correlationId,
            recipe.RecipeId,
            recipe.Version,
            heightMap,
            rois,
            parameters,
            timeout,
            _clock());
    }

    private static VisionImageFrame CreateVisionImageFrame(CameraFrame frame)
    {
        return new VisionImageFrame(
            frame.Width,
            frame.Height,
            frame.Stride,
            MapPixelFormat(frame.PixelFormat),
            frame.Pixels,
            frame.GrabbedAt,
            frame.Metadata);
    }

    private static VisionPixelFormat MapPixelFormat(CameraPixelFormat pixelFormat)
    {
        return pixelFormat switch
        {
            CameraPixelFormat.Gray8 => VisionPixelFormat.Gray8,
            _ => throw new NotSupportedException($"Unsupported camera pixel format: {pixelFormat}.")
        };
    }

    private static double CalculateLeadBentGradientTolerance(RecipeVisionParameters parameters)
    {
        return Math.Max(0.01, (parameters.HeightToleranceLow + parameters.HeightToleranceHigh) * 0.75);
    }

    private static Judgment DetermineFinalJudgment(
        VisionInspectionResult visionResult,
        VisionInspectionResult heightMapResult)
    {
        return visionResult.Judgment == Judgment.Fail || heightMapResult.Judgment == Judgment.Fail
            ? Judgment.Fail
            : Judgment.Pass;
    }

    private static string FormatJudgeMessage(
        VisionInspectionResult visionResult,
        VisionInspectionResult heightMapResult)
    {
        var finalJudgment = DetermineFinalJudgment(visionResult, heightMapResult);
        return finalJudgment == Judgment.Pass
            ? "Judge: Pass. No 2D/3D defects detected."
            : $"Judge: Fail. 2D defects: {visionResult.Defects.Count}; 3D defects: {heightMapResult.Defects.Count}.";
    }

    private InspectionResultSaveRequest CreateInspectionResultSaveRequest(
        Guid resultId,
        string lotId,
        RecipeIndexEntry recipe,
        MachineCommandRequest commandRequest,
        CameraFrame frame,
        VisionInspectionResult visionResult,
        VisionInspectionResult heightMapResult,
        Judgment finalJudgment,
        InspectionArtifactWriteResult artifacts,
        IReadOnlyList<InspectionDefectRecord> defects,
        IReadOnlyList<InspectionSequenceStepRecord> stepTimings,
        DateTimeOffset startedAt)
    {
        var completedAt = _clock();
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RecipeId"] = recipe.RecipeId,
            ["RecipeVersion"] = recipe.Version,
            ["ProductName"] = recipe.ProductName,
            ["CameraName"] = frame.CameraName,
            ["FrameWidth"] = frame.Width.ToString(),
            ["FrameHeight"] = frame.Height.ToString(),
            ["PixelFormat"] = frame.PixelFormat.ToString(),
            ["Vision2D"] = visionResult.Judgment.ToString(),
            ["Vision3D"] = heightMapResult.Judgment.ToString()
        };

        return new InspectionResultSaveRequest(
            resultId,
            commandRequest.CorrelationId.ToString(),
            lotId,
            recipe.RecipeId,
            recipe.Version,
            finalJudgment,
            defects.Count == 0 ? "No defects" : $"{defects.Count} defect(s)",
            artifacts.SourceImagePath,
            artifacts.OverlayImagePath,
            artifacts.HeightMapPath,
            completedAt - startedAt,
            stepTimings,
            parameters,
            defects,
            completedAt);
    }

    private static IReadOnlyList<InspectionDefectRecord> CreateInspectionDefectRecords(
        VisionInspectionResult visionResult,
        VisionInspectionResult heightMapResult)
    {
        return visionResult.Defects
            .Concat(heightMapResult.Defects)
            .Select(defect => new InspectionDefectRecord(
                defect.Type,
                defect.Score,
                RoiId: null,
                defect.X,
                defect.Y,
                defect.Width,
                defect.Height,
                defect.Message))
            .ToArray();
    }

    private static InspectionArtifactWriteRequest CreateInspectionArtifactWriteRequest(
        Guid resultId,
        string lotId,
        RecipeDefinition recipe,
        MachineCommandRequest commandRequest,
        CameraFrame frame,
        VisionHeightMap heightMap,
        Judgment finalJudgment,
        IReadOnlyList<InspectionDefectRecord> defects,
        DateTimeOffset startedAt)
    {
        return new InspectionArtifactWriteRequest(
            resultId,
            commandRequest.CorrelationId.ToString(),
            lotId,
            recipe.RecipeId,
            recipe.Version,
            finalJudgment,
            new InspectionArtifactImageFrame(
                frame.Width,
                frame.Height,
                frame.Stride,
                frame.Pixels),
            new InspectionArtifactHeightMap(
                heightMap.Width,
                heightMap.Height,
                heightMap.Values,
                heightMap.Unit),
            recipe.Vision.Rois
                .Select(roi => new InspectionArtifactRoi(roi.Id, roi.Name, roi.X, roi.Y, roi.Width, roi.Height))
                .ToArray(),
            defects,
            startedAt);
    }

    private static string CreateLotId(DateTimeOffset startedAt)
    {
        return $"LOT-{startedAt:yyyyMMddHHmmss}";
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
