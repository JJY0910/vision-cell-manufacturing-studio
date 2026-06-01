using VisionCell.Core.Commands;

namespace VisionCell.Application.Motion;

public sealed record MotionCommandHistoryRecord(
    Guid Id,
    string CorrelationId,
    string CommandName,
    string? AxisId,
    CommandStatus Status,
    string? ErrorCode,
    string Message,
    TimeSpan Elapsed,
    DateTimeOffset CreatedAt);
