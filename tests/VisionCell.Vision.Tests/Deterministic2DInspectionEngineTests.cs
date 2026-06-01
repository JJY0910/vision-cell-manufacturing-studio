using FluentAssertions;
using VisionCell.Core.Primitives;
using VisionCell.Vision.Inspection;
using Xunit;

namespace VisionCell_Vision_Tests;

public sealed class Deterministic2DInspectionEngineTests
{
    [Fact]
    public async Task InspectAsync_Should_Return_Pass_For_Centered_Foreground()
    {
        var engine = new Deterministic2DInspectionEngine();
        var frame = CreateFrame(pixels => FillRect(pixels, 120, x: 38, y: 32, width: 24, height: 16, value: 190));

        var result = await engine.InspectAsync(CreateRequest(frame), CancellationToken.None);

        result.Judgment.Should().Be(Judgment.Pass);
        result.IsPass.Should().BeTrue();
        result.Defects.Should().BeEmpty();
        result.Message.Should().Contain("Pass");
    }

    [Fact]
    public async Task InspectAsync_Should_Return_Fail_When_Roi_Is_Missing()
    {
        var engine = new Deterministic2DInspectionEngine();
        var frame = CreateFrame(pixels => FillRect(pixels, 120, x: 20, y: 20, width: 60, height: 40, value: 0));

        var result = await engine.InspectAsync(CreateRequest(frame), CancellationToken.None);

        result.Judgment.Should().Be(Judgment.Fail);
        result.Defects.Should().ContainSingle(defect => defect.Type == "Missing");
    }

    [Fact]
    public async Task InspectAsync_Should_Return_Fail_When_Dark_Line_Scratch_Is_Present()
    {
        var engine = new Deterministic2DInspectionEngine();
        var frame = CreateFrame(pixels =>
        {
            FillRect(pixels, 120, x: 38, y: 32, width: 24, height: 16, value: 190);
            FillRect(pixels, 120, x: 20, y: 42, width: 60, height: 1, value: 0);
        });

        var result = await engine.InspectAsync(CreateRequest(frame), CancellationToken.None);

        result.Judgment.Should().Be(Judgment.Fail);
        result.Defects.Should().Contain(defect => defect.Type == "Scratch");
    }

    [Fact]
    public async Task InspectAsync_Should_Return_Fail_When_Foreground_Is_Offset()
    {
        var engine = new Deterministic2DInspectionEngine();
        var frame = CreateFrame(pixels => FillRect(pixels, 120, x: 26, y: 26, width: 20, height: 14, value: 190));

        var result = await engine.InspectAsync(CreateRequest(frame), CancellationToken.None);

        result.Judgment.Should().Be(Judgment.Fail);
        result.Defects.Should().Contain(defect => defect.Type == "Offset");
    }

    [Fact]
    public async Task InspectAsync_Should_Return_Invalid_When_Roi_Is_Outside_Frame()
    {
        var engine = new Deterministic2DInspectionEngine();
        var frame = CreateFrame(_ => { });
        var request = CreateRequest(frame, new VisionRoi("ROI-BAD", "Outside", 90, 70, 40, 30));

        var result = await engine.InspectAsync(request, CancellationToken.None);

        result.Judgment.Should().Be(Judgment.Invalid);
        result.Defects.Should().ContainSingle(defect => defect.Type == "InvalidRoi");
    }

    private static VisionInspectionRequest CreateRequest(
        VisionImageFrame frame,
        VisionRoi? roi = null)
    {
        return new VisionInspectionRequest(
            CorrelationId.New(),
            "RCP-VISION",
            "1.0.0",
            frame,
            new[] { roi ?? new VisionRoi("ROI-01", "Main ROI", 20, 20, 60, 40) },
            new VisionInspectionParameters(
                MissingAreaThreshold: 0.75,
                OffsetTolerancePx: 6,
                ScratchThreshold: 0.65),
            TimeSpan.FromSeconds(1),
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    }

    private static VisionImageFrame CreateFrame(Action<byte[]> mutate)
    {
        const int width = 120;
        const int height = 90;
        var pixels = Enumerable.Repeat((byte)80, width * height).ToArray();
        mutate(pixels);
        return new VisionImageFrame(
            width,
            height,
            width,
            VisionPixelFormat.Gray8,
            pixels,
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            new Dictionary<string, string> { ["FrameKind"] = "UnitTestGray8" });
    }

    private static void FillRect(
        byte[] pixels,
        int stride,
        int x,
        int y,
        int width,
        int height,
        byte value)
    {
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                pixels[(row * stride) + column] = value;
            }
        }
    }
}
