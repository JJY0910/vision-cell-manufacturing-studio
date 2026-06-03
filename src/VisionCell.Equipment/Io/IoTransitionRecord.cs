namespace VisionCell.Equipment.Io;

public sealed record IoTransitionRecord
{
    public IoTransitionRecord(
        Guid id,
        string name,
        string address,
        IoBitDirection direction,
        bool previousValue,
        bool currentValue,
        bool previousForced,
        bool currentForced,
        DateTimeOffset changedAt,
        string source,
        string? correlationId = null,
        string? operatorMemo = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("I/O transition ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("I/O bit name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("I/O bit address is required.", nameof(address));
        }

        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported I/O direction.");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("I/O transition source is required.", nameof(source));
        }

        Id = id;
        Name = name.Trim();
        Address = address.Trim();
        Direction = direction;
        PreviousValue = previousValue;
        CurrentValue = currentValue;
        PreviousForced = previousForced;
        CurrentForced = currentForced;
        ChangedAt = changedAt;
        Source = source.Trim();
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        OperatorMemo = string.IsNullOrWhiteSpace(operatorMemo) ? null : operatorMemo.Trim();
    }

    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Address { get; init; }
    public IoBitDirection Direction { get; init; }
    public bool PreviousValue { get; init; }
    public bool CurrentValue { get; init; }
    public bool PreviousForced { get; init; }
    public bool CurrentForced { get; init; }
    public DateTimeOffset ChangedAt { get; init; }
    public string Source { get; init; }
    public string? CorrelationId { get; init; }
    public string? OperatorMemo { get; init; }
}
