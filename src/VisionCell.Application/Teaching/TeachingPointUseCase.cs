using System.Text.Json;
using System.Text.Json.Serialization;
using VisionCell.Application.Interlocks;
using VisionCell.Application.Motion;
using VisionCell.Core.Commands;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Controllers;
using VisionCell.Motion.Commands;
using VisionCell.Motion.Teaching;

namespace VisionCell.Application.Teaching;

public sealed class TeachingPointUseCase : ITeachingPointUseCase
{
    private static readonly JsonSerializerOptions HistoryJsonOptions = CreateHistoryJsonOptions();

    private readonly IEquipmentController _controller;
    private readonly ITeachingPointRepository _repository;
    private readonly ITeachingHistoryRepository _historyRepository;
    private readonly IMotionCommandUseCase _motionCommandUseCase;
    private readonly Func<DateTimeOffset> _clock;

    public TeachingPointUseCase(
        IEquipmentController controller,
        ITeachingPointRepository repository,
        ITeachingHistoryRepository historyRepository,
        IMotionCommandUseCase motionCommandUseCase,
        Func<DateTimeOffset>? clock = null)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
        _motionCommandUseCase = motionCommandUseCase ?? throw new ArgumentNullException(nameof(motionCommandUseCase));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<TeachingPoint>> ListAsync(int limit, CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Teaching point list limit must be greater than zero.");
        }

