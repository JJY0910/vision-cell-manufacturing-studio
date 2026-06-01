using FluentAssertions;
using VisionCell.Core.Primitives;
using VisionCell.Motion.Teaching;
using Xunit;

namespace VisionCell_Motion_Tests;

public sealed class TeachingPointTests
{
    [Fact]
    public void Create_Should_Preserve_Position_Role_Tolerance_And_Timestamps()
    {
        var id = Guid.Parse("1f232778-7395-4659-b4a0-fbe09db45c57");
        var timestamp = new DateTimeOffset(2026, 6, 1, 6, 30, 0, TimeSpan.Zero);
        var position = new Position4D(12.5, -5.0, 30.0, 45.0);
        var tolerance = new PositionTolerance(0.01, 0.02, 0.03, 0.04);

        var result = TeachingPointFactory.Create(
            " Camera Align ",
            TeachingRole.Camera,
            position,
            tolerance,
            " first pass ",
            id,
            timestamp);

        result.IsSuccess.Should().BeTrue();
        result.Issues.Should().BeEmpty();
        result.Point.Should().NotBeNull();
        result.Point!.Id.Should().Be(id);
        result.Point.Name.Should().Be("Camera Align");
        result.Point.Role.Should().Be(TeachingRole.Camera);
        result.Point.Position.Should().Be(position);
        result.Point.Tolerance.Should().Be(tolerance);
        result.Point.Memo.Should().Be("first pass");
        result.Point.CreatedAt.Should().Be(timestamp);
        result.Point.UpdatedAt.Should().Be(timestamp);
    }

    [Fact]
    public void Create_Should_Reject_Blank_Name()
    {
        var result = TeachingPointFactory.Create(
            " ",
            TeachingRole.Load,
            new Position4D(0.0, 0.0, 0.0, 0.0),
            PositionTolerance.Default);

        result.IsSuccess.Should().BeFalse();
        result.Point.Should().BeNull();
        result.Issues.Should().Contain(issue => issue.Code == "TeachingPoint.NameRequired");
    }

    [Fact]
    public void Create_Should_Reject_Position_Outside_Soft_Limit()
    {
        var result = TeachingPointFactory.Create(
            "Z Above Limit",
            TeachingRole.Safe,
            new Position4D(0.0, 0.0, 101.0, 0.0),
            PositionTolerance.Default);

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(issue =>
            issue.Code == "TeachingPoint.PositionOutOfSoftLimit" &&
            issue.Message.Contains("Z position", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Create_Should_Reject_Invalid_Tolerance(double toleranceValue)
    {
        var result = TeachingPointFactory.Create(
            "Invalid Tolerance",
            TeachingRole.Inspection,
            new Position4D(0.0, 0.0, 0.0, 0.0),
            new PositionTolerance(toleranceValue, 0.01, 0.01, 0.01));

        result.IsSuccess.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code == "TeachingPoint.ToleranceInvalid");
    }
}
