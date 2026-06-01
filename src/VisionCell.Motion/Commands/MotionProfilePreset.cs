namespace VisionCell.Motion.Commands;

public sealed record MotionProfilePreset(
    string Name,
    double Velocity,
    double Acceleration,
    double Deceleration,
    double Jerk,
    double ArrivalTolerance)
{
    public static MotionProfilePreset Fine { get; } = new("Fine", 10.0, 80.0, 80.0, 400.0, 0.005);
    public static MotionProfilePreset Standard { get; } = new("Standard", 50.0, 200.0, 200.0, 1000.0, 0.01);
    public static MotionProfilePreset Fast { get; } = new("Fast", 125.0, 300.0, 250.0, 1500.0, 0.02);

    public static IReadOnlyList<MotionProfilePreset> Defaults { get; } =
        new[] { Fine, Standard, Fast };
}
