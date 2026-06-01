namespace VisionCell.Vision.Inspection;

public interface IVisionInspectionEngine
{
    Task<VisionInspectionResult> InspectAsync(
        VisionInspectionRequest request,
        CancellationToken cancellationToken);
}
