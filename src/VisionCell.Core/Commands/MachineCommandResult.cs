using VisionCell.Core.Errors;
using VisionCell.Core.Events;
using VisionCell.Core.Primitives;

namespace VisionCell.Core.Commands;

public sealed record MachineCommandResult(
    CommandStatus Status,
    ErrorCode? ErrorCode,
    string Message,
    TimeSpan Elapsed,
    CorrelationId CorrelationId)
{
    public bool IsSuccess => Status == CommandStatus.Success;

    public SystemEvent ToSystemEvent(string source, string eventType)
    {
        var severity = Status switch
        {
            CommandStatus.Success => SystemEventSeverity.Info,
            CommandStatus.Rejected => SystemEventSeverity.Warning,
            CommandStatus.Timeout => SystemEventSeverity.Alarm,
            CommandStatus.Cancelled => SystemEventSeverity.Warning,
            _ => SystemEventSeverity.Error
        };

        return SystemEvent.Create(severity, source, eventType, Message, CorrelationId);
    }
}
