using VisionCell.Core.Primitives;
using VisionCell.Motion.Teaching;

namespace VisionCell.Application.Teaching;

public sealed record TeachingPointUpdateRequest(
    Guid TeachingPointId,
    string Name,
    TeachingRole Role,
    Position4D Position,
    PositionTolerance Tolerance,
    string? Memo);
