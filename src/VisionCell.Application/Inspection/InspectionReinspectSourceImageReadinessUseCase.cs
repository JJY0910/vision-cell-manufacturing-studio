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

    public Task<InspectionReinspectSourceImageReadinessResult> ResolveAsync(
        InspectionReinspectPreparation preparation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceImagePath = preparation.SourceImagePath?.Trim();
        if (string.IsNullOrWhiteSpace(sourceImagePath) ||
            string.Equals(sourceImagePath, "-", StringComparison.Ordinal))
        {
            return Task.FromResult(InspectionReinspectSourceImageReadinessResult.NotRecorded());
        }

        if (Uri.TryCreate(sourceImagePath, UriKind.Absolute, out var uri))
        {
            return Task.FromResult(ResolveUri(sourceImagePath, uri));
        }

        if (ContainsUnsafePathSegment(sourceImagePath))
        {
            return Task.FromResult(InspectionReinspectSourceImageReadinessResult.UnsafePath(sourceImagePath));
        }

        return Task.FromResult(InspectionReinspectSourceImageReadinessResult.SourceArtifactReaderMissing(sourceImagePath));
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

    public static InspectionReinspectSourceImageReadinessResult UnsupportedUri(string sourceImagePath, string scheme)
    {
        return new InspectionReinspectSourceImageReadinessResult(
            sourceImagePath,
            InspectionReinspectSourceImageReadinessStatus.UnsupportedUri,
            $"{scheme} URI",
            "Unsupported source URI",
            "Only an explicit source-image replay contract may resolve persisted image inputs. This URI scheme is not supported.");
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
    FrameArchiveUnavailable,
    SourceArtifactReaderMissing,
    NotRecorded,
    UnsupportedUri,
    UnsafePath
}
