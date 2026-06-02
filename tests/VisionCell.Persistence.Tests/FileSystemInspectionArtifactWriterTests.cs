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

        parentTraversal.Status.Should().Be(InspectionArtifactMetadataStatus.UnsafePath);
        rooted.Status.Should().Be(InspectionArtifactMetadataStatus.UnsafePath);
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
