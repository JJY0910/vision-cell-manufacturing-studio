namespace VisionCell.Vision.Inspection;

public sealed class SyntheticHeightMapFactory
{
    public VisionHeightMap CreateFromGray8(
        VisionImageFrame image,
        double expectedHeight,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.PixelFormat != VisionPixelFormat.Gray8)
        {
            throw new NotSupportedException($"Unsupported image pixel format for synthetic height-map generation: {image.PixelFormat}.");
        }

        if (!double.IsFinite(expectedHeight) || expectedHeight <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedHeight), expectedHeight, "Expected height must be a finite positive value.");
        }

        var values = new float[image.Width * image.Height];
        var amplitude = Math.Max(0.01, expectedHeight * 0.04);
        for (var y = 0; y < image.Height; y++)
        {
            var sourceOffset = y * image.Stride;
            var targetOffset = y * image.Width;
            for (var x = 0; x < image.Width; x++)
            {
                var normalized = (image.Pixels[sourceOffset + x] / 255.0) - 0.5;
                values[targetOffset + x] = (float)(expectedHeight + (normalized * amplitude));
            }
        }

        var mergedMetadata = new Dictionary<string, string>(image.Metadata, StringComparer.Ordinal)
        {
            ["HeightMapKind"] = "SyntheticFromGray8",
            ["ExpectedHeight"] = expectedHeight.ToString("0.###")
        };

        if (metadata is not null)
        {
            foreach (var item in metadata)
            {
                mergedMetadata[item.Key] = item.Value;
            }
        }

        return new VisionHeightMap(
            image.Width,
            image.Height,
            values,
            "mm",
            mergedMetadata);
    }
}
