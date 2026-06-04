using VisionCell.Core.Alarms;

namespace VisionCell.Core.Errors;

public sealed record ErrorCodeCatalogEntry(
    ErrorCode ErrorCode,
    EquipmentAlarmSeverity Severity,
    EquipmentArea Area,
    string RecoveryAction)
{
    public string Code => ErrorCode.Code;
    public string Cause => ErrorCode.Message.TrimEnd('.');
}
