using VisionCell.Application.Motion;
using VisionCell.Motion.Teaching;

namespace VisionCell.Application.Teaching;

public sealed record TeachingPointGoToResult(
    TeachingPointOperationStatus Status,
    TeachingPoint? Point,
    MotionCommandExecutionResult? MotionExecution,
    string? Message)
{
    public bool IsSuccess => Status == TeachingPointOperationStatus.Success;

    public static TeachingPointGoToResult Success(TeachingPoint point, MotionCommandExecutionResult execution)
    {
        return new TeachingPointGoToResult(TeachingPointOperationStatus.Success, point, execution, null);
    }

    public static TeachingPointGoToResult Failure(
        TeachingPointOperationStatus status,
        string message,
        TeachingPoint? point = null,
        MotionCommandExecutionResult? execution = null)
    {
        return new TeachingPointGoToResult(status, point, execution, message);
    }
}
