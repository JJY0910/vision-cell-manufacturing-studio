namespace VisionCell.Motion.Axes;

public sealed record MotionProfile(
    double Velocity,
    double Acceleration,
    double Deceleration,
    double Jerk,
    string Unit)
{
    public static MotionProfile DefaultLinear { get; } = new(50.0, 200.0, 200.0, 1000.0, "mm/s");
    public static MotionProfile DefaultTheta { get; } = new(45.0, 180.0, 180.0, 720.0, "deg/s");
}
