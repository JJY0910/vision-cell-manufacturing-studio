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
    string Alarm)
{
    public string PositionText => $"Pos {Position:0.000} {Unit}";
    public string TargetText => $"Target {Target:0.000} {Unit}";
    public string SoftLimitText => $"Unit {Unit}";
    public string HomeState => IsHomed ? "Homed" : "Not homed";
    public string ServoState => ServoOn ? "Servo On" : "Servo Off";
    public string MotionState => IsMoving ? "Moving" : "Idle";
}
