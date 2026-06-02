using VisionCell.Core.Alarms;

namespace VisionCell.Application.Alarms;

public sealed class AlarmCenterUseCase : IAlarmCenterUseCase
{
    private readonly IEquipmentAlarmRepository _repository;
    private readonly Func<DateTimeOffset> _clock;

    public AlarmCenterUseCase(
        IEquipmentAlarmRepository repository,
        Func<DateTimeOffset>? clock = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<IReadOnlyList<EquipmentAlarm>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be greater than zero.");
        }

        return _repository.ListRecentAsync(limit, cancellationToken);
    }

    public Task AcknowledgeAsync(
        Guid alarmId,
        string? actionMemo,
        CancellationToken cancellationToken)
    {
        if (alarmId == Guid.Empty)
        {
            throw new ArgumentException("Alarm ID is required.", nameof(alarmId));
        }

        return _repository.AcknowledgeAsync(alarmId, _clock(), actionMemo, cancellationToken);
    }
}
