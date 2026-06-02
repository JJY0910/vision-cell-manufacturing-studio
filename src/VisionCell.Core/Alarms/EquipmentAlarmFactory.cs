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
        var resolvedArea = ResolveArea(errorCode, area);
        return new EquipmentAlarm(
            Guid.NewGuid(),
            errorCode.Code,
            ResolveSeverity(errorCode),
            resolvedArea,
            string.IsNullOrWhiteSpace(message) ? errorCode.Message : message,
            occurredAt,
            correlationId: correlationId);
    }

    private static EquipmentArea ResolveArea(ErrorCode errorCode, EquipmentArea fallback)
    {
        if (errorCode.Code.StartsWith("MOT-", StringComparison.Ordinal))
        {
            return EquipmentArea.Motion;
        }

        if (errorCode.Code.StartsWith("CAM-", StringComparison.Ordinal))
        {
            return EquipmentArea.Camera;
        }

        if (errorCode.Code.StartsWith("VIS-", StringComparison.Ordinal))
        {
            return EquipmentArea.Inspection;
        }

        if (errorCode.Code.StartsWith("DB-", StringComparison.Ordinal))
        {
            return EquipmentArea.Database;
        }

        return errorCode.Code is "EQP-003" or "EQP-004"
            ? EquipmentArea.Safety
            : fallback;
    }

    private static EquipmentAlarmSeverity ResolveSeverity(ErrorCode errorCode)
    {
        return errorCode.Code switch
        {
            "EQP-003" => EquipmentAlarmSeverity.Critical,
            "EQP-004" => EquipmentAlarmSeverity.Warning,
            "MOT-001" => EquipmentAlarmSeverity.Warning,
            "MOT-002" => EquipmentAlarmSeverity.Warning,
            "VIS-001" => EquipmentAlarmSeverity.Warning,
            _ => EquipmentAlarmSeverity.Error
        };
    }
}
