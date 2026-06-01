using FluentAssertions;
using VisionCell.Core.Primitives;
using VisionCell.Motion.Axes;
using Xunit;

namespace VisionCell_Motion_Tests;

public sealed class AxisModelTests
{
    [Fact]
    public void Default_Axes_Should_Provide_Four_Semiconductor_Stage_Axes()
    {
        var axes = AxisDefaults.CreatePowerOffAxes();

        axes.Select(axis => axis.AxisId)
            .Should()
            .BeEquivalentTo(new[] { AxisId.X, AxisId.Y, AxisId.Z, AxisId.Theta });
        axes.Should().OnlyContain(axis => !axis.ServoOn && !axis.IsHomed && !axis.IsMoving);
    }

    [Theory]
    [InlineData(AxisId.X, -200.0, true)]
    [InlineData(AxisId.X, 201.0, false)]
    [InlineData(AxisId.Z, -0.1, false)]
    [InlineData(AxisId.Theta, 180.0, true)]
    public void SoftLimit_Should_Validate_Target_Range(AxisId axisId, double value, bool expected)
    {
        var axis = AxisDefaults.CreatePowerOffAxes().Single(axis => axis.AxisId == axisId);

        axis.SoftLimit.Contains(value).Should().Be(expected);
    }
}
