using VisionCell.Core.Primitives;

namespace VisionCell.Vision.Inspection;

public sealed record HeightMapInspectionRequest
{
    public HeightMapInspectionRequest(
        CorrelationId correlationId,
        string recipeId,
        string recipeVersion,
        VisionHeightMap heightMap,
        IReadOnlyList<VisionRoi> rois,
        HeightMapInspectionParameters parameters,
        TimeSpan timeout,
        DateTimeOffset requestedAt)
    {
        if (string.IsNullOrWhiteSpace(recipeId))
        {
            throw new ArgumentException("Recipe ID is required.", nameof(recipeId));
        }

        if (string.IsNullOrWhiteSpace(recipeVersion))
        {
            throw new ArgumentException("Recipe version is required.", nameof(recipeVersion));
        }

        ArgumentNullException.ThrowIfNull(heightMap);
        ArgumentNullException.ThrowIfNull(rois);
        ArgumentNullException.ThrowIfNull(parameters);

        if (rois.Count == 0)
        {
            throw new ArgumentException("At least one ROI is required.", nameof(rois));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Height-map inspection timeout must be greater than zero.");
        }

        CorrelationId = correlationId;
        RecipeId = recipeId;
        RecipeVersion = recipeVersion;
        HeightMap = heightMap;
        Rois = rois.ToArray();
        Parameters = parameters;
        Timeout = timeout;
        RequestedAt = requestedAt;
    }

    public CorrelationId CorrelationId { get; init; }
    public string RecipeId { get; init; }
    public string RecipeVersion { get; init; }
    public VisionHeightMap HeightMap { get; init; }
    public IReadOnlyList<VisionRoi> Rois { get; init; }
    public HeightMapInspectionParameters Parameters { get; init; }
    public TimeSpan Timeout { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
}
