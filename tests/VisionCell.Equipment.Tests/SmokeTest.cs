using FluentAssertions;
using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Cameras;
using VisionCell.Motion.Commands;
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
    public async Task ExecuteCommandAsync_Should_Enter_Auto_And_Manual_Mode()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.Home, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);

        var auto = await controller.ExecuteCommandAsync(CommandKind.EnterAutoMode, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var autoSnapshot = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);
        var runInspection = await controller.ExecuteCommandAsync(CommandKind.RunInspection, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var manual = await controller.ExecuteCommandAsync(CommandKind.EnterManualMode, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var manualSnapshot = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        auto.Status.Should().Be(CommandStatus.Success);
        autoSnapshot.Mode.Should().Be(MachineMode.Auto);
        runInspection.Status.Should().Be(CommandStatus.Success);
        manual.Status.Should().Be(CommandStatus.Success);
        manualSnapshot.Mode.Should().Be(MachineMode.Manual);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Reject_EnterAuto_When_Axes_Not_Homed()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);

        var result = await controller.ExecuteCommandAsync(
            CommandKind.EnterAutoMode,
            ReadyManualContext() with { AllRequiredAxesHomed = false, AxisHomed = false },
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Rejected);
        result.Message.Should().Contain("all required axes homed");
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Apply_Typed_Jog_Target()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.Home, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var request = CreateRequest(
            "Jog",
            TimeSpan.FromSeconds(1),
            new JogMotionTarget(AxisId.Y, MotionDirection.Negative, 2.5).ToParameters());

        var result = await controller.ExecuteCommandAsync(CommandKind.Jog, ReadyManualContext(), request, CancellationToken.None);
        var snapshot = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Success);
        result.Message.Should().Contain("Y -2.500");
        snapshot.Axes.Single(axis => axis.AxisId == AxisId.Y).Position.Should().Be(-2.5);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Apply_Typed_Absolute_Move_Target()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.Home, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var request = CreateRequest(
            "Move Absolute",
            TimeSpan.FromSeconds(1),
            new AbsoluteMoveTarget(12.5, -4.0, 6.0, 15.0, 125.0, 300.0, 250.0, 1500.0, 0.02, "Fast").ToParameters());

        var result = await controller.ExecuteCommandAsync(CommandKind.MoveAbsolute, ReadyManualContext(), request, CancellationToken.None);
        var snapshot = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Success);
        result.Message.Should().Contain("profile=Fast");
        result.Message.Should().Contain("velocity=125.000");
        result.Message.Should().Contain("tolerance=0.020");
        snapshot.Axes.Single(axis => axis.AxisId == AxisId.X).Position.Should().Be(12.5);
        snapshot.Axes.Single(axis => axis.AxisId == AxisId.Y).Position.Should().Be(-4.0);
        snapshot.Axes.Single(axis => axis.AxisId == AxisId.Z).Position.Should().Be(6.0);
        snapshot.Axes.Single(axis => axis.AxisId == AxisId.Theta).Position.Should().Be(15.0);
        snapshot.Axes.Should().OnlyContain(axis =>
            axis.Profile.Velocity == 125.0 &&
            axis.Profile.Acceleration == 300.0 &&
            axis.Profile.Deceleration == 250.0 &&
            axis.Profile.Jerk == 1500.0);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Reject_Typed_Target_Outside_Soft_Limit()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.Home, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var request = CreateRequest(
            "Move Absolute",
            TimeSpan.FromSeconds(1),
            new AbsoluteMoveTarget(12.5, -4.0, 999.0, 15.0).ToParameters());

        var result = await controller.ExecuteCommandAsync(CommandKind.MoveAbsolute, ReadyManualContext(), request, CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Rejected);
        result.ErrorCode?.Code.Should().Be("MOT-004");
        result.Message.Should().Contain("soft limit");
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Reject_Invalid_Typed_Profile()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.Home, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var request = CreateRequest(
            "Move Absolute",
            TimeSpan.FromSeconds(1),
            new AbsoluteMoveTarget(12.5, -4.0, 6.0, 15.0, 0.0, 300.0, 250.0, 1500.0, 0.02).ToParameters());

        var result = await controller.ExecuteCommandAsync(CommandKind.MoveAbsolute, ReadyManualContext(), request, CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Rejected);
        result.ErrorCode?.Code.Should().Be("EQP-007");
        result.Message.Should().Contain("Velocity must be greater than zero");
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Reject_Empty_Profile_Preset_Name()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.Home, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var parameters = new Dictionary<string, string>(
            new AbsoluteMoveTarget(12.5, -4.0, 6.0, 15.0).ToParameters())
        {
            ["ProfilePreset"] = " "
        };
        var request = CreateRequest("Move Absolute", TimeSpan.FromSeconds(1), parameters);

        var result = await controller.ExecuteCommandAsync(CommandKind.MoveAbsolute, ReadyManualContext(), request, CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Rejected);
        result.ErrorCode?.Code.Should().Be("EQP-007");
        result.Message.Should().Contain("ProfilePreset must not be empty");
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

    [Fact]
    public async Task VirtualCameraDevice_GrabAsync_Should_Return_Synthetic_Gray8_Frame_With_Metadata()
    {
        var camera = new VirtualCameraDevice(() => new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        var request = CreateCameraRequest(TimeSpan.FromSeconds(1));

        var result = await camera.GrabAsync(request, CancellationToken.None);

        result.Status.Should().Be(CameraGrabStatus.Success);
        result.ErrorCode.Should().BeNull();
        result.Frame.Should().NotBeNull();
        result.Frame!.Width.Should().Be(320);
        result.Frame.Height.Should().Be(240);
        result.Frame.PixelFormat.Should().Be(CameraPixelFormat.Gray8);
        result.Frame.Pixels.Should().HaveCount(result.Frame.Stride * result.Frame.Height);
        result.Frame.Metadata.Should().Contain("RecipeId", "RCP-CAM");
    }

    [Fact]
    public async Task VirtualCameraDevice_GrabAsync_Should_Return_Timeout_When_Injected()
    {
        var camera = new VirtualCameraDevice
        {
            InjectGrabTimeout = true
        };
        var request = CreateCameraRequest(TimeSpan.FromMilliseconds(5));

        var result = await camera.GrabAsync(request, CancellationToken.None);

        result.Status.Should().Be(CameraGrabStatus.Timeout);
        result.ErrorCode?.Code.Should().Be("CAM-001");
        result.Frame.Should().BeNull();
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

    private static MachineCommandRequest CreateRequest(
        string commandName,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string> parameters)
    {
        return new MachineCommandRequest(
            commandName,
            CorrelationId.New(),
            timeout,
            DateTimeOffset.UtcNow,
            parameters);
    }

    private static CameraGrabRequest CreateCameraRequest(TimeSpan timeout)
    {
        return new CameraGrabRequest(
            CorrelationId.New(),
            timeout,
            DateTimeOffset.UtcNow,
            "RCP-CAM",
            "1.0.0",
            exposureMilliseconds: 5.0,
            gain: 1.0,
            lightIntensity: 80);
    }
}
