using System.Globalization;

namespace VisionCell.Motion.Commands;

public sealed record AbsoluteMoveTarget(double X, double Y, double Z, double Theta)
{
    public IReadOnlyDictionary<string, string> ToParameters()
    {
        return new Dictionary<string, string>
        {
            [MotionCommandParameterKeys.X] = X.ToString("0.###", CultureInfo.InvariantCulture),
            [MotionCommandParameterKeys.Y] = Y.ToString("0.###", CultureInfo.InvariantCulture),
            [MotionCommandParameterKeys.Z] = Z.ToString("0.###", CultureInfo.InvariantCulture),
            [MotionCommandParameterKeys.Theta] = Theta.ToString("0.###", CultureInfo.InvariantCulture)
        };
    }
}
