using VisionCell.Core.Primitives;

namespace VisionCell.App.Modules.Motion.ViewModels;

public sealed record MotionAxisStatusViewModel(
    AxisId AxisId,
    string Label,
    double Position,
    double Target,
    string Unit,
    double SoftLimitMin,
    double SoftLimitMax,
    bool IsHomed,
    bool ServoOn,
    bool IsMoving,
    string Alarm)
{
    public string PositionText => $"Pos {Position:0.000} {Unit}";
    public string TargetText => $"Target {Target:0.000} {Unit}";
    public string SoftLimitText => $"Limit {SoftLimitMin:0.###}..{SoftLimitMax:0.###} {Unit}";
    public string HomeState => IsHomed ? "Homed" : "Not homed";
    public string ServoState => ServoOn ? "Servo On" : "Servo Off";
    public string MotionState => IsMoving ? "Moving" : "Idle";
}
