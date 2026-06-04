using VisionCell.Core.Alarms;
using VisionCell.Core.Errors;

namespace VisionCell.App.Modules.Alarm.ViewModels;

internal static class AlarmRecoveryGuidance
{
    public static string GetHint(string code, EquipmentArea area)
    {
        return ErrorCodeCatalog.GetRecoveryAction(code, area);
    }
}
