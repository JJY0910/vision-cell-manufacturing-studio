namespace VisionCell.Application.Inspection;

public interface IInspectionReinspectUseCase
{
    Task<InspectionReinspectComparisonResult> RunAsync(
        InspectionReinspectPreparation preparation,
        CancellationToken cancellationToken);
}

public sealed class InspectionReinspectUseCase : IInspectionReinspectUseCase
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<Guid> _idFactory;

    public InspectionReinspectUseCase(
        Func<DateTimeOffset>? clock = null,
        Func<Guid>? idFactory = null)
    {
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
            return Task.FromResult(new InspectionReinspectComparisonResult(
                preparation.SourceResultId,
                "-",
                preparation.LotId,
                preparation.RecipeId,
                preparation.RecipeVersion,
                preparation.PreviousJudgment,
                "-",
                preparation.PreviousDefectCount,
                0,
                preparation.PreviousCycleTime,
                TimeSpan.Zero,
                InspectionReinspectComparisonStatus.Blocked,
                _clock(),
                "Not persisted",
                preparation.DisabledReason));
        }

        var replayCorrelationId = $"offline-reinspect-{_idFactory():N}";
        return Task.FromResult(new InspectionReinspectComparisonResult(
            preparation.SourceResultId,
            replayCorrelationId,
            preparation.LotId,
            preparation.RecipeId,
            preparation.RecipeVersion,
            preparation.PreviousJudgment,
            preparation.PreviousJudgment,
            preparation.PreviousDefectCount,
            preparation.PreviousDefectCount,
            preparation.PreviousCycleTime,
            preparation.PreviousCycleTime,
            InspectionReinspectComparisonStatus.Matched,
            _clock(),
            "Not persisted",
            "Metadata comparison completed from the prepared historical result context. Live camera, motion, and vision sequence replay were not executed."));
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
