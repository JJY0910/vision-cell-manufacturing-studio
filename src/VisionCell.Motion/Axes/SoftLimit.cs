using VisionCell.Core.Primitives;

namespace VisionCell.Motion.Axes;

public sealed record SoftLimit(AxisId AxisId, double Minimum, double Maximum, string Unit)
{
    public bool Contains(double value) => value >= Minimum && value <= Maximum;
}
