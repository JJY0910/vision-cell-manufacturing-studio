using VisionCell.Core.Primitives;

namespace VisionCell.App.Modules.Dashboard.ViewModels;

public sealed record AxisStatusViewModel(
    AxisId AxisId,
    string Label,
    double Position,
    double Target,
    string Unit,
    bool IsHomed,
    bool ServoOn,
    bool IsMoving,
    string Alarm);
