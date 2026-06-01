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
        result.ErrorCode?.Code.Should().Be("EQP-007");
        result.Message.Should().Contain("Home requires servo on");
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Reject_MoveAbsolute_When_Target_Is_Outside_Soft_Limit()
    {
        var controller = new VirtualEquipmentController();
        var context = ReadyManualContext() with { WithinSoftLimit = false };

        var result = await controller.ExecuteCommandAsync(CommandKind.MoveAbsolute, context, TimeSpan.FromSeconds(1), CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Rejected);
        result.ErrorCode?.Message.Should().NotBeNullOrWhiteSpace();
        result.Message.Should().Contain("soft limit");
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
