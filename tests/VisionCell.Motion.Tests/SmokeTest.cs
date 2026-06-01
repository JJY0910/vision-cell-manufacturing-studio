using FluentAssertions;
using VisionCell.Core.Primitives;
using VisionCell.Motion.Axes;
using VisionCell.Motion.Commands;
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

    [Fact]
    public void MotionProfilePreset_Defaults_Should_Expose_Operator_Move_Profiles()
    {
        MotionProfilePreset.Defaults.Select(preset => preset.Name)
            .Should()
            .Equal("Fine", "Standard", "Fast");

        MotionProfilePreset.Defaults.Should().OnlyContain(preset =>
            preset.Velocity > 0.0 &&
            preset.Acceleration > 0.0 &&
            preset.Deceleration > 0.0 &&
            preset.Jerk > 0.0 &&
            preset.ArrivalTolerance > 0.0);
    }

    [Fact]
    public void AbsoluteMoveTarget_ToParameters_Should_Preserve_Profile_Preset()
    {
        var target = new AbsoluteMoveTarget(
            1.0,
            2.0,
            3.0,
            4.0,
            MotionProfilePreset.Fine.Velocity,
            MotionProfilePreset.Fine.Acceleration,
            MotionProfilePreset.Fine.Deceleration,
            MotionProfilePreset.Fine.Jerk,
            MotionProfilePreset.Fine.ArrivalTolerance,
            MotionProfilePreset.Fine.Name);

        var parameters = target.ToParameters();

        parameters.Should().Contain("ProfilePreset", "Fine");
        parameters.Should().Contain("Velocity", "10");
        parameters.Should().Contain("ArrivalTolerance", "0.005");
    }
}
