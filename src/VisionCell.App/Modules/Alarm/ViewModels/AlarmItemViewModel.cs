using VisionCell.Core.Alarms;

namespace VisionCell.App.Modules.Alarm.ViewModels;

public sealed class AlarmItemViewModel
{
    public AlarmItemViewModel(EquipmentAlarm alarm)
    {
        Alarm = alarm ?? throw new ArgumentNullException(nameof(alarm));
    }

    public EquipmentAlarm Alarm { get; }
    public Guid Id => Alarm.Id;
    public string Code => Alarm.Code;
    public string Severity => Alarm.Severity.ToString();
    public string Area => Alarm.Area.ToString();
    public string Message => Alarm.Message;
    public string CorrelationId => Alarm.CorrelationId ?? "-";
    public string OccurredAtText => Alarm.OccurredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string AcknowledgedAtText => Alarm.AcknowledgedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
    public string ActionMemo => Alarm.ActionMemo ?? "-";
    public string StateText => Alarm.IsAcknowledged ? "Acknowledged" : "Active";
    public string RecoveryHint => AlarmRecoveryGuidance.GetHint(Alarm.Code, Alarm.Area);
}
