using FluentAssertions;
using VisionCell.Application.Inspection;
using Xunit;

namespace VisionCell.Application.Tests;

public sealed class InspectionReinspectSourceImageReadinessUseCaseTests
{
    [Fact]
    public async Task ResolveAsync_Should_Report_FrameArchiveUnavailable_For_CameraFrame_Uri()
    {
        var useCase = new InspectionReinspectSourceImageReadinessUseCase();
        var preparation = CreatePreparation("camera-frame://VirtualCamera/correlation-001");

        var result = await useCase.ResolveAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectSourceImageReadinessStatus.FrameArchiveUnavailable);
        result.CanReplaySourceImage.Should().BeFalse();
        result.ReplayInputKind.Should().Be("Transient camera frame URI");
        result.Message.Should().Contain("Raw source pixels are not archived");
    }

    [Fact]
    public async Task ResolveAsync_Should_Report_NotRecorded_When_Source_Image_Is_Blank()
    {
        var useCase = new InspectionReinspectSourceImageReadinessUseCase();
        var preparation = CreatePreparation(" ");

        var result = await useCase.ResolveAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectSourceImageReadinessStatus.NotRecorded);
        result.SourceImagePath.Should().Be("-");
        result.StatusLabel.Should().Be("Source image not recorded");
    }

    [Fact]
    public async Task ResolveAsync_Should_Report_UnsupportedUri_For_External_Uri()
    {
        var useCase = new InspectionReinspectSourceImageReadinessUseCase();
        var preparation = CreatePreparation("https://example.invalid/source.bmp");

        var result = await useCase.ResolveAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectSourceImageReadinessStatus.UnsupportedUri);
        result.ReplayInputKind.Should().Be("https URI");
        result.Message.Should().Contain("not supported");
    }

    [Fact]
    public async Task ResolveAsync_Should_Reject_Parent_Traversal_Path()
    {
        var useCase = new InspectionReinspectSourceImageReadinessUseCase();
        var preparation = CreatePreparation(@"inspection-artifacts\..\source.bmp");

        var result = await useCase.ResolveAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectSourceImageReadinessStatus.UnsafePath);
        result.StatusLabel.Should().Be("Source path rejected");
    }

    [Fact]
    public async Task ResolveAsync_Should_Report_SourceArtifactReaderMissing_For_Relative_Candidate()
    {
        var useCase = new InspectionReinspectSourceImageReadinessUseCase();
        var preparation = CreatePreparation(@"inspection-sources\20260603\source.bmp");

        var result = await useCase.ResolveAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectSourceImageReadinessStatus.SourceArtifactReaderMissing);
        result.StatusLabel.Should().Be("Source artifact reader missing");
        result.Message.Should().Contain("no source-image artifact reader");
    }

    [Fact]
    public async Task ResolveAsync_Should_Report_SourceArtifactArchived_When_Reader_Finds_Source_Bmp()
    {
        var useCase = new InspectionReinspectSourceImageReadinessUseCase(new FakeInspectionArtifactReader(
            InspectionArtifactMetadata.Available(
                "inspection-artifacts/20260603/result.source.bmp",
                4096,
                new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero))));
        var preparation = CreatePreparation("inspection-artifacts/20260603/result.source.bmp");

        var result = await useCase.ResolveAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectSourceImageReadinessStatus.SourceArtifactArchived);
        result.ReplayInputKind.Should().Be("Archived source BMP");
        result.Message.Should().Contain("future replay input");
        result.CanReplaySourceImage.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_Should_Report_SourceArtifactMissing_When_Reader_Cannot_Find_Source_Bmp()
    {
        var useCase = new InspectionReinspectSourceImageReadinessUseCase(new FakeInspectionArtifactReader(
            InspectionArtifactMetadata.Missing("inspection-artifacts/20260603/result.source.bmp")));
        var preparation = CreatePreparation("inspection-artifacts/20260603/result.source.bmp");

        var result = await useCase.ResolveAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectSourceImageReadinessStatus.SourceArtifactMissing);
        result.StatusLabel.Should().Be("Source artifact missing");
    }

    [Fact]
    public async Task ResolveAsync_Should_Reject_Relative_Non_Source_Bmp_When_Reader_Is_Configured()
    {
        var useCase = new InspectionReinspectSourceImageReadinessUseCase(new FakeInspectionArtifactReader(
            InspectionArtifactMetadata.Available(
                "inspection-artifacts/20260603/result.overlay.bmp",
                4096,
                new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero))));
        var preparation = CreatePreparation("inspection-artifacts/20260603/result.overlay.bmp");

        var result = await useCase.ResolveAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectSourceImageReadinessStatus.UnsupportedSourceArtifactType);
        result.Message.Should().Contain(".source.bmp");
    }

    private static InspectionReinspectPreparation CreatePreparation(string sourceImagePath)
    {
        return new InspectionReinspectPreparation(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "LOT-20260603120000",
            "RCP-OFFLINE",
            "1.0.0",
            "Pass",
            TimeSpan.FromMilliseconds(123),
            0,
            "corr-001",
            sourceImagePath,
            "inspection-artifacts/result.overlay.bmp",
            "inspection-artifacts/result.height.bmp",
            new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero),
            true,
            "Ready for metadata comparison.");
    }

    private sealed class FakeInspectionArtifactReader : IInspectionArtifactReader
    {
        private readonly InspectionArtifactMetadata _metadata;

        public FakeInspectionArtifactReader(InspectionArtifactMetadata metadata)
        {
            _metadata = metadata;
        }

        public Task<InspectionArtifactMetadata> ReadMetadataAsync(
            string? artifactPath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_metadata);
        }

        public Task<InspectionArtifactPreviewResult> ReadPreviewAsync(
            string? artifactPath,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Preview is not used by source readiness tests.");
        }

        public Task<InspectionArtifactOpenResult> PrepareOpenAsync(
            InspectionArtifactOpenRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Open preparation is not used by source readiness tests.");
        }
    }
}
