namespace VisionCell.Vision.Inspection;

public sealed record Defect(
    string Type,
    double Score,
    int X,
    int Y,
    int Width,
    int Height,
    string Message);
