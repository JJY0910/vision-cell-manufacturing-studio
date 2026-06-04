using VisionCell.Core.Errors;

namespace VisionCell.App.Modules.Alarm.ViewModels;

public sealed class AlarmErrorCodeCatalogItemViewModel
{
    public AlarmErrorCodeCatalogItemViewModel(ErrorCodeCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Code = entry.Code;
        Severity = entry.Severity.ToString();
        Area = entry.Area.ToString();
        Cause = entry.Cause;
        RecoveryAction = entry.RecoveryAction;
    }

    public string Code { get; }
    public string Severity { get; }
    public string Area { get; }
    public string Cause { get; }
    public string RecoveryAction { get; }
}
