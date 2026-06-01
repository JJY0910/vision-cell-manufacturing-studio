namespace VisionCell.Vision.Inspection;

public sealed record VisionImageFrame
{
    public VisionImageFrame(
        int width,
        int height,
        int stride,
        VisionPixelFormat pixelFormat,
        byte[] pixels,
        DateTimeOffset capturedAt,
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Frame width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Frame height must be greater than zero.");
        }

        if (stride < width)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), stride, "Frame stride must be at least the frame width.");
        }

        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length < stride * height)
        {
            throw new ArgumentException("Frame pixel buffer is smaller than stride multiplied by height.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Stride = stride;
        PixelFormat = pixelFormat;
        Pixels = pixels.ToArray();
        CapturedAt = capturedAt;
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
    }

    public int Width { get; init; }
    public int Height { get; init; }
    public int Stride { get; init; }
    public VisionPixelFormat PixelFormat { get; init; }
    public byte[] Pixels { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
}
