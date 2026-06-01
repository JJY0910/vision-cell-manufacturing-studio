using VisionCell.Core.Commands;

namespace VisionCell.App.Modules.Dashboard.ViewModels;

public sealed record CommandAvailabilityViewModel(
    CommandKind Command,
    string Label,
    bool IsEnabled,
    string DisabledReason)
{
    public string State => IsEnabled ? "Enabled" : "Disabled";
    public string Reason => IsEnabled ? "Ready" : DisabledReason;
}
