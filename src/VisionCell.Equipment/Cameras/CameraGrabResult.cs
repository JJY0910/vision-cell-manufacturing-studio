using VisionCell.Core.Primitives;
using CoreErrorCode = VisionCell.Core.Errors.ErrorCode;

namespace VisionCell.Equipment.Cameras;

public sealed record CameraGrabResult(
    CameraGrabStatus Status,
    CoreErrorCode? ErrorCode,
    string Message,
    CameraFrame? Frame,
    TimeSpan Elapsed,
    CorrelationId CorrelationId)
{
    public bool IsSuccess => Status == CameraGrabStatus.Success && Frame is not null;

    public static CameraGrabResult Success(
        CameraFrame frame,
        string message,
        TimeSpan elapsed,
        CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return new CameraGrabResult(CameraGrabStatus.Success, null, message, frame, elapsed, correlationId);
    }

    public static CameraGrabResult NotReady(
        string message,
        TimeSpan elapsed,
        CorrelationId correlationId)
    {
        return new CameraGrabResult(CameraGrabStatus.NotReady, CoreErrorCode.CameraNotReady, message, null, elapsed, correlationId);
    }

    public static CameraGrabResult Timeout(
        string message,
        TimeSpan elapsed,
        CorrelationId correlationId)
    {
        return new CameraGrabResult(CameraGrabStatus.Timeout, CoreErrorCode.CameraGrabTimeout, message, null, elapsed, correlationId);
    }

    public static CameraGrabResult Cancelled(
        string message,
        TimeSpan elapsed,
        CorrelationId correlationId)
    {
        return new CameraGrabResult(CameraGrabStatus.Cancelled, CoreErrorCode.CommandCancelled, message, null, elapsed, correlationId);
    }

    public static CameraGrabResult Failed(
        string message,
        TimeSpan elapsed,
        CorrelationId correlationId)
    {
        return new CameraGrabResult(CameraGrabStatus.Failed, CoreErrorCode.CameraGrabFailed, message, null, elapsed, correlationId);
    }
}
