using VisionCell.Motion.Teaching;

namespace VisionCell.Application.Teaching;

public sealed record TeachingPointSaveRequest(
    string Name,
    TeachingRole Role,
    PositionTolerance Tolerance,
    string? Memo,
    TimeSpan SnapshotTimeout);
