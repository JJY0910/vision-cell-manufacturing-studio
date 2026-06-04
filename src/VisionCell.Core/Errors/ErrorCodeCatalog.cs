using VisionCell.Core.Alarms;

namespace VisionCell.Core.Errors;

public static class ErrorCodeCatalog
{
    private static readonly ErrorCodeCatalogEntry[] CatalogEntries =
    [
        new(
            ErrorCode.ControllerConnectionFailed,
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Equipment,
            "Check the controller profile and reconnect the simulator/controller."),
        new(
            ErrorCode.HeartbeatLost,
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Equipment,
            "Reconnect the controller after heartbeat is restored."),
        new(
            ErrorCode.EmergencyStopActive,
            EquipmentAlarmSeverity.Critical,
            EquipmentArea.Safety,
            "Release EStop, verify the safety cause is clear, then run the validated reset path."),
        new(
            ErrorCode.DoorOpen,
            EquipmentAlarmSeverity.Warning,
            EquipmentArea.Safety,
            "Close the safety door before restarting automatic operation."),
        new(
            ErrorCode.CommandTimeout,
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Equipment,
            "Check command timeout conditions and retry after equipment state is stable."),
        new(
            ErrorCode.CommandCancelled,
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Equipment,
            "Confirm the operator cancellation was intentional before restarting the command."),
        new(
            ErrorCode.CommandRejected,
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Equipment,
            "Clear the interlock cause before retrying the rejected command."),
        new(
            ErrorCode.AirPressureLow,
            EquipmentAlarmSeverity.Critical,
            EquipmentArea.Safety,
            "Restore air pressure and verify the safety summary before reset."),
        new(
            ErrorCode.VacuumLoss,
            EquipmentAlarmSeverity.Warning,
            EquipmentArea.Safety,
            "Check vacuum line, product pickup, and fixture state before retrying."),
        new(
            ErrorCode.ServoOff,
            EquipmentAlarmSeverity.Warning,
            EquipmentArea.Motion,
            "Turn servo on before motion commands."),
        new(
            ErrorCode.AxisNotHomed,
            EquipmentAlarmSeverity.Warning,
            EquipmentArea.Motion,
            "Home the axis before move or jog commands."),
        new(
            ErrorCode.MotionTimeout,
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Motion,
            "Check soft limit, axis load, and motion timeout cause before reset."),
        new(
            ErrorCode.SoftLimitExceeded,
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Motion,
            "Change the target position inside configured soft limits."),
        new(
            ErrorCode.ServoAlarm,
            EquipmentAlarmSeverity.Critical,
            EquipmentArea.Motion,
            "Clear the servo drive alarm and verify axis state before reset."),
        new(
            ErrorCode.CameraGrabTimeout,
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Camera,
            "Retry grab after confirming camera trigger and timeout conditions."),
        new(
            ErrorCode.CameraNotReady,
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Camera,
            "Check camera readiness before starting inspection."),
        new(
            ErrorCode.CameraGrabFailed,
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Camera,
            "Check camera state and simulator failure injection before retrying grab."),
        new(
            ErrorCode.RecipeValidationFailed,
            EquipmentAlarmSeverity.Warning,
            EquipmentArea.Inspection,
            "Fix Recipe validation issues before running inspection."),
        new(
            ErrorCode.InspectionFailed,
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Inspection,
            "Review image, ROI, and Recipe parameters before rerun."),
        new(
            ErrorCode.PersistenceFailed,
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Database,
            "Check the database path and storage availability before retrying persistence.")
    ];

    public static IReadOnlyList<ErrorCodeCatalogEntry> Entries { get; } = CatalogEntries;

    public static ErrorCodeCatalogEntry? Find(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return CatalogEntries.FirstOrDefault(entry =>
            string.Equals(entry.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetRecoveryAction(string code, EquipmentArea fallbackArea)
    {
        return Find(code)?.RecoveryAction ?? GetAreaFallback(fallbackArea);
    }

    internal static EquipmentArea ResolveArea(ErrorCode errorCode, EquipmentArea fallback)
    {
        var catalogArea = Find(errorCode.Code)?.Area;
        if (catalogArea is not null)
        {
            return catalogArea.Value;
        }

        if (errorCode.Code.StartsWith("MOT-", StringComparison.OrdinalIgnoreCase))
        {
            return EquipmentArea.Motion;
        }

        if (errorCode.Code.StartsWith("CAM-", StringComparison.OrdinalIgnoreCase))
        {
            return EquipmentArea.Camera;
        }

        if (errorCode.Code.StartsWith("VIS-", StringComparison.OrdinalIgnoreCase))
        {
            return EquipmentArea.Inspection;
        }

        if (errorCode.Code.StartsWith("DB-", StringComparison.OrdinalIgnoreCase))
        {
            return EquipmentArea.Database;
        }

        return fallback;
    }

    internal static EquipmentAlarmSeverity ResolveSeverity(ErrorCode errorCode)
    {
        return Find(errorCode.Code)?.Severity ?? EquipmentAlarmSeverity.Error;
    }

    private static string GetAreaFallback(EquipmentArea area)
    {
        return area switch
        {
            EquipmentArea.Safety => "Verify safety inputs and keep hardware reset separate from acknowledgement.",
            EquipmentArea.Motion => "Review motion state and clear axis cause before retrying.",
            EquipmentArea.Camera => "Review camera readiness and acquisition state before retrying.",
            EquipmentArea.Inspection => "Review Recipe, image, and judgment context before rerun.",
            EquipmentArea.Database => "Review storage and repository availability before retrying.",
            _ => "Review equipment status and record the operator recovery action memo."
        };
    }
}
