namespace VisionCell.Vision.Inspection;

public sealed record VisionInspectionParameters(
    double MissingAreaThreshold,
    int OffsetTolerancePx,
    double ScratchThreshold);
