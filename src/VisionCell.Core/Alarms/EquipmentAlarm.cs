namespace VisionCell.Core.Alarms;

public sealed record EquipmentAlarm
{
    public EquipmentAlarm(
        Guid id,
        string code,
        EquipmentAlarmSeverity severity,
        EquipmentArea area,
        string message,
        DateTimeOffset occurredAt,
        DateTimeOffset? acknowledgedAt = null,
        string? actionMemo = null,
        string? correlationId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Alarm ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Alarm code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Alarm message is required.", nameof(message));
        }

        if (acknowledgedAt is not null && acknowledgedAt.Value < occurredAt)
        {
            throw new ArgumentOutOfRangeException(nameof(acknowledgedAt), acknowledgedAt, "Acknowledged time cannot be earlier than occurred time.");
        }

        Id = id;
        Code = code.Trim();
        Severity = severity;
        Area = area;
        Message = message.Trim();
        OccurredAt = occurredAt;
        AcknowledgedAt = acknowledgedAt;
        ActionMemo = string.IsNullOrWhiteSpace(actionMemo) ? null : actionMemo.Trim();
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
    }

    public Guid Id { get; init; }
    public string Code { get; init; }
    public EquipmentAlarmSeverity Severity { get; init; }
    public EquipmentArea Area { get; init; }
    public string Message { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public string? ActionMemo { get; init; }
    public string? CorrelationId { get; init; }

    public bool IsAcknowledged => AcknowledgedAt is not null;

    public EquipmentAlarm Acknowledge(DateTimeOffset acknowledgedAt, string? actionMemo)
    {
        return new EquipmentAlarm(
            Id,
            Code,
            Severity,
            Area,
            Message,
            OccurredAt,
            acknowledgedAt,
            actionMemo,
            CorrelationId);
    }
}
