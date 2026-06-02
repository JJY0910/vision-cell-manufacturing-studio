using VisionCell.Core.Alarms;
using VisionCell.Core.Errors;

namespace VisionCell.Application.Alarms;

public sealed class NoopEquipmentAlarmRecorder : IEquipmentAlarmRecorder
{
    public static NoopEquipmentAlarmRecorder Instance { get; } = new();

    private NoopEquipmentAlarmRecorder()
    {
    }

    public Task RecordAsync(EquipmentAlarm alarm, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task RecordFailureAsync(
        ErrorCode errorCode,
        EquipmentArea area,
        string message,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
