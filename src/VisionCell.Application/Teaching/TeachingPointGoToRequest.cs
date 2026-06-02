namespace VisionCell.Application.Teaching;

public sealed record TeachingPointGoToRequest(
    Guid TeachingPointId,
    TimeSpan SnapshotTimeout,
    TimeSpan CommandTimeout);
