namespace VisionCell.Application.Inspection;

public interface IInspectionReinspectUseCase
{
    Task<InspectionReinspectComparisonResult> RunAsync(
        InspectionReinspectPreparation preparation,
        CancellationToken cancellationToken);
}

public sealed class InspectionReinspectUseCase : IInspectionReinspectUseCase
{
    private readonly IInspectionReinspectComparisonRepository? _comparisonRepository;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<Guid> _idFactory;

    public InspectionReinspectUseCase(
        Func<DateTimeOffset>? clock = null,
        Func<Guid>? idFactory = null)
        : this(null, clock, idFactory)
    {
    }

    public InspectionReinspectUseCase(
        IInspectionReinspectComparisonRepository? comparisonRepository,
        Func<DateTimeOffset>? clock = null,
        Func<Guid>? idFactory = null)
    {
        _comparisonRepository = comparisonRepository;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _idFactory = idFactory ?? Guid.NewGuid;
    }

    public Task<InspectionReinspectComparisonResult> RunAsync(
        InspectionReinspectPreparation preparation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        cancellationToken.ThrowIfCancellationRequested();

        if (!preparation.CanRunInspection)
        {
            var blocked = CreateResult(
                preparation,
                "-",
                "-",
                0,
                TimeSpan.Zero,
                InspectionReinspectComparisonStatus.Blocked,
                preparation.DisabledReason);
            return Task.FromResult(blocked);
        }

        var replayCorrelationId = $"offline-reinspect-{_idFactory():N}";
        var result = CreateResult(
            preparation,
            replayCorrelationId,
            preparation.PreviousJudgment,
            preparation.PreviousDefectCount,
            preparation.PreviousCycleTime,
            InspectionReinspectComparisonStatus.Matched,
            "Metadata comparison completed from the prepared historical result context. Live camera, motion, and vision sequence replay were not executed.");
        if (_comparisonRepository is null)
        {
            return Task.FromResult(result);
        }

        var persisted = result with { PersistenceStatus = "Persisted to offline re-inspect history." };
        return SaveAndReturnAsync(persisted, cancellationToken);
    }

    private InspectionReinspectComparisonResult CreateResult(
        InspectionReinspectPreparation preparation,
        string replayCorrelationId,
        string replayedJudgment,
        int replayedDefectCount,
        TimeSpan replayedCycleTime,
        InspectionReinspectComparisonStatus status,
        string message)
    {
        return new InspectionReinspectComparisonResult(
            preparation.SourceResultId,
            replayCorrelationId,
            preparation.LotId,
            preparation.RecipeId,
            preparation.RecipeVersion,
            preparation.PreviousJudgment,
            replayedJudgment,
            preparation.PreviousDefectCount,
            replayedDefectCount,
            preparation.PreviousCycleTime,
            replayedCycleTime,
            status,
            _clock(),
            "Not persisted",
            message);
    }

    private async Task<InspectionReinspectComparisonResult> SaveAndReturnAsync(
        InspectionReinspectComparisonResult result,
        CancellationToken cancellationToken)
    {
        await _comparisonRepository!
            .SaveAsync(result, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }
}

public sealed record InspectionReinspectComparisonResult(
    Guid SourceResultId,
    string ReplayCorrelationId,
    string LotId,
    string RecipeId,
    string RecipeVersion,
    string PreviousJudgment,
    string ReplayedJudgment,
    int PreviousDefectCount,
    int ReplayedDefectCount,
    TimeSpan PreviousCycleTime,
    TimeSpan ReplayedCycleTime,
    InspectionReinspectComparisonStatus Status,
    DateTimeOffset ComparedAt,
    string PersistenceStatus,
    string Message)
{
    public bool JudgmentChanged => !string.Equals(PreviousJudgment, ReplayedJudgment, StringComparison.OrdinalIgnoreCase);
}

public enum InspectionReinspectComparisonStatus
{
    Matched,
    Changed,
    Blocked
}
