using VisionCell.Vision.Inspection;

namespace VisionCell.Application.Inspection;

public interface IInspectionArtifactWriter
{
    Task<InspectionArtifactWriteResult> WriteAsync(
        InspectionArtifactWriteRequest request,
        CancellationToken cancellationToken);
}

public sealed record InspectionArtifactWriteRequest(
    Guid ResultId,
    string CorrelationId,
    string LotId,
    string RecipeId,
    string RecipeVersion,
    Judgment Judgment,
    InspectionArtifactImageFrame SourceImage,
    InspectionArtifactHeightMap HeightMap,
    IReadOnlyList<InspectionArtifactRoi> Rois,
    IReadOnlyList<InspectionDefectRecord> Defects,
    DateTimeOffset CreatedAt);

public sealed record InspectionArtifactImageFrame(
    int Width,
    int Height,
    int Stride,
    byte[] Gray8Pixels);

public sealed record InspectionArtifactHeightMap(
    int Width,
    int Height,
    float[] Values,
    string Unit);

public sealed record InspectionArtifactRoi(
    string Id,
    string Name,
    int X,
    int Y,
    int Width,
    int Height);

public sealed record InspectionArtifactWriteResult(
    string OverlayImagePath,
    string HeightMapPath);
