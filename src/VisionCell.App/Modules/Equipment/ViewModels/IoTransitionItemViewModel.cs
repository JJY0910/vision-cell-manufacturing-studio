using VisionCell.Equipment.Io;

namespace VisionCell.App.Modules.Equipment.ViewModels;

public sealed record IoTransitionItemViewModel(
    DateTimeOffset ChangedAt,
    string Name,
    string Address,
    IoBitDirection Direction,
    bool PreviousValue,
    bool CurrentValue,
    bool PreviousForced,
    bool CurrentForced,
    string Source,
    string? CorrelationId)
{
    public string TimeText => ChangedAt.ToLocalTime().ToString("HH:mm:ss");
    public string PreviousState => FormatState(PreviousValue, PreviousForced);
    public string CurrentState => FormatState(CurrentValue, CurrentForced);
    public string CorrelationText => string.IsNullOrWhiteSpace(CorrelationId) ? "-" : CorrelationId;

    public static IoTransitionItemViewModel FromRecord(IoTransitionRecord transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        return new IoTransitionItemViewModel(
            transition.ChangedAt,
            transition.Name,
            transition.Address,
            transition.Direction,
            transition.PreviousValue,
            transition.CurrentValue,
            transition.PreviousForced,
            transition.CurrentForced,
            transition.Source,
            transition.CorrelationId);
    }

    private static string FormatState(bool value, bool isForced)
    {
        return isForced
            ? $"{(value ? "On" : "Off")} / Forced"
            : value ? "On" : "Off";
    }
}
