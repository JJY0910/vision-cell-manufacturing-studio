using VisionCell.Motion.Teaching;

namespace VisionCell.App.Modules.Teaching.ViewModels;

public sealed class TeachingPointItemViewModel
{
    public TeachingPointItemViewModel(TeachingPoint point)
    {
        Point = point ?? throw new ArgumentNullException(nameof(point));
    }

    public TeachingPoint Point { get; }
    public Guid Id => Point.Id;
    public string Name => Point.Name;
    public string Role => Point.Role.ToString();
    public string Memo => string.IsNullOrWhiteSpace(Point.Memo) ? "-" : Point.Memo;
    public string PositionText => $"X {Point.Position.X:0.###}, Y {Point.Position.Y:0.###}, Z {Point.Position.Z:0.###}, T {Point.Position.Theta:0.###}";
    public string ToleranceText => $"X {Point.Tolerance.X:0.###}, Y {Point.Tolerance.Y:0.###}, Z {Point.Tolerance.Z:0.###}, T {Point.Tolerance.Theta:0.###}";
    public string UpdatedAtText => Point.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
