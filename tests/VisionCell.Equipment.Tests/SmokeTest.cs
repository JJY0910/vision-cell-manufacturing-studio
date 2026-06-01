using FluentAssertions;
using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Simulator;
using Xunit;

namespace VisionCell_Equipment_Tests;

public sealed class VirtualEquipmentControllerTests
{
    [Fact]
    public async Task ConnectAsync_Should_Update_Snapshot_To_Manual_Mode()
    {
        var controller = new VirtualEquipmentController();

        var result = await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        var snapshot = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Success);
        snapshot.IsConnected.Should().BeTrue();
        snapshot.Mode.Should().Be(MachineMode.Manual);
        snapshot.Axes.Should().HaveCount(4);
        snapshot.Io.Bits.Should().Contain(bit => bit.Name == "DI_DOOR_CLOSED" && bit.Value);
        snapshot.Camera.IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_Should_Return_Timeout_Result_When_Timeout_Elapses()
    {
        var controller = new VirtualEquipmentController();

        var result = await controller.ConnectAsync(TimeSpan.FromMilliseconds(1), CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Timeout);
        result.ErrorCode?.Code.Should().Be("EQP-005");
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Reject_Disabled_Command_With_Code_And_Message()
    {
        var controller = new VirtualEquipmentController();
        var context = ReadyManualContext() with { ServoOn = false };

        var result = await controller.ExecuteCommandAsync(CommandKind.Home, context, TimeSpan.FromSeconds(1), CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Rejected);
        result.ErrorCode?.Code.Should().Be("MOT-001");
        result.Message.Should().Contain("Home requires servo on");
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Reject_MoveAbsolute_When_Target_Is_Outside_Soft_Limit()
    {
        var controller = new VirtualEquipmentController();
        var context = ReadyManualContext() with { WithinSoftLimit = false };

        var result = await controller.ExecuteCommandAsync(CommandKind.MoveAbsolute, context, TimeSpan.FromSeconds(1), CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Rejected);
        result.ErrorCode?.Code.Should().Be("MOT-004");
        result.Message.Should().Contain("soft limit");
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Update_Servo_Output_For_Servo_On()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);

        var result = await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var snapshot = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Success);
        snapshot.Safety.ServoEnabled.Should().BeTrue();
        snapshot.Axes.Should().OnlyContain(axis => axis.ServoOn);
        snapshot.Io.Bits.Should().Contain(bit => bit.Name == "DO_SERVO_ENABLE" && bit.Value);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Home_All_Axes_With_Timeout_Result_Model()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);

        var result = await controller.ExecuteCommandAsync(CommandKind.Home, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var snapshot = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Success);
        snapshot.Axes.Should().OnlyContain(axis => axis.IsHomed && axis.Position == 0.0 && !axis.IsMoving);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Return_Motion_Timeout_And_Alarm_When_Move_Exceeds_Timeout()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);

        var result = await controller.ExecuteCommandAsync(CommandKind.MoveAbsolute, ReadyManualContext(), TimeSpan.FromMilliseconds(1), CancellationToken.None);
        var snapshot = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Timeout);
        result.ErrorCode?.Code.Should().Be("MOT-003");
        snapshot.Alarm?.ErrorCode.Code.Should().Be("MOT-003");
        snapshot.Axes.Should().OnlyContain(axis => axis.Alarm != null && !axis.IsMoving);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Return_Cancelled_When_Caller_Cancels_Motion()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));

        var result = await controller.ExecuteCommandAsync(CommandKind.Home, ReadyManualContext(), TimeSpan.FromSeconds(5), cancellation.Token);

        result.Status.Should().Be(CommandStatus.Cancelled);
        result.ErrorCode?.Code.Should().Be("EQP-006");
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Reject_Duplicate_Motion_While_Axis_Busy()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);

        var homeTask = controller.ExecuteCommandAsync(CommandKind.Home, ReadyManualContext(), TimeSpan.FromSeconds(5), CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(20));
        var duplicate = await controller.ExecuteCommandAsync(CommandKind.Jog, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var home = await homeTask;

        duplicate.Status.Should().Be(CommandStatus.Rejected);
        duplicate.Message.Should().Contain("axis idle");
        home.Status.Should().Be(CommandStatus.Success);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Stop_Active_Motion_With_Cancelled_Original_Result()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);

        var moveTask = controller.ExecuteCommandAsync(CommandKind.MoveAbsolute, ReadyManualContext(), TimeSpan.FromSeconds(5), CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(20));
        var stop = await controller.ExecuteCommandAsync(CommandKind.Stop, ReadyManualContext() with { AxisBusy = true }, TimeSpan.FromSeconds(1), CancellationToken.None);
        var move = await moveTask;
        var snapshot = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        stop.Status.Should().Be(CommandStatus.Success);
        move.Status.Should().Be(CommandStatus.Cancelled);
        snapshot.Axes.Should().OnlyContain(axis => !axis.IsMoving);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Map_EStop_Interlock_To_Explicit_Error_Code()
    {
        var controller = new VirtualEquipmentController();
        var context = ReadyManualContext() with { EmergencyStopActive = true, SafetyOk = false };

        var result = await controller.ExecuteCommandAsync(CommandKind.ServoOn, context, TimeSpan.FromSeconds(1), CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Rejected);
        result.ErrorCode?.Code.Should().Be("EQP-003");
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
}
