using VisionCell.Core.Primitives;

namespace VisionCell.Motion.Teaching;

public sealed record TeachingPoint(
    Guid Id,
    string Name,
    TeachingRole Role,
    Position4D Position,
    PositionTolerance Tolerance,
    string? Memo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
