using VisionCell.Core.Primitives;

namespace VisionCell.Equipment.Cameras;

public sealed record CameraGrabRequest
{
    public CameraGrabRequest(
        CorrelationId correlationId,
        TimeSpan timeout,
        DateTimeOffset requestedAt,
        string recipeId,
        string recipeVersion,
        double exposureMilliseconds,
        double gain,
        int lightIntensity)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Camera grab timeout must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(recipeId))
        {
            throw new ArgumentException("Recipe ID is required.", nameof(recipeId));
        }

        if (string.IsNullOrWhiteSpace(recipeVersion))
        {
            throw new ArgumentException("Recipe version is required.", nameof(recipeVersion));
        }

        if (exposureMilliseconds <= 0.0 || double.IsNaN(exposureMilliseconds) || double.IsInfinity(exposureMilliseconds))
        {
            throw new ArgumentOutOfRangeException(nameof(exposureMilliseconds), exposureMilliseconds, "Exposure must be a finite positive value.");
        }

        if (gain <= 0.0 || double.IsNaN(gain) || double.IsInfinity(gain))
        {
            throw new ArgumentOutOfRangeException(nameof(gain), gain, "Gain must be a finite positive value.");
        }

        if (lightIntensity is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(lightIntensity), lightIntensity, "Light intensity must be between 0 and 100.");
        }

        CorrelationId = correlationId;
        Timeout = timeout;
        RequestedAt = requestedAt;
        RecipeId = recipeId;
        RecipeVersion = recipeVersion;
        ExposureMilliseconds = exposureMilliseconds;
        Gain = gain;
        LightIntensity = lightIntensity;
    }

    public CorrelationId CorrelationId { get; init; }
    public TimeSpan Timeout { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public string RecipeId { get; init; }
    public string RecipeVersion { get; init; }
    public double ExposureMilliseconds { get; init; }
    public double Gain { get; init; }
    public int LightIntensity { get; init; }
}
