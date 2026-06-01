namespace VisionCell.Application.Teaching;

public enum TeachingPointOperationStatus
{
    Success,
    ValidationFailed,
    NotFound,
    SnapshotUnavailable,
    RepositoryUnavailable,
    MotionCommandFailed
}
