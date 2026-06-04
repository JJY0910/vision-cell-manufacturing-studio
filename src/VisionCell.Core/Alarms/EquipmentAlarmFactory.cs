using VisionCell.Core.Errors;

namespace VisionCell.Core.Alarms;

public static class EquipmentAlarmFactory
{
    public static EquipmentAlarm FromFailure(
        ErrorCode errorCode,
        EquipmentArea area,
        string message,
        DateTimeOffset occurredAt,
        string? correlationId = null)
    {
        var resolvedArea = ErrorCodeCatalog.ResolveArea(errorCode, area);
        return new EquipmentAlarm(
            Guid.NewGuid(),
            errorCode.Code,
            ErrorCodeCatalog.ResolveSeverity(errorCode),
            resolvedArea,
            string.IsNullOrWhiteSpace(message) ? errorCode.Message : message,
            occurredAt,
            correlationId: correlationId);
    }
}
