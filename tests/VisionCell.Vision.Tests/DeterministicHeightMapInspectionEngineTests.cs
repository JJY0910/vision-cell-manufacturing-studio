using FluentAssertions;
using VisionCell.Core.Primitives;
using VisionCell.Vision.Inspection;
using Xunit;

namespace VisionCell_Vision_Tests;

public sealed class DeterministicHeightMapInspectionEngineTests
{
    [Fact]
    public async Task InspectAsync_Should_Return_Pass_For_Stable_Height_Map()
    {
        var engine = new DeterministicHeightMapInspectionEngine();
        var map = CreateMap(_ => { });

        var result = await engine.InspectAsync(CreateRequest(map), CancellationToken.None);

        result.Judgment.Should().Be(Judgment.Pass);
        result.Defects.Should().BeEmpty();
        result.Message.Should().Contain("Pass");
    }

    [Fact]
    public async Task InspectAsync_Should_Return_Fail_When_Lift_Is_Present()
    {
        var engine = new DeterministicHeightMapInspectionEngine();
        var map = CreateMap(values => values[(30 * 80) + 30] = 1.35f);

        var result = await engine.InspectAsync(CreateRequest(map), CancellationToken.None);

        result.Judgment.Should().Be(Judgment.Fail);
        result.Defects.Should().Contain(defect => defect.Type == "Lift");
    }

    [Fact]
    public async Task InspectAsync_Should_Return_Fail_When_Dent_Is_Present()
    {
        var engine = new DeterministicHeightMapInspectionEngine();
        var map = CreateMap(values => values[(30 * 80) + 30] = 0.65f);

        var result = await engine.InspectAsync(CreateRequest(map), CancellationToken.None);

        result.Judgment.Should().Be(Judgment.Fail);
        result.Defects.Should().Contain(defect => defect.Type == "Dent");
    }

    [Fact]
    public async Task InspectAsync_Should_Return_Fail_When_Local_Gradient_Indicates_LeadBent()
    {
        var engine = new DeterministicHeightMapInspectionEngine();
        var map = CreateMap(values =>
        {
            for (var y = 20; y < 50; y++)
            {
                for (var x = 40; x < 60; x++)
                {
                    values[(y * 80) + x] = 1.45f;
                }
            }
        });

        var result = await engine.InspectAsync(
            CreateRequest(
                map,
                parameters: new HeightMapInspectionParameters(
                    ExpectedHeight: 1.0,
                    HeightToleranceLow: 1.0,
                    HeightToleranceHigh: 1.0,
                    LeadBentGradientTolerance: 0.2)),
            CancellationToken.None);

        result.Judgment.Should().Be(Judgment.Fail);
        result.Defects.Should().ContainSingle(defect => defect.Type == "LeadBent");
    }

    [Fact]
    public async Task InspectAsync_Should_Return_Invalid_When_Roi_Is_Outside_Height_Map()
    {
        var engine = new DeterministicHeightMapInspectionEngine();
        var map = CreateMap(_ => { });

        var result = await engine.InspectAsync(
            CreateRequest(map, new VisionRoi("ROI-BAD", "Outside", 70, 50, 20, 20)),
            CancellationToken.None);

        result.Judgment.Should().Be(Judgment.Invalid);
        result.Defects.Should().ContainSingle(defect => defect.Type == "InvalidRoi");
    }

    private static HeightMapInspectionRequest CreateRequest(
        VisionHeightMap map,
        VisionRoi? roi = null,
        HeightMapInspectionParameters? parameters = null)
    {
        return new HeightMapInspectionRequest(
            CorrelationId.New(),
            "RCP-HEIGHT",
            "1.0.0",
            map,
            new[] { roi ?? new VisionRoi("ROI-01", "Main ROI", 20, 15, 40, 30) },
            parameters ?? new HeightMapInspectionParameters(
                ExpectedHeight: 1.0,
                HeightToleranceLow: 0.15,
                HeightToleranceHigh: 0.15,
                LeadBentGradientTolerance: 1.0),
            TimeSpan.FromSeconds(1),
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private static VisionHeightMap CreateMap(Action<float[]> mutate)
    {
        const int width = 80;
        const int height = 60;
        var values = Enumerable.Repeat(1.0f, width * height).ToArray();
        mutate(values);
        return new VisionHeightMap(
            width,
            height,
            values,
            "mm",
            new Dictionary<string, string> { ["HeightMapKind"] = "UnitTest" });
    }
}
