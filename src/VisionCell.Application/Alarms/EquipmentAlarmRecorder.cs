using VisionCell.Core.Alarms;
using VisionCell.Core.Errors;

namespace VisionCell.Application.Alarms;

public sealed class EquipmentAlarmRecorder : IEquipmentAlarmRecorder
{
    private readonly IEquipmentAlarmRepository _repository;
    private readonly Func<DateTimeOffset> _clock;

    public EquipmentAlarmRecorder(
        IEquipmentAlarmRepository repository,
        Func<DateTimeOffset>? clock = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Task RecordAsync(EquipmentAlarm alarm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alarm);
        return _repository.SaveAsync(alarm, cancellationToken);
    }

    public Task RecordFailureAsync(
        ErrorCode errorCode,
        EquipmentArea area,
        string message,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var alarm = EquipmentAlarmFactory.FromFailure(
            errorCode,
            area,
            message,
            _clock(),
            correlationId);
        return RecordAsync(alarm, cancellationToken);
    }
}
