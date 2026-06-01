namespace VisionCell.Motion.Teaching;

public sealed record TeachingPointCreateResult(
    TeachingPoint? Point,
    IReadOnlyList<TeachingPointValidationIssue> Issues)
{
    public bool IsSuccess => Point is not null && Issues.Count == 0;

    public static TeachingPointCreateResult Success(TeachingPoint point)
    {
        return new TeachingPointCreateResult(point, Array.Empty<TeachingPointValidationIssue>());
    }

    public static TeachingPointCreateResult Failure(IReadOnlyList<TeachingPointValidationIssue> issues)
    {
        return new TeachingPointCreateResult(null, issues);
    }
}
