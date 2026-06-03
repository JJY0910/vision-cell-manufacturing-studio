namespace VisionCell.Application.Inspection;

public interface IInspectionReinspectSourceImageReadinessUseCase
{
    Task<InspectionReinspectSourceImageReadinessResult> ResolveAsync(
        InspectionReinspectPreparation preparation,
        CancellationToken cancellationToken);
}

public sealed class InspectionReinspectSourceImageReadinessUseCase : IInspectionReinspectSourceImageReadinessUseCase
{
    private const string CameraFrameScheme = "camera-frame";
    private readonly IInspectionArtifactReader? _artifactReader;

    public InspectionReinspectSourceImageReadinessUseCase(IInspectionArtifactReader? artifactReader = null)
    {
        _artifactReader = artifactReader;
    }

    public async Task<InspectionReinspectSourceImageReadinessResult> ResolveAsync(
        InspectionReinspectPreparation preparation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceImagePath = preparation.SourceImagePath?.Trim();
        if (string.IsNullOrWhiteSpace(sourceImagePath) ||
            string.Equals(sourceImagePath, "-", StringComparison.Ordinal))
        {
            return InspectionReinspectSourceImageReadinessResult.NotRecorded();
        }

        if (Uri.TryCreate(sourceImagePath, UriKind.Absolute, out var uri))
        {
            return ResolveUri(sourceImagePath, uri);
        }

        if (ContainsUnsafePathSegment(sourceImagePath))
        {
            return InspectionReinspectSourceImageReadinessResult.UnsafePath(sourceImagePath);
        }

        if (_artifactReader is null)
        {
            return InspectionReinspectSourceImageReadinessResult.SourceArtifactReaderMissing(sourceImagePath);
        }

        if (!sourceImagePath.EndsWith(".source.bmp", StringComparison.OrdinalIgnoreCase))
        {
            return InspectionReinspectSourceImageReadinessResult.UnsupportedSourceArtifactType(sourceImagePath);
        }

        var metadata = await _artifactReader.ReadMetadataAsync(sourceImagePath, cancellationToken).ConfigureAwait(false);
        return metadata.Status switch
        {
            InspectionArtifactMetadataStatus.Available =>
                InspectionReinspectSourceImageReadinessResult.SourceArtifactArchived(sourceImagePath),
            InspectionArtifactMetadataStatus.Missing =>
                InspectionReinspectSourceImageReadinessResult.SourceArtifactMissing(sourceImagePath),
            InspectionArtifactMetadataStatus.UnsafePath =>
                InspectionReinspectSourceImageReadinessResult.UnsafePath(sourceImagePath),
            _ => InspectionReinspectSourceImageReadinessResult.SourceArtifactUnavailable(sourceImagePath, metadata.Message)
        };
    }

    private static InspectionReinspectSourceImageReadinessResult ResolveUri(string sourceImagePath, Uri uri)
    {
        if (string.Equals(uri.Scheme, CameraFrameScheme, StringComparison.OrdinalIgnoreCase))
        {
            return InspectionReinspectSourceImageReadinessResult.CameraFrameReferenceOnly(sourceImagePath);
        }

        return InspectionReinspectSourceImageReadinessResult.UnsupportedUri(sourceImagePath, uri.Scheme);
    }

