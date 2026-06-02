using VisionCell.Core.Alarms;

namespace VisionCell.Application.Alarms;

public interface IAlarmCenterUseCase
{
    Task<IReadOnlyList<EquipmentAlarm>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken);

    Task AcknowledgeAsync(
        Guid alarmId,
        string? actionMemo,
        CancellationToken cancellationToken);
}
