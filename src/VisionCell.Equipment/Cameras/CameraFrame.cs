namespace VisionCell.Equipment.Cameras;

public sealed record CameraFrame
{
    public CameraFrame(
        string cameraName,
        int width,
        int height,
        int stride,
        CameraPixelFormat pixelFormat,
        byte[] pixels,
        DateTimeOffset grabbedAt,
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (string.IsNullOrWhiteSpace(cameraName))
        {
            throw new ArgumentException("Camera name is required.", nameof(cameraName));
        }

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

        CameraName = cameraName;
        Width = width;
        Height = height;
        Stride = stride;
        PixelFormat = pixelFormat;
        Pixels = pixels.ToArray();
        GrabbedAt = grabbedAt;
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
    }

    public string CameraName { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int Stride { get; init; }
    public CameraPixelFormat PixelFormat { get; init; }
    public byte[] Pixels { get; init; }
    public DateTimeOffset GrabbedAt { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
}