    private static bool ContainsUnsafePathSegment(string sourceImagePath)
    {
        var segments = sourceImagePath.Split(
            ['/', '\\'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
    }
}

public sealed record InspectionReinspectSourceImageReadinessResult(
    string SourceImagePath,
    InspectionReinspectSourceImageReadinessStatus Status,
    string ReplayInputKind,
    string StatusLabel,
    string Message)
{
    public bool CanReplaySourceImage => Status == InspectionReinspectSourceImageReadinessStatus.Ready;

    public static InspectionReinspectSourceImageReadinessResult NotRecorded()
    {
        return new InspectionReinspectSourceImageReadinessResult(
            "-",
            InspectionReinspectSourceImageReadinessStatus.NotRecorded,
            "Not recorded",
            "Source image not recorded",
            "The selected inspection result has no source image reference, so source-image replay cannot be prepared.");
    }

    public static InspectionReinspectSourceImageReadinessResult CameraFrameReferenceOnly(string sourceImagePath)
    {
        return new InspectionReinspectSourceImageReadinessResult(
            sourceImagePath,
            InspectionReinspectSourceImageReadinessStatus.FrameArchiveUnavailable,
            "Transient camera frame URI",
            "Frame archive unavailable",
            "The source image points to a captured camera-frame URI. Raw source pixels are not archived for Offline Debug replay yet.");
    }

    public static InspectionReinspectSourceImageReadinessResult SourceArtifactReaderMissing(string sourceImagePath)
    {
        return new InspectionReinspectSourceImageReadinessResult(
            sourceImagePath,
            InspectionReinspectSourceImageReadinessStatus.SourceArtifactReaderMissing,
            "Relative source path",
            "Source artifact reader missing",
            "The source image reference looks like a replay candidate, but no source-image artifact reader or replay runner is implemented.");
    }

    public static InspectionReinspectSourceImageReadinessResult SourceArtifactArchived(string sourceImagePath)
    {
        return new InspectionReinspectSourceImageReadinessResult(
            sourceImagePath,
            InspectionReinspectSourceImageReadinessStatus.SourceArtifactArchived,
            "Archived source BMP",
            "Source artifact archived",
            "The source image artifact exists for future replay input, but no source-image replay runner is implemented.");
    }

    public static InspectionReinspectSourceImageReadinessResult SourceArtifactMissing(string sourceImagePath)
    {
        return new InspectionReinspectSourceImageReadinessResult(
            sourceImagePath,
            InspectionReinspectSourceImageReadinessStatus.SourceArtifactMissing,
            "Missing source artifact",
            "Source artifact missing",
            "The source image reference points to an artifact path, but the file is not available for replay preparation.");
    }

    public static InspectionReinspectSourceImageReadinessResult SourceArtifactUnavailable(
        string sourceImagePath,
        string message)
    {
        return new InspectionReinspectSourceImageReadinessResult(
            sourceImagePath,
            InspectionReinspectSourceImageReadinessStatus.SourceArtifactUnavailable,
            "Unavailable source artifact",
            "Source artifact unavailable",
            message);
    }

    public static InspectionReinspectSourceImageReadinessResult UnsupportedUri(string sourceImagePath, string scheme)
    {
        return new InspectionReinspectSourceImageReadinessResult(
            sourceImagePath,
            InspectionReinspectSourceImageReadinessStatus.UnsupportedUri,
            $"{scheme} URI",
            "Unsupported source URI",
            "Only an explicit source-image replay contract may resolve persisted image inputs. This URI scheme is not supported.");
    }

    public static InspectionReinspectSourceImageReadinessResult UnsupportedSourceArtifactType(string sourceImagePath)
    {
        return new InspectionReinspectSourceImageReadinessResult(
            sourceImagePath,
            InspectionReinspectSourceImageReadinessStatus.UnsupportedSourceArtifactType,
            "Unsupported source artifact",
            "Unsupported source artifact type",
            "Source replay readiness currently accepts archived .source.bmp artifacts only.");
    }

    public static InspectionReinspectSourceImageReadinessResult UnsafePath(string sourceImagePath)
    {
        return new InspectionReinspectSourceImageReadinessResult(
            sourceImagePath,
            InspectionReinspectSourceImageReadinessStatus.UnsafePath,
            "Unsafe path",
            "Source path rejected",
            "The source image reference contains parent path traversal and cannot be used for replay preparation.");
    }
}

public enum InspectionReinspectSourceImageReadinessStatus
{
    Ready,
    SourceArtifactArchived,
    SourceArtifactMissing,
    SourceArtifactUnavailable,
    FrameArchiveUnavailable,
    SourceArtifactReaderMissing,
    NotRecorded,
    UnsupportedUri,
    UnsupportedSourceArtifactType,
    UnsafePath
}