        return await _repository.ListAsync(limit, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TeachingPointSaveResult> SaveCurrentPositionAsync(
        TeachingPointSaveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsurePositiveTimeout(request.SnapshotTimeout, nameof(request.SnapshotTimeout));

        EquipmentSnapshot snapshot;
        try
        {
            snapshot = await _controller.GetSnapshotAsync(request.SnapshotTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TeachingPointSaveResult.Failure(
                TeachingPointOperationStatus.SnapshotUnavailable,
                $"Unable to read current axis position: {ex.Message}");
        }

        if (!TryCreatePosition(snapshot, out var position, out var positionIssues))
        {
            return TeachingPointSaveResult.ValidationFailed(positionIssues);
        }

        var creation = TeachingPointFactory.Create(
            request.Name,
            request.Role,
            position,
            request.Tolerance,
            request.Memo,
            timestamp: _clock());

        if (!creation.IsSuccess || creation.Point is null)
        {
            return TeachingPointSaveResult.ValidationFailed(creation.Issues);
        }

        try
        {
            var duplicate = await _repository.FindByNameAsync(creation.Point.Name, cancellationToken).ConfigureAwait(false);
            if (duplicate is not null)
            {
                return TeachingPointSaveResult.ValidationFailed(new[]
                {
                    new TeachingPointValidationIssue(
                        "TeachingPoint.NameDuplicate",
                        $"Teaching point name '{creation.Point.Name}' already exists.")
                });
            }

            await _repository.SaveAsync(creation.Point, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TeachingPointSaveResult.Failure(
                TeachingPointOperationStatus.RepositoryUnavailable,
                $"Unable to save teaching point: {ex.Message}");
        }

        try
        {
            await _historyRepository.SaveAsync(
                TeachingHistoryEntry.Create(
                    creation.Point.Id,
                    recipeId: request.RecipeId,
                    TeachingHistoryAction.Created,
                    beforeJson: null,
                    afterJson: JsonSerializer.Serialize(creation.Point, HistoryJsonOptions),
                    clock: _clock),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TeachingPointSaveResult.Failure(
                TeachingPointOperationStatus.RepositoryUnavailable,
                $"Unable to save teaching point history: {ex.Message}");
        }

        return TeachingPointSaveResult.Success(creation.Point);
    }

    public async Task<TeachingPointSaveResult> UpdateAsync(
        TeachingPointUpdateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TeachingPointId == Guid.Empty)
        {
            return TeachingPointSaveResult.ValidationFailed(new[]
            {
                new TeachingPointValidationIssue(
                    "TeachingPoint.IdRequired",
                    "Teaching point id must not be empty.")
            });
        }

        TeachingPoint? existing;
        try
        {
            existing = await _repository.FindByIdAsync(request.TeachingPointId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TeachingPointSaveResult.Failure(
                TeachingPointOperationStatus.RepositoryUnavailable,
                $"Unable to load teaching point: {ex.Message}");
        }

        if (existing is null)
        {
            return TeachingPointSaveResult.Failure(
                TeachingPointOperationStatus.NotFound,
                $"Teaching point '{request.TeachingPointId}' was not found.");
        }

        var updatedAt = _clock();
        var update = TeachingPointFactory.Create(
            request.Name,
            request.Role,
            request.Position,
            request.Tolerance,
            request.Memo,
            request.TeachingPointId,
            updatedAt);

        if (!update.IsSuccess || update.Point is null)
        {
            return TeachingPointSaveResult.ValidationFailed(update.Issues);
        }

        var updated = update.Point with
        {
            CreatedAt = existing.CreatedAt,
            UpdatedAt = updatedAt
        };

        try
        {
            var duplicate = await _repository.FindByNameAsync(updated.Name, cancellationToken).ConfigureAwait(false);
            if (duplicate is not null && duplicate.Id != updated.Id)
            {
                return TeachingPointSaveResult.ValidationFailed(new[]
                {
                    new TeachingPointValidationIssue(
                        "TeachingPoint.NameDuplicate",
                        $"Teaching point name '{updated.Name}' already exists.")
                });
            }

            await _repository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TeachingPointSaveResult.Failure(
                TeachingPointOperationStatus.RepositoryUnavailable,
                $"Unable to update teaching point: {ex.Message}");
        }

        try
        {
            await _historyRepository.SaveAsync(
                TeachingHistoryEntry.Create(
                    updated.Id,
                    recipeId: request.RecipeId,
                    TeachingHistoryAction.Updated,
                    beforeJson: JsonSerializer.Serialize(existing, HistoryJsonOptions),
                    afterJson: JsonSerializer.Serialize(updated, HistoryJsonOptions),
                    clock: _clock),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TeachingPointSaveResult.Failure(
                TeachingPointOperationStatus.RepositoryUnavailable,
                $"Unable to save teaching point history: {ex.Message}");
        }

        return TeachingPointSaveResult.Success(updated);
    }

    public async Task<TeachingPointDeleteResult> DeleteAsync(
        TeachingPointDeleteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TeachingPointId == Guid.Empty)
        {
            return TeachingPointDeleteResult.Failure(
                TeachingPointOperationStatus.ValidationFailed,
                "Teaching point id must not be empty.");
        }

        TeachingPoint? existing;
        try
        {
            existing = await _repository.FindByIdAsync(request.TeachingPointId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TeachingPointDeleteResult.Failure(
                TeachingPointOperationStatus.RepositoryUnavailable,
                $"Unable to load teaching point: {ex.Message}");
        }

        if (existing is null)
        {
            return TeachingPointDeleteResult.Failure(
                TeachingPointOperationStatus.NotFound,
                $"Teaching point '{request.TeachingPointId}' was not found.");
        }

        try
        {
            await _historyRepository.SaveAsync(
                TeachingHistoryEntry.Create(
                    existing.Id,
                    recipeId: request.RecipeId,
                    TeachingHistoryAction.Deleted,
                    beforeJson: JsonSerializer.Serialize(existing, HistoryJsonOptions),
                    afterJson: null,
                    clock: _clock),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TeachingPointDeleteResult.Failure(
                TeachingPointOperationStatus.RepositoryUnavailable,
                $"Unable to save teaching point history: {ex.Message}",
                existing);
        }

        try
        {
            await _repository.DeleteAsync(existing.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TeachingPointDeleteResult.Failure(
                TeachingPointOperationStatus.RepositoryUnavailable,
                $"Unable to delete teaching point: {ex.Message}",
                existing);
        }

        return TeachingPointDeleteResult.Success(existing);
    }

    public async Task<TeachingPointGoToResult> GoToAsync(
        TeachingPointGoToRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsurePositiveTimeout(request.SnapshotTimeout, nameof(request.SnapshotTimeout));
        EnsurePositiveTimeout(request.CommandTimeout, nameof(request.CommandTimeout));

        TeachingPoint? point;
        try
        {
            point = await _repository.FindByIdAsync(request.TeachingPointId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TeachingPointGoToResult.Failure(
                TeachingPointOperationStatus.RepositoryUnavailable,
                $"Unable to load teaching point: {ex.Message}");
        }

        if (point is null)
        {
            return TeachingPointGoToResult.Failure(
                TeachingPointOperationStatus.NotFound,
                $"Teaching point '{request.TeachingPointId}' was not found.");
        }

        EquipmentSnapshot snapshot;
        try
        {
            snapshot = await _controller.GetSnapshotAsync(request.SnapshotTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TeachingPointGoToResult.Failure(
                TeachingPointOperationStatus.SnapshotUnavailable,
                $"Unable to read equipment state for Go To Teaching Point: {ex.Message}",
                point);
        }

        var target = CreateMoveTarget(point);
        var executionRequest = new MotionCommandExecutionRequest(
            CommandKind.MoveAbsolute,
            EquipmentSnapshotInterlockContextFactory.Create(snapshot),
            request.CommandTimeout,
            target.ToParameters());

        try
        {
            var execution = await _motionCommandUseCase.ExecuteAsync(executionRequest, cancellationToken).ConfigureAwait(false);
            if (execution.CommandResult.IsSuccess)
            {
                return TeachingPointGoToResult.Success(point, execution);
            }

            return TeachingPointGoToResult.Failure(
                TeachingPointOperationStatus.MotionCommandFailed,
                execution.CommandResult.Message,
                point,
                execution);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TeachingPointGoToResult.Failure(
                TeachingPointOperationStatus.MotionCommandFailed,
                $"Unable to execute Go To Teaching Point: {ex.Message}",
                point);
        }
    }

    private static void EnsurePositiveTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, timeout, "Teaching point timeout must be greater than zero.");
        }
    }

    private static AbsoluteMoveTarget CreateMoveTarget(TeachingPoint point)
    {
        var arrivalTolerance = new[]
        {
            point.Tolerance.X,
            point.Tolerance.Y,
            point.Tolerance.Z,
            point.Tolerance.Theta
        }.Min();

        return new AbsoluteMoveTarget(
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
    }

    private static bool TryCreatePosition(
        EquipmentSnapshot snapshot,
        out Position4D position,
        out IReadOnlyList<TeachingPointValidationIssue> issues)
    {
        var axisPositions = snapshot.Axes.ToDictionary(axis => axis.AxisId, axis => axis.Position);
        var missingAxes = new[] { AxisId.X, AxisId.Y, AxisId.Z, AxisId.Theta }
            .Where(axisId => !axisPositions.ContainsKey(axisId))
            .ToArray();

        if (missingAxes.Length > 0)
        {
            position = default;
            issues = missingAxes
                .Select(axisId => new TeachingPointValidationIssue(
                    "TeachingPoint.AxisSnapshotMissing",
                    $"{axisId} axis snapshot is required to save current position."))
                .ToArray();
            return false;
        }

        position = new Position4D(
            axisPositions[AxisId.X],
            axisPositions[AxisId.Y],
            axisPositions[AxisId.Z],
            axisPositions[AxisId.Theta]);
        issues = Array.Empty<TeachingPointValidationIssue>();
        return true;
    }

    private static JsonSerializerOptions CreateHistoryJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
