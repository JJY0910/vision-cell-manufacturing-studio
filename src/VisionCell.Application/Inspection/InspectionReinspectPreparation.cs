namespace VisionCell.Application.Inspection;

public sealed record InspectionReinspectPreparation(
    Guid SourceResultId,
    string LotId,
    string RecipeId,
    string RecipeVersion,
    string SourceCorrelationId,
    DateTimeOffset PreparedAt);
