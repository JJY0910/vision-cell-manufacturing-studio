using System.Diagnostics;
using VisionCell.Application.Interlocks;
using VisionCell.Application.Recipes;
using VisionCell.Core.Commands;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Controllers;

namespace VisionCell.Application.Inspection;

public sealed class InspectionRunUseCase : IInspectionRunUseCase
{
    private const string LoadRecipeStep = "Load Recipe";
    private const string SafetyStep = "Safety Interlock";
    private const string StartSequenceStep = "Start Sequence";

    private static readonly string[] StepNames =
    {
        LoadRecipeStep,
        SafetyStep,
        StartSequenceStep,
        "Move To Camera",
        "Grab Image",
        "Inspect 2D",
        "Inspect 3D",
        "Judge",
        "Persist Result"
    };

    private readonly IActiveRecipeContext _activeRecipeContext;
    private readonly IEquipmentController _controller;
    private readonly Func<DateTimeOffset> _clock;

    public InspectionRunUseCase(
        IActiveRecipeContext activeRecipeContext,
        IEquipmentController controller,
        Func<DateTimeOffset>? clock = null)
    {
        _activeRecipeContext = activeRecipeContext ?? throw new ArgumentNullException(nameof(activeRecipeContext));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
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

        var startedAt = _clock();
        var recorder = new StepRecorder(progress);
        RecipeIndexEntry? recipe = null;
        MachineCommandRequest? commandRequest = null;
        MachineCommandResult? commandResult = null;

        try
        {
            var recipeStopwatch = Stopwatch.StartNew();
            recorder.Update(LoadRecipeStep, InspectionSequenceStepStatus.Running, "Reading active Recipe context.", null);
            var activeRecipe = await _activeRecipeContext.GetActiveAsync(cancellationToken).ConfigureAwait(false);
            recipeStopwatch.Stop();

            if (!activeRecipe.IsSuccess || activeRecipe.Entry is null)
            {
                recorder.Update(LoadRecipeStep, InspectionSequenceStepStatus.Failed, activeRecipe.Message, recipeStopwatch.Elapsed);
                recorder.SkipPending("Blocked before sequence start.");
                return Finish(MapActiveRecipeStatus(activeRecipe.Status), activeRecipe.Message);
            }

            recipe = activeRecipe.Entry;
            recorder.Update(LoadRecipeStep, InspectionSequenceStepStatus.Success, activeRecipe.Message, recipeStopwatch.Elapsed);

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
            recorder.SkipPending("Pending implementation in camera, vision, judge, and persistence slices.");

            return Finish(
                InspectionRunStatus.Accepted,
                $"Inspection sequence accepted for recipe '{recipe.RecipeId}' v{recipe.Version}.");
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
                recorder.Snapshot(),
                startedAt,
                _clock());
        }
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

    private static InspectionSequenceStepStatus MapStepStatus(CommandStatus status)
    {
        return status switch
        {
            CommandStatus.Cancelled => InspectionSequenceStepStatus.Cancelled,
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
