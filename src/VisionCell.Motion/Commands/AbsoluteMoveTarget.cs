using System.Globalization;

namespace VisionCell.Motion.Commands;

public sealed record AbsoluteMoveTarget(
    double X,
    double Y,
    double Z,
    double Theta,
    double Velocity = 50.0,
    double Acceleration = 200.0,
    double Deceleration = 200.0,
    double Jerk = 1000.0,
    double ArrivalTolerance = 0.01,
    string ProfilePreset = "Standard")
{
    public IReadOnlyDictionary<string, string> ToParameters()
    {
        return new Dictionary<string, string>
        {
            [MotionCommandParameterKeys.X] = X.ToString("0.###", CultureInfo.InvariantCulture),
            [MotionCommandParameterKeys.Y] = Y.ToString("0.###", CultureInfo.InvariantCulture),
            [MotionCommandParameterKeys.Z] = Z.ToString("0.###", CultureInfo.InvariantCulture),
            [MotionCommandParameterKeys.Theta] = Theta.ToString("0.###", CultureInfo.InvariantCulture),
            [MotionCommandParameterKeys.Velocity] = Velocity.ToString("0.###", CultureInfo.InvariantCulture),
            [MotionCommandParameterKeys.Acceleration] = Acceleration.ToString("0.###", CultureInfo.InvariantCulture),
            [MotionCommandParameterKeys.Deceleration] = Deceleration.ToString("0.###", CultureInfo.InvariantCulture),
            [MotionCommandParameterKeys.Jerk] = Jerk.ToString("0.###", CultureInfo.InvariantCulture),
            [MotionCommandParameterKeys.ArrivalTolerance] = ArrivalTolerance.ToString("0.###", CultureInfo.InvariantCulture),
            [MotionCommandParameterKeys.ProfilePreset] = ProfilePreset
        };
    }
}
