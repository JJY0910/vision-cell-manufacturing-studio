namespace VisionCell.Persistence.Motion;

public sealed record MotionCommandHistoryRow(
    Guid Id,
    string CorrelationId,
    string CommandName,
    string? AxisId,
    string RequestJson,
    string CommandResultJson,
    long ElapsedMs,
    DateTimeOffset CreatedAt);
