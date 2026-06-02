using System.Globalization;
using VisionCell.Application.Inspection;
using VisionCell.Vision.Inspection;

namespace VisionCell.Persistence.Inspection;

public sealed class FileSystemInspectionArtifactWriter : IInspectionArtifactWriter, IInspectionArtifactReader
{
    private const string RelativeRoot = "inspection-artifacts";
    private static readonly Rgb Cyan = new(0, 210, 255);
    private static readonly Rgb Green = new(32, 180, 90);
    private static readonly Rgb Red = new(230, 48, 48);
    private static readonly Rgb Yellow = new(255, 210, 42);

    private readonly string _rootDirectory;
    private readonly string _rootDirectoryWithSeparator;

    public FileSystemInspectionArtifactWriter(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Artifact root path is required.", nameof(rootDirectory));
        }

        _rootDirectory = Path.GetFullPath(rootDirectory);
        _rootDirectoryWithSeparator = _rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _rootDirectory
            : _rootDirectory + Path.DirectorySeparatorChar;
    }

    public async Task<InspectionArtifactWriteResult> WriteAsync(
        InspectionArtifactWriteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateImage(request.SourceImage);
        ValidateHeightMap(request.HeightMap);

        var dateSegment = request.CreatedAt.ToUniversalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var directory = GetSafeDirectory(dateSegment);
        Directory.CreateDirectory(directory);

        var resultId = request.ResultId.ToString("N");
        var overlayFileName = $"{resultId}.overlay.bmp";
        var heightMapFileName = $"{resultId}.height.bmp";
        var overlayAbsolutePath = GetSafeFilePath(directory, overlayFileName);
        var heightMapAbsolutePath = GetSafeFilePath(directory, heightMapFileName);

        await File.WriteAllBytesAsync(
            overlayAbsolutePath,
            RenderOverlay(request),
            cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(
            heightMapAbsolutePath,
            RenderHeightMap(request.HeightMap),
            cancellationToken).ConfigureAwait(false);

        return new InspectionArtifactWriteResult(
            ToRelativePath(dateSegment, overlayFileName),
            ToRelativePath(dateSegment, heightMapFileName));
    }

    public Task<InspectionArtifactMetadata> ReadMetadataAsync(
        string? artifactPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(artifactPath) ||
            string.Equals(artifactPath.Trim(), "-", StringComparison.Ordinal))
        {
            return Task.FromResult(InspectionArtifactMetadata.NotRecorded());
        }

        var displayPath = artifactPath.Trim();
        if (!TryResolveArtifactPath(displayPath, out var absolutePath))
        {
            return Task.FromResult(InspectionArtifactMetadata.UnsafePath(displayPath));
        }

        try
        {
            var info = new FileInfo(absolutePath);
            if (!info.Exists)
            {
                return Task.FromResult(InspectionArtifactMetadata.Missing(displayPath));
            }

            return Task.FromResult(InspectionArtifactMetadata.Available(
                displayPath,
                info.Length,
                new DateTimeOffset(info.LastWriteTimeUtc)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Task.FromResult(InspectionArtifactMetadata.Unavailable(
                displayPath,
                $"Artifact metadata unavailable: {ex.Message}"));
        }
    }

    public async Task<InspectionArtifactPreviewResult> ReadPreviewAsync(
        string? artifactPath,
        CancellationToken cancellationToken)
    {
        var metadata = await ReadMetadataAsync(artifactPath, cancellationToken).ConfigureAwait(false);
        if (!metadata.IsAvailable)
        {
            return InspectionArtifactPreviewResult.FromMetadata(metadata);
        }

        if (!TryResolveArtifactPath(metadata.DisplayPath, out var absolutePath))
        {
            return InspectionArtifactPreviewResult.FromMetadata(
                InspectionArtifactMetadata.UnsafePath(metadata.DisplayPath));
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(absolutePath, cancellationToken).ConfigureAwait(false);
            return DecodeBmpPreview(metadata.DisplayPath, bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return InspectionArtifactPreviewResult.Unavailable(
                metadata.DisplayPath,
                $"Artifact preview unavailable: {ex.Message}");
        }
    }

    private string GetSafeDirectory(string dateSegment)
    {
        var directory = Path.GetFullPath(Path.Combine(_rootDirectory, dateSegment));
        EnsureInsideRoot(directory);
        return directory;
    }

    private string GetSafeFilePath(string directory, string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        EnsureInsideRoot(path);
        return path;
    }

    private bool TryResolveArtifactPath(string artifactPath, out string absolutePath)
    {
        absolutePath = string.Empty;
        if (Path.IsPathRooted(artifactPath))
        {
            return false;
        }

        var segments = artifactPath.Split(
            new[] { '/', '\\' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        var startIndex = string.Equals(segments[0], RelativeRoot, StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;
        if (startIndex >= segments.Length)
        {
            return false;
        }

        var resolvedPath = _rootDirectory;
        for (var index = startIndex; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (segment is "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }

            resolvedPath = Path.Combine(resolvedPath, segment);
        }

        absolutePath = Path.GetFullPath(resolvedPath);
        return IsInsideRoot(absolutePath);
    }

    private void EnsureInsideRoot(string path)
    {
        if (!IsInsideRoot(path))
        {
            throw new InvalidOperationException("Artifact path resolved outside the configured root.");
        }
    }

    private bool IsInsideRoot(string path)
    {
        return path.StartsWith(_rootDirectoryWithSeparator, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, _rootDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static string ToRelativePath(string dateSegment, string fileName)
    {
        return string.Join('/', RelativeRoot, dateSegment, fileName);
    }

    private static byte[] RenderOverlay(InspectionArtifactWriteRequest request)
    {
        var image = request.SourceImage;
        var pixels = new Rgb[image.Width * image.Height];
        for (var y = 0; y < image.Height; y++)
        {
            var rowOffset = y * image.Stride;
            for (var x = 0; x < image.Width; x++)
            {
                var gray = image.Gray8Pixels[rowOffset + x];
                pixels[(y * image.Width) + x] = new Rgb(gray, gray, gray);
            }
        }

        DrawBanner(pixels, image.Width, image.Height, request.Judgment == Judgment.Pass ? Green : Red);

        foreach (var roi in request.Rois)
        {
            DrawRectangle(pixels, image.Width, image.Height, roi.X, roi.Y, roi.Width, roi.Height, Cyan);
        }

        foreach (var defect in request.Defects)
        {
            DrawDefect(pixels, image.Width, image.Height, defect);
        }

        return EncodeBmp(pixels, image.Width, image.Height);
    }

    private static byte[] RenderHeightMap(InspectionArtifactHeightMap heightMap)
    {
        var pixels = new Rgb[heightMap.Width * heightMap.Height];
        var finiteValues = heightMap.Values
            .Where(float.IsFinite)
            .ToArray();
        var min = finiteValues.Length == 0 ? 0.0f : finiteValues.Min();
        var max = finiteValues.Length == 0 ? 1.0f : finiteValues.Max();
        var range = Math.Max(0.0001f, max - min);

        for (var index = 0; index < heightMap.Width * heightMap.Height; index++)
        {
            var value = heightMap.Values[index];
            var normalized = float.IsFinite(value)
                ? Math.Clamp((value - min) / range, 0.0f, 1.0f)
                : 0.0f;
            pixels[index] = ToHeatColor(normalized);
        }

        return EncodeBmp(pixels, heightMap.Width, heightMap.Height);
    }

    private static void DrawBanner(Rgb[] pixels, int width, int height, Rgb color)
    {
        var bannerHeight = Math.Min(6, height);
        for (var y = 0; y < bannerHeight; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = color;
            }
        }
    }

    private static void DrawDefect(
        Rgb[] pixels,
        int imageWidth,
        int imageHeight,
        InspectionDefectRecord defect)
    {
        var width = Math.Max(3, defect.Width);
        var height = Math.Max(3, defect.Height);
        var x = defect.X - ((width - defect.Width) / 2);
        var y = defect.Y - ((height - defect.Height) / 2);
        DrawRectangle(pixels, imageWidth, imageHeight, x, y, width, height, defect.Type == "LeadBent" ? Yellow : Red);
    }

    private static void DrawRectangle(
        Rgb[] pixels,
        int imageWidth,
        int imageHeight,
        int x,
        int y,
        int width,
        int height,
        Rgb color)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || width <= 0 || height <= 0)
        {
            return;
        }

        var left = Math.Clamp(x, 0, imageWidth - 1);
        var top = Math.Clamp(y, 0, imageHeight - 1);
        var right = Math.Clamp(x + width - 1, 0, imageWidth - 1);
        var bottom = Math.Clamp(y + height - 1, 0, imageHeight - 1);
        if (right < left || bottom < top)
        {
            return;
        }

        for (var px = left; px <= right; px++)
        {
            pixels[(top * imageWidth) + px] = color;
            pixels[(bottom * imageWidth) + px] = color;
        }

        for (var py = top; py <= bottom; py++)
        {
            pixels[(py * imageWidth) + left] = color;
            pixels[(py * imageWidth) + right] = color;
        }
    }

    private static Rgb ToHeatColor(float normalized)
    {
        if (normalized < 0.5f)
        {
            var blend = normalized / 0.5f;
            return new Rgb(0, (byte)(255 * blend), (byte)(255 * (1.0f - blend)));
        }

        var highBlend = (normalized - 0.5f) / 0.5f;
        return new Rgb((byte)(255 * highBlend), (byte)(255 * (1.0f - highBlend)), 0);
    }

    private static byte[] EncodeBmp(Rgb[] pixels, int width, int height)
    {
        var rowStride = ((width * 3) + 3) & ~3;
        var pixelDataSize = rowStride * height;
        var fileSize = 54 + pixelDataSize;
        using var stream = new MemoryStream(fileSize);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(pixelDataSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        var padding = new byte[rowStride - (width * 3)];
        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = pixels[(y * width) + x];
                writer.Write(pixel.B);
                writer.Write(pixel.G);
                writer.Write(pixel.R);
            }

            writer.Write(padding);
        }

        return stream.ToArray();
    }

    private static InspectionArtifactPreviewResult DecodeBmpPreview(string displayPath, byte[] bytes)
    {
        if (bytes.Length < 54 || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
        {
            return InspectionArtifactPreviewResult.Unavailable(displayPath, "Artifact preview rejected: not a BMP file.");
        }

        var pixelOffset = BitConverter.ToInt32(bytes, 10);
        var dibHeaderSize = BitConverter.ToInt32(bytes, 14);
        var width = BitConverter.ToInt32(bytes, 18);
        var signedHeight = BitConverter.ToInt32(bytes, 22);
        var planes = BitConverter.ToInt16(bytes, 26);
        var bitsPerPixel = BitConverter.ToInt16(bytes, 28);
        var compression = BitConverter.ToInt32(bytes, 30);

        if (dibHeaderSize < 40 ||
            width <= 0 ||
            signedHeight == 0 ||
            signedHeight == int.MinValue ||
            planes != 1 ||
            bitsPerPixel != 24 ||
            compression != 0 ||
            pixelOffset < 54)
        {
            return InspectionArtifactPreviewResult.Unavailable(displayPath, "Artifact preview rejected: unsupported BMP format.");
        }

        var height = Math.Abs(signedHeight);
        var sourceStride = (((long)width * 3) + 3) & ~3L;
        var expectedLength = pixelOffset + (sourceStride * height);
        if (expectedLength > bytes.Length || expectedLength > int.MaxValue)
        {
            return InspectionArtifactPreviewResult.Unavailable(displayPath, "Artifact preview rejected: BMP pixel data is incomplete.");
        }

        var targetStride = (long)width * 4;
        var targetLength = targetStride * height;
        if (targetStride > int.MaxValue || targetLength > int.MaxValue)
        {
            return InspectionArtifactPreviewResult.Unavailable(displayPath, "Artifact preview rejected: BMP image is too large.");
        }

        var pixels = new byte[(int)targetLength];
        var bottomUp = signedHeight > 0;
        for (var y = 0; y < height; y++)
        {
            var sourceY = bottomUp ? height - 1 - y : y;
            var sourceRow = pixelOffset + (sourceY * sourceStride);
            var targetRow = y * targetStride;
            for (var x = 0; x < width; x++)
            {
                var sourceIndex = (int)(sourceRow + (x * 3));
                var targetIndex = (int)(targetRow + (x * 4));
                pixels[targetIndex] = bytes[sourceIndex];
                pixels[targetIndex + 1] = bytes[sourceIndex + 1];
                pixels[targetIndex + 2] = bytes[sourceIndex + 2];
                pixels[targetIndex + 3] = 255;
            }
        }

        return InspectionArtifactPreviewResult.Available(
            displayPath,
            width,
            height,
            (int)targetStride,
            InspectionArtifactPreviewPixelFormat.Bgra32,
            pixels);
    }

    private static void ValidateImage(InspectionArtifactImageFrame image)
    {
        if (image.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(image), image.Width, "Artifact image width must be greater than zero.");
        }

        if (image.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(image), image.Height, "Artifact image height must be greater than zero.");
        }

        if (image.Stride < image.Width)
        {
            throw new ArgumentOutOfRangeException(nameof(image), image.Stride, "Artifact image stride must be at least image width.");
        }

        if (image.Gray8Pixels.Length < image.Stride * image.Height)
        {
            throw new ArgumentException("Artifact image buffer is smaller than stride multiplied by height.", nameof(image));
        }
    }

    private static void ValidateHeightMap(InspectionArtifactHeightMap heightMap)
    {
        if (heightMap.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heightMap), heightMap.Width, "Height-map artifact width must be greater than zero.");
        }

        if (heightMap.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heightMap), heightMap.Height, "Height-map artifact height must be greater than zero.");
        }

        if (heightMap.Values.Length < heightMap.Width * heightMap.Height)
        {
            throw new ArgumentException("Height-map artifact buffer is smaller than width multiplied by height.", nameof(heightMap));
        }
    }

    private readonly record struct Rgb(byte R, byte G, byte B);
}
