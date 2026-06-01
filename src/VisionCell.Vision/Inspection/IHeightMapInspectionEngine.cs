namespace VisionCell.Vision.Inspection;

public interface IHeightMapInspectionEngine
{
    Task<VisionInspectionResult> InspectAsync(
        HeightMapInspectionRequest request,
        CancellationToken cancellationToken);
}
