using VisionCell.Core.Primitives;

namespace VisionCell.Motion.Axes;

public sealed record AxisSnapshot(
    AxisId AxisId,
    double Position,
    double Target,
    bool IsHomed,
    bool ServoOn,
    bool IsMoving,
    AxisAlarm? Alarm,
    SoftLimit SoftLimit,
    MotionProfile Profile);
