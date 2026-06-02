using VisionCell.Vision.Inspection;

namespace VisionCell.Application.Inspection;

public interface IInspectionArtifactWriter
{
    Task<InspectionArtifactWriteResult> WriteAsync(
        InspectionArtifactWriteRequest request,
        CancellationToken cancellationToken);
}

public interface IInspectionArtifactReader
{
    Task<InspectionArtifactMetadata> ReadMetadataAsync(
        string? artifactPath,
        CancellationToken cancellationToken);

    Task<InspectionArtifactPreviewResult> ReadPreviewAsync(
        string? artifactPath,
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

public sealed record InspectionArtifactMetadata(
    InspectionArtifactMetadataStatus Status,
    string DisplayPath,
    long? SizeBytes,
    DateTimeOffset? LastModifiedAt,
    string Message)
{
    public bool IsAvailable => Status == InspectionArtifactMetadataStatus.Available;

    public static InspectionArtifactMetadata NotRecorded()
    {
        return new InspectionArtifactMetadata(
            InspectionArtifactMetadataStatus.NotRecorded,
            "-",
            SizeBytes: null,
            LastModifiedAt: null,
            "Artifact path not recorded.");
    }

    public static InspectionArtifactMetadata Available(
        string displayPath,
        long sizeBytes,
        DateTimeOffset lastModifiedAt)
    {
        return new InspectionArtifactMetadata(
            InspectionArtifactMetadataStatus.Available,
            displayPath,
            sizeBytes,
            lastModifiedAt,
            "Artifact available.");
    }

    public static InspectionArtifactMetadata Missing(string displayPath)
    {
        return new InspectionArtifactMetadata(
            InspectionArtifactMetadataStatus.Missing,
            displayPath,
            SizeBytes: null,
            LastModifiedAt: null,
            "Artifact file missing.");
    }

    public static InspectionArtifactMetadata UnsafePath(string displayPath)
    {
        return new InspectionArtifactMetadata(
            InspectionArtifactMetadataStatus.UnsafePath,
            displayPath,
            SizeBytes: null,
            LastModifiedAt: null,
            "Artifact path rejected by safety policy.");
    }

    public static InspectionArtifactMetadata Unavailable(string displayPath, string message)
    {
        return new InspectionArtifactMetadata(
            InspectionArtifactMetadataStatus.Unavailable,
            displayPath,
            SizeBytes: null,
            LastModifiedAt: null,
            message);
    }
}

public enum InspectionArtifactMetadataStatus
{
    NotRecorded,
    Available,
    Missing,
    UnsafePath,
    Unavailable
}

public sealed record InspectionArtifactPreviewResult(
    InspectionArtifactMetadataStatus Status,
    string DisplayPath,
    int Width,
    int Height,
    int Stride,
    InspectionArtifactPreviewPixelFormat PixelFormat,
    byte[] Pixels,
    string Message)
{
    public bool HasImage => Status == InspectionArtifactMetadataStatus.Available && Pixels.Length > 0;

    public static InspectionArtifactPreviewResult Available(
        string displayPath,
        int width,
        int height,
        int stride,
        InspectionArtifactPreviewPixelFormat pixelFormat,
        byte[] pixels)
    {
        return new InspectionArtifactPreviewResult(
            InspectionArtifactMetadataStatus.Available,
            displayPath,
            width,
            height,
            stride,
            pixelFormat,
            pixels,
            "Artifact preview available.");
    }

    public static InspectionArtifactPreviewResult FromMetadata(InspectionArtifactMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new InspectionArtifactPreviewResult(
            metadata.Status,
            metadata.DisplayPath,
            Width: 0,
            Height: 0,
            Stride: 0,
            InspectionArtifactPreviewPixelFormat.Bgra32,
            Array.Empty<byte>(),
            metadata.Message);
    }

    public static InspectionArtifactPreviewResult Unavailable(string displayPath, string message)
    {
        return new InspectionArtifactPreviewResult(
            InspectionArtifactMetadataStatus.Unavailable,
            displayPath,
            Width: 0,
            Height: 0,
            Stride: 0,
            InspectionArtifactPreviewPixelFormat.Bgra32,
            Array.Empty<byte>(),
            message);
    }
}

public enum InspectionArtifactPreviewPixelFormat
{
    Bgra32
}
