using System.Globalization;
using VisionCell.Core.Primitives;

namespace VisionCell.Motion.Commands;

public sealed record JogMotionTarget(AxisId AxisId, MotionDirection Direction, double Step)
{
    public IReadOnlyDictionary<string, string> ToParameters()
    {
        return new Dictionary<string, string>
        {
            [MotionCommandParameterKeys.Axis] = AxisId.ToString(),
            [MotionCommandParameterKeys.Direction] = Direction == MotionDirection.Positive ? "+" : "-",
            [MotionCommandParameterKeys.Step] = Step.ToString("0.###", CultureInfo.InvariantCulture)
        };
    }
}
