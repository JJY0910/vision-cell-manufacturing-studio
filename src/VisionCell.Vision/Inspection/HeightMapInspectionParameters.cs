namespace VisionCell.Vision.Inspection;

public sealed record HeightMapInspectionParameters(
    double ExpectedHeight,
    double HeightToleranceLow,
    double HeightToleranceHigh,
    double LeadBentGradientTolerance);
