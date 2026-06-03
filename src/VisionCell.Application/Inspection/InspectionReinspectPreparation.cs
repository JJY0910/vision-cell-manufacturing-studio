namespace VisionCell.Application.Inspection;

public sealed record InspectionReinspectPreparation(
    Guid SourceResultId,
    string LotId,
    string RecipeId,
    string RecipeVersion,
    string PreviousJudgment,
    TimeSpan PreviousCycleTime,
    int PreviousDefectCount,
    string SourceCorrelationId,
    string SourceImagePath,
    string OverlayImagePath,
    string HeightMapPath,
    DateTimeOffset PreparedAt,
    bool CanRunInspection,
    string DisabledReason);
