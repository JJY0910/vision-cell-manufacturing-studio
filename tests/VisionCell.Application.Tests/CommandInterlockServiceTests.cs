using FluentAssertions;
using VisionCell.Application.Interlocks;
using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;
using Xunit;

namespace VisionCell_Application_Tests;

public sealed class CommandInterlockServiceTests
{
    private readonly CommandInterlockService _service = new();

    [Fact]
    public void Connect_Should_Be_Enabled_Only_When_Disconnected()
    {
        _service.Evaluate(CommandKind.Connect, ReadyManualContext() with { Connected = false }).IsEnabled.Should().BeTrue();
        _service.Evaluate(CommandKind.Connect, ReadyManualContext() with { Connected = true }).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Disconnect_Should_Be_Disabled_When_Sequence_Running()
    {
        _service.Evaluate(CommandKind.Disconnect, ReadyManualContext() with { SequenceRunning = true }).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void ServoOn_Should_Be_Disabled_When_EmergencyStop_Active()
    {
        _service.Evaluate(CommandKind.ServoOn, ReadyManualContext() with { EmergencyStopActive = true, SafetyOk = false }).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void ServoOn_Should_Be_Disabled_When_Door_Open()
    {
        _service.Evaluate(CommandKind.ServoOn, ReadyManualContext() with { DoorClosed = false, SafetyOk = false }).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Home_Should_Be_Disabled_When_Servo_Off()
    {
        _service.Evaluate(CommandKind.Home, ReadyManualContext() with { ServoOn = false }).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Home_Should_Be_Disabled_In_Auto_Mode()
    {
        _service.Evaluate(CommandKind.Home, ReadyManualContext() with { ManualMode = false, AutoMode = true }).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Jog_Should_Not_Require_Homed_Axis_When_Setup_State_Is_Otherwise_Ready()
    {
        var availability = _service.Evaluate(CommandKind.Jog, ReadyManualContext() with { AxisHomed = false, AllRequiredAxesHomed = false });

        availability.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void MoveAbsolute_Should_Be_Disabled_When_Axis_Not_Homed()
    {
        _service.Evaluate(CommandKind.MoveAbsolute, ReadyManualContext() with { AxisHomed = false }).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void MoveAbsolute_Should_Be_Disabled_When_Target_Exceeds_Soft_Limit()
    {
        _service.Evaluate(CommandKind.MoveAbsolute, ReadyManualContext() with { WithinSoftLimit = false }).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Stop_Should_Be_Enabled_When_Axis_Busy_Or_Sequence_Running()
    {
        _service.Evaluate(CommandKind.Stop, ReadyManualContext() with { AxisBusy = true }).IsEnabled.Should().BeTrue();
        _service.Evaluate(CommandKind.Stop, ReadyManualContext() with { SequenceRunning = true }).IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ResetAlarm_Should_Be_Enabled_When_Alarm_Active()
    {
        _service.Evaluate(CommandKind.ResetAlarm, ReadyManualContext() with { AlarmActive = true }).IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void RunInspection_Should_Be_Disabled_When_Recipe_Not_Loaded()
    {
        _service.Evaluate(CommandKind.RunInspection, ReadyAutoContext() with { RecipeLoaded = false }).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void RunInspection_Should_Be_Disabled_When_All_Required_Axes_Are_Not_Homed()
    {
        _service.Evaluate(CommandKind.RunInspection, ReadyAutoContext() with { AllRequiredAxesHomed = false }).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void RunInspection_Should_Be_Disabled_When_Safety_Not_Ok()
    {
        _service.Evaluate(CommandKind.RunInspection, ReadyAutoContext() with { SafetyOk = false }).IsEnabled.Should().BeFalse();
    }

    private static InterlockContext ReadyManualContext()
    {
        return new InterlockContext(
            Connected: true,
            ControllerBusy: false,
            SequenceRunning: false,
            EmergencyStopActive: false,
            DoorClosed: true,
            SafetyOk: true,
            ManualMode: true,
            AutoMode: false,
            ServoOn: true,
            AxisHomed: true,
            AllRequiredAxesHomed: true,
            AxisBusy: false,
            AxisAlarm: false,
            WithinSoftLimit: true,
            RecipeLoaded: true,
            CameraConnected: true,
            IoReady: true,
            AlarmActive: false);
    }

    private static InterlockContext ReadyAutoContext()
    {
        return ReadyManualContext() with { ManualMode = false, AutoMode = true };
    }
}
