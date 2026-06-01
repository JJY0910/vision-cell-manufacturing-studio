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

    public static MachineCommandResult Success(string message, TimeSpan elapsed, CorrelationId correlationId)
    {
        return new MachineCommandResult(CommandStatus.Success, null, message, elapsed, correlationId);
    }

    public static MachineCommandResult Rejected(ErrorCode errorCode, string message, TimeSpan elapsed, CorrelationId correlationId)
    {
        return new MachineCommandResult(CommandStatus.Rejected, errorCode, message, elapsed, correlationId);
    }

    public static MachineCommandResult Timeout(ErrorCode errorCode, string message, TimeSpan elapsed, CorrelationId correlationId)
    {
        return new MachineCommandResult(CommandStatus.Timeout, errorCode, message, elapsed, correlationId);
    }

    public static MachineCommandResult Cancelled(ErrorCode errorCode, string message, TimeSpan elapsed, CorrelationId correlationId)
    {
        return new MachineCommandResult(CommandStatus.Cancelled, errorCode, message, elapsed, correlationId);
    }

    public static MachineCommandResult Failed(ErrorCode errorCode, string message, TimeSpan elapsed, CorrelationId correlationId)
    {
        return new MachineCommandResult(CommandStatus.Failed, errorCode, message, elapsed, correlationId);
    }

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
