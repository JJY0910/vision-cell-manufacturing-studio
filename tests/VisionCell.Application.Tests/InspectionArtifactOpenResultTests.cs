using FluentAssertions;
using VisionCell.Application.Inspection;
using Xunit;

namespace VisionCell_Application_Tests;

public sealed class InspectionArtifactOpenResultTests
{
    [Fact]
    public void Ready_Should_Expose_Openable_State()
    {
        var resolvedPath = Path.Combine(
            Path.GetTempPath(),
            "VisionCellArtifactOpenTests",
            "result.overlay.bmp");

        var result = InspectionArtifactOpenResult.Ready(
            InspectionArtifactKind.Overlay,
            "inspection-artifacts/20260601/result.overlay.bmp",
            resolvedPath);

        result.CanOpen.Should().BeTrue();
        result.Status.Should().Be(InspectionArtifactOpenStatus.Ready);
        result.DisplayPath.Should().Be("inspection-artifacts/20260601/result.overlay.bmp");
        result.ResolvedPath.Should().EndWith(@"result.overlay.bmp");
        result.Message.Should().Contain("Overlay");
    }

    [Fact]
    public void FromMetadata_Should_Map_Rejected_Metadata_To_Not_Openable_Result()
    {
        var unsafeMetadata = InspectionArtifactMetadata.UnsafePath("inspection-artifacts/../outside.bmp");

        var result = InspectionArtifactOpenResult.FromMetadata(
            InspectionArtifactKind.HeightMap,
            unsafeMetadata);

        result.CanOpen.Should().BeFalse();
        result.Status.Should().Be(InspectionArtifactOpenStatus.UnsafePath);
        result.ResolvedPath.Should().BeNull();
        result.Message.Should().Contain("Height Map");
    }

    [Fact]
    public void UnsupportedType_Should_Not_Be_Openable()
    {
        var result = InspectionArtifactOpenResult.UnsupportedType(
            InspectionArtifactKind.Overlay,
            "inspection-artifacts/20260601/result.txt");

        result.CanOpen.Should().BeFalse();
        result.Status.Should().Be(InspectionArtifactOpenStatus.UnsupportedType);
        result.ResolvedPath.Should().BeNull();
    }
}
