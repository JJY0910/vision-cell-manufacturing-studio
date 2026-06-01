using VisionCell.Core.Errors;

namespace VisionCell.Motion.Axes;

public sealed record AxisAlarm(ErrorCode ErrorCode, string Message, DateTimeOffset CreatedAt);
