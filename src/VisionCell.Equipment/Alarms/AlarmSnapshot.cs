using VisionCell.Core.Errors;

namespace VisionCell.Equipment.Alarms;

public sealed record AlarmSnapshot(ErrorCode ErrorCode, string Message, DateTimeOffset RaisedAt);
