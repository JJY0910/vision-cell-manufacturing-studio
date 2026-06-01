namespace VisionCell.Vision.Inspection;

public sealed record VisionInspectionResult(
    Judgment Judgment,
    IReadOnlyList<Defect> Defects,
    string Message,
    string RecipeId,
    string RecipeVersion,
    TimeSpan Elapsed,
    DateTimeOffset CompletedAt)
{
    public bool IsPass => Judgment == VisionCell.Vision.Inspection.Judgment.Pass;
}
