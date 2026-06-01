namespace VisionCell.Motion.Teaching;

public sealed record PositionTolerance(double X, double Y, double Z, double Theta)
{
    public static PositionTolerance Default { get; } = new(0.01, 0.01, 0.01, 0.01);
}
