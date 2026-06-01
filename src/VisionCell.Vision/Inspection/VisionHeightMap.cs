namespace VisionCell.Vision.Inspection;

public sealed record VisionHeightMap
{
    public VisionHeightMap(
        int width,
        int height,
        float[] values,
        string unit,
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Height map width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height map height must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(values);
        if (values.Length < width * height)
        {
            throw new ArgumentException("Height map value buffer is smaller than width multiplied by height.", nameof(values));
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Height map unit is required.", nameof(unit));
        }

        Width = width;
        Height = height;
        Values = values.ToArray();
        Unit = unit;
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
    }

    public int Width { get; init; }
    public int Height { get; init; }
    public float[] Values { get; init; }
    public string Unit { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
}
