namespace VisionCell.Vision.Inspection;

public sealed record VisionRoi(
    string Id,
    string Name,
    int X,
    int Y,
    int Width,
    int Height);
