using VisionCell.Vision.Inspection;

namespace VisionCell.Application.Inspection;

public interface IInspectionResultRepository
{
    Task SaveAsync(InspectionResultSaveRequest request, CancellationToken cancellationToken);
}

public interface IInspectionResultReader
{
    Task<IReadOnlyList<InspectionResultRecord>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken);
}

public sealed record InspectionResultSaveRequest(
    Guid Id,
    string CorrelationId,
    string LotId,
    string RecipeId,
    string RecipeVersion,
    Judgment Judgment,
    string? DefectSummary,
    string SourceImagePath,
    string? OverlayImagePath,
    string? HeightMapPath,
    TimeSpan CycleTime,
    IReadOnlyList<InspectionSequenceStepRecord> StepTimings,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<InspectionDefectRecord> Defects,
    DateTimeOffset CreatedAt);

public sealed record InspectionResultRecord(
    Guid Id,
    string CorrelationId,
    string LotId,
    string RecipeId,
    string RecipeVersion,
    Judgment Judgment,
    string? DefectSummary,
    string SourceImagePath,
    string? OverlayImagePath,
    string? HeightMapPath,
    TimeSpan CycleTime,
    DateTimeOffset CreatedAt,
    IReadOnlyList<InspectionDefectRecord> Defects);

public sealed record InspectionDefectRecord(
    string Type,
    double Score,
    string? RoiId,
    int X,
    int Y,
    int Width,
    int Height,
    string? Message);
