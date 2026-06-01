using VisionCell.Core.Primitives;

namespace VisionCell.Motion.Axes;

public static class AxisDefaults
{
    public static IReadOnlyList<AxisSnapshot> CreatePowerOffAxes()
    {
        return new[]
        {
            Create(AxisId.X, -200.0, 200.0, "mm", MotionProfile.DefaultLinear),
            Create(AxisId.Y, -200.0, 200.0, "mm", MotionProfile.DefaultLinear),
            Create(AxisId.Z, 0.0, 100.0, "mm", MotionProfile.DefaultLinear),
            Create(AxisId.Theta, -180.0, 180.0, "deg", MotionProfile.DefaultTheta)
        };
    }

    private static AxisSnapshot Create(AxisId axisId, double minimum, double maximum, string unit, MotionProfile profile)
    {
        return new AxisSnapshot(
            axisId,
            Position: 0.0,
            Target: 0.0,
            IsHomed: false,
            ServoOn: false,
            IsMoving: false,
            Alarm: null,
            SoftLimit: new SoftLimit(axisId, minimum, maximum, unit),
            Profile: profile);
    }
}
