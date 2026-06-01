using VisionCell.Motion.Teaching;

namespace VisionCell.Application.Teaching;

public sealed record TeachingPointDeleteResult(
    TeachingPointOperationStatus Status,
    TeachingPoint? DeletedPoint,
    string? Message)
{
    public bool IsSuccess => Status == TeachingPointOperationStatus.Success;

    public static TeachingPointDeleteResult Success(TeachingPoint point)
    {
        return new TeachingPointDeleteResult(TeachingPointOperationStatus.Success, point, null);
    }

    public static TeachingPointDeleteResult Failure(
        TeachingPointOperationStatus status,
        string message,
        TeachingPoint? deletedPoint = null)
    {
        return new TeachingPointDeleteResult(status, deletedPoint, message);
    }
}
