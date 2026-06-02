using FluentAssertions;
using VisionCell.Application.Inspection;
using VisionCell.Persistence.Inspection;
using VisionCell.Vision.Inspection;
using Xunit;

namespace VisionCell_Persistence_Tests;

public sealed class FileSystemInspectionArtifactWriterTests
{
    [Fact]
    public async Task WriteAsync_Should_Create_Overlay_And_HeightMap_Bmp_Files_With_Relative_Paths()
    {
        using var directory = TemporaryDirectory.Create();
        var artifactRoot = Path.Combine(directory.Path, "inspection-artifacts");
        var writer = new FileSystemInspectionArtifactWriter(artifactRoot);
        var resultId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var request = CreateRequest(resultId);

        var result = await writer.WriteAsync(request, CancellationToken.None);

        result.OverlayImagePath.Should().Be("inspection-artifacts/20260601/aaaaaaaabbbbccccddddeeeeeeeeeeee.overlay.bmp");
        result.HeightMapPath.Should().Be("inspection-artifacts/20260601/aaaaaaaabbbbccccddddeeeeeeeeeeee.height.bmp");
        Path.IsPathRooted(result.OverlayImagePath).Should().BeFalse();
        Path.IsPathRooted(result.HeightMapPath).Should().BeFalse();
        result.OverlayImagePath.Should().NotContain("..");
        result.HeightMapPath.Should().NotContain("..");

        var overlayPath = Path.Combine(artifactRoot, "20260601", "aaaaaaaabbbbccccddddeeeeeeeeeeee.overlay.bmp");
        var heightMapPath = Path.Combine(artifactRoot, "20260601", "aaaaaaaabbbbccccddddeeeeeeeeeeee.height.bmp");
        File.Exists(overlayPath).Should().BeTrue();
        File.Exists(heightMapPath).Should().BeTrue();

        var overlay = await File.ReadAllBytesAsync(overlayPath);
        var heightMap = await File.ReadAllBytesAsync(heightMapPath);
        ReadSignature(overlay).Should().Be("BM");
        ReadSignature(heightMap).Should().Be("BM");
        ReadWidth(overlay).Should().Be(10);
        ReadHeight(overlay).Should().Be(8);
        ReadWidth(heightMap).Should().Be(10);
        ReadHeight(heightMap).Should().Be(8);
        ReadPixel(overlay, 4, 4).Should().Be(((byte)230, (byte)48, (byte)48));
        ReadPixel(overlay, 2, 2).Should().Be(((byte)0, (byte)210, (byte)255));

        var overlayMetadata = await writer.ReadMetadataAsync(result.OverlayImagePath, CancellationToken.None);
        overlayMetadata.Status.Should().Be(InspectionArtifactMetadataStatus.Available);
        overlayMetadata.DisplayPath.Should().Be(result.OverlayImagePath);
        overlayMetadata.SizeBytes.Should().BeGreaterThan(0);
        overlayMetadata.LastModifiedAt.Should().NotBeNull();

        var overlayPreview = await writer.ReadPreviewAsync(result.OverlayImagePath, CancellationToken.None);
        overlayPreview.Status.Should().Be(InspectionArtifactMetadataStatus.Available);
        overlayPreview.HasImage.Should().BeTrue();
        overlayPreview.Width.Should().Be(10);
        overlayPreview.Height.Should().Be(8);
        overlayPreview.Stride.Should().Be(40);
        overlayPreview.PixelFormat.Should().Be(InspectionArtifactPreviewPixelFormat.Bgra32);
        overlayPreview.Pixels.Should().HaveCount(10 * 8 * 4);
    }

