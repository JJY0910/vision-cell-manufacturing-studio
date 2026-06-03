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
}
