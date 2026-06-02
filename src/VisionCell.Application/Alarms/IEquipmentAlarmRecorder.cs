using VisionCell.Core.Alarms;
using VisionCell.Core.Errors;

namespace VisionCell.Application.Alarms;

public interface IEquipmentAlarmRecorder
{
    Task RecordAsync(EquipmentAlarm alarm, CancellationToken cancellationToken);

    Task RecordFailureAsync(
        ErrorCode errorCode,
        EquipmentArea area,
        string message,
        string? correlationId,
        CancellationToken cancellationToken);
}