    [Fact]
    public async Task PrepareOpenAsync_Should_Return_Resolved_Path_For_Available_Bmp_Artifact()
    {
        using var directory = TemporaryDirectory.Create();
        var artifactRoot = Path.Combine(directory.Path, "inspection-artifacts");
        var writer = new FileSystemInspectionArtifactWriter(artifactRoot);
        var resultId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var written = await writer.WriteAsync(CreateRequest(resultId), CancellationToken.None);

        var overlay = await writer.PrepareOpenAsync(
            new InspectionArtifactOpenRequest(InspectionArtifactKind.Overlay, written.OverlayImagePath),
            CancellationToken.None);
        var heightMap = await writer.PrepareOpenAsync(
            new InspectionArtifactOpenRequest(InspectionArtifactKind.HeightMap, written.HeightMapPath),
            CancellationToken.None);

        overlay.Status.Should().Be(InspectionArtifactOpenStatus.Ready);
        overlay.CanOpen.Should().BeTrue();
        overlay.DisplayPath.Should().Be(written.OverlayImagePath);
        overlay.ResolvedPath.Should().NotBeNull();
        Path.IsPathRooted(overlay.ResolvedPath!).Should().BeTrue();
        File.Exists(overlay.ResolvedPath!).Should().BeTrue();
        overlay.ResolvedPath!.StartsWith(artifactRoot, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        heightMap.Status.Should().Be(InspectionArtifactOpenStatus.Ready);
        heightMap.CanOpen.Should().BeTrue();
        heightMap.ResolvedPath.Should().EndWith(".height.bmp");
    }

    [Fact]
    public async Task PrepareOpenAsync_Should_Reject_Missing_NotRecorded_Unsafe_And_Unsupported_Paths()
    {
        using var directory = TemporaryDirectory.Create();
        var artifactRoot = Path.Combine(directory.Path, "inspection-artifacts");
        var writer = new FileSystemInspectionArtifactWriter(artifactRoot);
        var unsupportedDirectory = Path.Combine(artifactRoot, "20260601");
        Directory.CreateDirectory(unsupportedDirectory);
        var unsupportedPath = Path.Combine(unsupportedDirectory, "artifact.txt");
        await File.WriteAllTextAsync(unsupportedPath, "not a supported inspection artifact");

        var missing = await writer.PrepareOpenAsync(
            new InspectionArtifactOpenRequest(InspectionArtifactKind.Overlay, "inspection-artifacts/20260601/missing.overlay.bmp"),
            CancellationToken.None);
        var notRecorded = await writer.PrepareOpenAsync(
            new InspectionArtifactOpenRequest(InspectionArtifactKind.Overlay, " "),
            CancellationToken.None);
        var unsafePath = await writer.PrepareOpenAsync(
            new InspectionArtifactOpenRequest(InspectionArtifactKind.HeightMap, "inspection-artifacts/../outside.height.bmp"),
            CancellationToken.None);
        var unsupported = await writer.PrepareOpenAsync(
            new InspectionArtifactOpenRequest(InspectionArtifactKind.Overlay, "inspection-artifacts/20260601/artifact.txt"),
            CancellationToken.None);

        missing.Status.Should().Be(InspectionArtifactOpenStatus.Missing);
        missing.CanOpen.Should().BeFalse();
        notRecorded.Status.Should().Be(InspectionArtifactOpenStatus.NotRecorded);
        unsafePath.Status.Should().Be(InspectionArtifactOpenStatus.UnsafePath);
        unsupported.Status.Should().Be(InspectionArtifactOpenStatus.UnsupportedType);
        unsupported.CanOpen.Should().BeFalse();
    }

    [Fact]
    public void Constructor_Should_Reject_Blank_Root_Path()
    {
        var act = () => new FileSystemInspectionArtifactWriter(" ");

        act.Should().Throw<ArgumentException>().WithMessage("*Artifact root path*");
    }

    [Fact]
    public async Task ReadMetadataAsync_Should_Report_Missing_And_NotRecorded_Artifacts()
    {
        using var directory = TemporaryDirectory.Create();
        var artifactRoot = Path.Combine(directory.Path, "inspection-artifacts");
        var writer = new FileSystemInspectionArtifactWriter(artifactRoot);

        var missing = await writer.ReadMetadataAsync(
            "inspection-artifacts/20260601/missing.overlay.bmp",
            CancellationToken.None);
        var notRecorded = await writer.ReadMetadataAsync(" ", CancellationToken.None);

        missing.Status.Should().Be(InspectionArtifactMetadataStatus.Missing);
        missing.DisplayPath.Should().Be("inspection-artifacts/20260601/missing.overlay.bmp");
        notRecorded.Status.Should().Be(InspectionArtifactMetadataStatus.NotRecorded);
        notRecorded.DisplayPath.Should().Be("-");
    }

    [Fact]
    public async Task ReadMetadataAsync_Should_Reject_Unsafe_Artifact_Paths()
    {
        using var directory = TemporaryDirectory.Create();
        var artifactRoot = Path.Combine(directory.Path, "inspection-artifacts");
        var writer = new FileSystemInspectionArtifactWriter(artifactRoot);

        var parentTraversal = await writer.ReadMetadataAsync(
            "inspection-artifacts/../outside.bmp",
            CancellationToken.None);
        var rooted = await writer.ReadMetadataAsync(
            Path.Combine(directory.Path, "outside.bmp"),
            CancellationToken.None);
        var unsafePreview = await writer.ReadPreviewAsync(
            "inspection-artifacts/../outside.bmp",
            CancellationToken.None);

        parentTraversal.Status.Should().Be(InspectionArtifactMetadataStatus.UnsafePath);
        rooted.Status.Should().Be(InspectionArtifactMetadataStatus.UnsafePath);
        unsafePreview.Status.Should().Be(InspectionArtifactMetadataStatus.UnsafePath);
        unsafePreview.HasImage.Should().BeFalse();
    }

    private static InspectionArtifactWriteRequest CreateRequest(Guid resultId)
    {
        var imagePixels = Enumerable.Range(0, 80)
            .Select(index => (byte)(40 + (index % 160)))
            .ToArray();
        var heightValues = Enumerable.Range(0, 80)
            .Select(index => 0.8f + (index / 80.0f))
            .ToArray();

        return new InspectionArtifactWriteRequest(
            resultId,
            "corr-001",
            "LOT-20260601120000",
            "RCP-ARTIFACT",
            "1.0.0",
            Judgment.Fail,
            new InspectionArtifactImageFrame(10, 8, 10, imagePixels),
            new InspectionArtifactHeightMap(10, 8, heightValues, "mm"),
            new[]
            {
                new InspectionArtifactRoi("ROI-01", "Main", 2, 2, 5, 4)
            },
            new[]
            {
                new InspectionDefectRecord("Missing", 0.9, "ROI-01", 4, 4, 2, 2, "Missing area.")
            },
            DateTimeOffset.Parse("2026-06-01T12:00:00Z"));
    }

    private static string ReadSignature(byte[] bytes)
    {
        return $"{(char)bytes[0]}{(char)bytes[1]}";
    }

    private static int ReadWidth(byte[] bytes)
    {
        return BitConverter.ToInt32(bytes, 18);
    }

    private static int ReadHeight(byte[] bytes)
    {
        return BitConverter.ToInt32(bytes, 22);
    }

    private static (byte R, byte G, byte B) ReadPixel(byte[] bytes, int x, int y)
    {
        var pixelOffset = BitConverter.ToInt32(bytes, 10);
        var width = ReadWidth(bytes);
        var height = ReadHeight(bytes);
        var rowStride = ((width * 3) + 3) & ~3;
        var bmpY = height - 1 - y;
        var index = pixelOffset + (bmpY * rowStride) + (x * 3);
        return (bytes[index + 2], bytes[index + 1], bytes[index]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "VisionCellArtifactTests",
                Guid.NewGuid().ToString("N"));
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
