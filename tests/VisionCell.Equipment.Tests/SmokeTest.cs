using FluentAssertions;
using VisionCell.Core.Commands;
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
}
