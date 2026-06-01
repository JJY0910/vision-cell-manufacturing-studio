using VisionCell.Core.Primitives;

namespace VisionCell.Core.Events;

public sealed record SystemEvent(
    string Id,
    CorrelationId CorrelationId,
    SystemEventSeverity Severity,
    string Source,
    string EventType,
    string Message,
    IReadOnlyDictionary<string, string>? Data,
    DateTimeOffset CreatedAt)
{
    public static SystemEvent Create(
        SystemEventSeverity severity,
        string source,
        string eventType,
        string message,
        CorrelationId? correlationId = null,
        IReadOnlyDictionary<string, string>? data = null)
    {
        return new SystemEvent(
            Guid.NewGuid().ToString("N"),
            correlationId ?? CorrelationId.New(),
            severity,
            source,
            eventType,
            message,
            data,
            DateTimeOffset.UtcNow);
    }
}
