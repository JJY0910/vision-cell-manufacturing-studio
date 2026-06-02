using VisionCell.Core.Alarms;

namespace VisionCell.Application.Alarms;

public interface IEquipmentAlarmRepository
{
    Task SaveAsync(EquipmentAlarm alarm, CancellationToken cancellationToken);

    Task<IReadOnlyList<EquipmentAlarm>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken);

    Task AcknowledgeAsync(
        Guid alarmId,
        DateTimeOffset acknowledgedAt,
        string? actionMemo,
        CancellationToken cancellationToken);
}
