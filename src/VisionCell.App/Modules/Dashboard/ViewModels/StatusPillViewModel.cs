using VisionCell.Core.Events;

namespace VisionCell.App.Modules.Dashboard.ViewModels;

public sealed record StatusPillViewModel(string Label, string Value, SystemEventSeverity Severity);
