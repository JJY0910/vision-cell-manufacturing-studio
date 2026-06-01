using VisionCell.Motion.Teaching;

namespace VisionCell.Application.Teaching;

public sealed record TeachingPointSaveResult(
    TeachingPointOperationStatus Status,
    TeachingPoint? Point,
    IReadOnlyList<TeachingPointValidationIssue> ValidationIssues,
    string? Message)
{
    public bool IsSuccess => Status == TeachingPointOperationStatus.Success;

    public static TeachingPointSaveResult Success(TeachingPoint point)
    {
        return new TeachingPointSaveResult(
            TeachingPointOperationStatus.Success,
            point,
            Array.Empty<TeachingPointValidationIssue>(),
            null);
    }

    public static TeachingPointSaveResult ValidationFailed(IReadOnlyList<TeachingPointValidationIssue> issues)
    {
        return new TeachingPointSaveResult(
            TeachingPointOperationStatus.ValidationFailed,
            null,
            issues,
            "Teaching point validation failed.");
    }

    public static TeachingPointSaveResult Failure(TeachingPointOperationStatus status, string message)
    {
        return new TeachingPointSaveResult(status, null, Array.Empty<TeachingPointValidationIssue>(), message);
    }
}
