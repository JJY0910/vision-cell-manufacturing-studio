using VisionCell.Core.Interlocks;

namespace VisionCell.Application.Teaching;

public sealed record TeachingPointGoToRequest(
    Guid TeachingPointId,
    InterlockContext InterlockContext,
    TimeSpan Timeout);
