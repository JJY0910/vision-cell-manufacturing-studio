using FluentAssertions;
using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Cameras;
using VisionCell.Equipment.Faults;
using VisionCell.Equipment.Hardware;
using VisionCell.Equipment.Io;
using VisionCell.Motion.Axes;
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
    public async Task ExecuteCommandAsync_Should_Run_Sequence_Move_To_Camera_In_Auto_Sequence()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.Home, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.EnterAutoMode, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var request = CreateRequest(
            "Sequence Move To Camera",
            TimeSpan.FromSeconds(1),
            new AbsoluteMoveTarget(12.0, 24.0, 6.0, 1.5).ToParameters());

        var result = await controller.ExecuteCommandAsync(
            CommandKind.SequenceMoveToCamera,
            ReadyManualContext() with { ManualMode = false, AutoMode = true, SequenceRunning = true },
            request,
            CancellationToken.None);
        var snapshot = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Success);
        snapshot.Axes.Single(axis => axis.AxisId == AxisId.X).Position.Should().Be(12.0);
        snapshot.Axes.Single(axis => axis.AxisId == AxisId.Y).Position.Should().Be(24.0);
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
    public async Task ApplyFaultAsync_Should_Surface_EStop_In_Snapshot_Io_And_Interlocks()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var request = CreateFaultRequest(EquipmentFaultKind.EmergencyStop, isActive: true);

        var fault = await controller.ApplyFaultAsync(request, CancellationToken.None);
        var snapshot = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);
        var servoOn = await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);

        fault.Status.Should().Be(CommandStatus.Success);
        fault.CorrelationId.Should().Be(request.CommandRequest.CorrelationId);
        snapshot.Mode.Should().Be(MachineMode.Alarm);
        snapshot.Safety.EmergencyStopActive.Should().BeTrue();
        snapshot.Safety.ServoEnabled.Should().BeFalse();
        snapshot.Alarm?.ErrorCode.Code.Should().Be("EQP-003");
        snapshot.Io.Bits.Should().Contain(bit => bit.Name == "DI_ESTOP_ON" && bit.Value && bit.IsForced);
        servoOn.Status.Should().Be(CommandStatus.Rejected);
        servoOn.ErrorCode?.Code.Should().Be("EQP-003");
    }

    [Fact]
    public async Task ApplyFaultAsync_Should_Surface_Camera_Not_Ready_And_Clear_All()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);

        await controller.ApplyFaultAsync(CreateFaultRequest(EquipmentFaultKind.CameraNotReady, isActive: true), CancellationToken.None);
        var faulted = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        await controller.ApplyFaultAsync(CreateFaultRequest(EquipmentFaultKind.ClearAll, isActive: false), CancellationToken.None);
        var cleared = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        faulted.Camera.IsReady.Should().BeFalse();
        faulted.Alarm?.ErrorCode.Code.Should().Be("CAM-002");
        faulted.Io.Bits.Should().Contain(bit => bit.Name == "DI_CAMERA_READY" && !bit.Value && bit.IsForced);
        cleared.Mode.Should().Be(MachineMode.Manual);
        cleared.Camera.IsReady.Should().BeTrue();
        cleared.Alarm.Should().BeNull();
    }

    [Fact]
    public async Task ApplyFaultAsync_Should_Surface_Servo_Alarm_On_Axes_And_Io()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);

        await controller.ApplyFaultAsync(CreateFaultRequest(EquipmentFaultKind.ServoAlarm, isActive: true), CancellationToken.None);
        var snapshot = await controller.GetSnapshotAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

        snapshot.Safety.ServoEnabled.Should().BeFalse();
        snapshot.Axes.Should().OnlyContain(axis => axis.Alarm != null && axis.Alarm.ErrorCode.Code == "MOT-005");
        snapshot.Io.Bits.Should().Contain(bit => bit.Name == "DI_SERVO_ALARM" && bit.Value && bit.IsForced);
        snapshot.Alarm?.ErrorCode.Code.Should().Be("MOT-005");
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Preserve_Request_Correlation_For_Success_Result()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.Home, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var request = CreateRequest(
            "Move Absolute",
            TimeSpan.FromSeconds(1),
            new AbsoluteMoveTarget(1.0, 2.0, 3.0, 4.0).ToParameters());

        var result = await controller.ExecuteCommandAsync(CommandKind.MoveAbsolute, ReadyManualContext(), request, CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Success);
        result.CorrelationId.Should().Be(request.CorrelationId);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Preserve_Request_Correlation_For_Rejected_Result()
    {
        var controller = new VirtualEquipmentController();
        var request = CreateRequest("Home", TimeSpan.FromSeconds(1), new Dictionary<string, string>());

        var result = await controller.ExecuteCommandAsync(
            CommandKind.Home,
            ReadyManualContext() with { ServoOn = false },
            request,
            CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Rejected);
        result.CorrelationId.Should().Be(request.CorrelationId);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Preserve_Request_Correlation_For_Timeout_Result()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var request = CreateRequest(
            "Move Absolute",
            TimeSpan.FromMilliseconds(1),
            new AbsoluteMoveTarget(1.0, 2.0, 3.0, 4.0).ToParameters());

        var result = await controller.ExecuteCommandAsync(CommandKind.MoveAbsolute, ReadyManualContext(), request, CancellationToken.None);

        result.Status.Should().Be(CommandStatus.Timeout);
        result.CorrelationId.Should().Be(request.CorrelationId);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Preserve_Request_Correlation_For_Cancelled_Result()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var request = CreateRequest("Home", TimeSpan.FromSeconds(5), new Dictionary<string, string>());
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));

        var result = await controller.ExecuteCommandAsync(CommandKind.Home, ReadyManualContext(), request, cancellation.Token);

        result.Status.Should().Be(CommandStatus.Cancelled);
        result.CorrelationId.Should().Be(request.CorrelationId);
    }

    [Fact]
    public async Task ExecuteCommandAsync_Should_Preserve_Request_Correlation_For_Stop_Result()
    {
        var controller = new VirtualEquipmentController();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        await controller.ExecuteCommandAsync(CommandKind.ServoOn, ReadyManualContext(), TimeSpan.FromSeconds(1), CancellationToken.None);
        var moveTask = controller.ExecuteCommandAsync(CommandKind.MoveAbsolute, ReadyManualContext(), TimeSpan.FromSeconds(5), CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(20));
        var stopRequest = CreateRequest("Stop", TimeSpan.FromSeconds(1), new Dictionary<string, string>());

        var stop = await controller.ExecuteCommandAsync(CommandKind.Stop, ReadyManualContext() with { AxisBusy = true }, stopRequest, CancellationToken.None);
        await moveTask;

        stop.Status.Should().Be(CommandStatus.Success);
        stop.CorrelationId.Should().Be(stopRequest.CorrelationId);
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

    [Fact]
    public async Task HardwareAdapterContracts_Should_Surface_Status_Axes_Camera_And_Io_Boundaries()
    {
        var motion = new FakeMotionControllerAdapter();
        var camera = new FakeCameraAdapter();
        var plc = new FakePlcIoAdapter();
        var request = CreateRequest("Adapter Move", TimeSpan.FromSeconds(1), new Dictionary<string, string>());

        var motionStatus = await motion.GetStatusAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        var axes = await motion.ReadAxesAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        var cameraStatus = await camera.GetCameraSnapshotAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        var io = await plc.ReadIoAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        var write = await plc.WriteOutputAsync("DO_TOWER_RED", true, request, CancellationToken.None);

        motionStatus.AdapterName.Should().Be("Fake Motion");
        axes.Should().HaveCount(4);
        cameraStatus.IsReady.Should().BeTrue();
        io.Bits.Should().Contain(bit => bit.Name == "DO_TOWER_RED" && bit.Direction == IoBitDirection.Output);
        write.Status.Should().Be(CommandStatus.Success);
        write.CorrelationId.Should().Be(request.CorrelationId);
    }

    [Fact]
    public void HardwareAdapterStatus_Should_Reject_Missing_Identity()
    {
        var act = () => new HardwareAdapterStatus(
            "",
            isConnected: false,
            isReady: false,
            "tcp://127.0.0.1:5000",
            "Disconnected.",
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>().WithMessage("*Adapter name*");
    }

    [Fact]
    public void HardwareAdapterBoundaryCatalog_Should_List_Required_RealHardware_Adapter_Boundaries()
    {
        var requirements = HardwareAdapterBoundaryCatalog.RequiredAdapters;

        requirements.Should().HaveCount(3);
        requirements.Should().Contain(adapter =>
            adapter.Role == HardwareAdapterRole.MotionController &&
            adapter.RoleName == "Motion Controller" &&
            adapter.InterfaceName == nameof(IMotionControllerAdapter) &&
            adapter.PlannedAdapterName == "MotionControllerAdapter" &&
            adapter.RequiredEvidence == "motion adapter bench validation");
        requirements.Should().Contain(adapter =>
            adapter.Role == HardwareAdapterRole.Camera &&
            adapter.InterfaceName == nameof(ICameraAdapter) &&
            adapter.PlannedAdapterName == "CameraAdapter" &&
            adapter.RequiredEvidence == "camera adapter bench validation");
        requirements.Should().Contain(adapter =>
            adapter.Role == HardwareAdapterRole.PlcIo &&
            adapter.RoleName == "PLC I/O" &&
            adapter.InterfaceName == nameof(IPlcIoAdapter) &&
            adapter.PlannedAdapterName == "PlcIoAdapter" &&
            adapter.RequiredEvidence == "PLC I/O adapter bench validation");
        requirements.Should().OnlyContain(adapter =>
            !string.IsNullOrWhiteSpace(adapter.CurrentProvider) &&
            !string.IsNullOrWhiteSpace(adapter.BoundaryNotes));
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

    private static EquipmentFaultInjectionRequest CreateFaultRequest(
        EquipmentFaultKind kind,
        bool isActive)
    {
        return new EquipmentFaultInjectionRequest(
            kind,
            isActive,
            new MachineCommandRequest(
                $"Fault {kind}",
                CorrelationId.New(),
                TimeSpan.FromSeconds(1),
                DateTimeOffset.UtcNow,
                new Dictionary<string, string>
                {
                    ["FaultKind"] = kind.ToString(),
                    ["IsActive"] = isActive.ToString()
                }));
    }

    private sealed class FakeMotionControllerAdapter : IMotionControllerAdapter
    {
        public string AdapterName => "Fake Motion";

        public Task<HardwareAdapterStatus> GetStatusAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HardwareAdapterStatus(
                AdapterName,
                isConnected: true,
                isReady: true,
                "virtual-motion://stage",
                "Motion adapter ready.",
                DateTimeOffset.UtcNow));
        }

        public Task<IReadOnlyList<AxisSnapshot>> ReadAxesAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<AxisSnapshot>>(AxisDefaults.CreatePowerOffAxes());
        }

        public Task<MachineCommandResult> ExecuteMotionAsync(
            CommandKind command,
            InterlockContext context,
            MachineCommandRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(MachineCommandResult.Success("Adapter motion accepted.", TimeSpan.Zero, request.CorrelationId));
        }
    }

    private sealed class FakeCameraAdapter : ICameraAdapter
    {
        public string AdapterName => "Fake Camera";

        public Task<HardwareAdapterStatus> GetStatusAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HardwareAdapterStatus(
                AdapterName,
                isConnected: true,
                isReady: true,
                "virtual-camera://top",
                "Camera adapter ready.",
                DateTimeOffset.UtcNow));
        }

        public Task<CameraSnapshot> GetCameraSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CameraSnapshot(true, "Fake Camera", DateTimeOffset.UtcNow));
        }

        public Task<CameraGrabResult> GrabAsync(CameraGrabRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CameraGrabResult.Timeout("No real camera connected.", TimeSpan.Zero, request.CorrelationId));
        }
    }

    private sealed class FakePlcIoAdapter : IPlcIoAdapter
    {
        public string AdapterName => "Fake PLC I/O";

        public Task<HardwareAdapterStatus> GetStatusAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HardwareAdapterStatus(
                AdapterName,
                isConnected: true,
                isReady: true,
                "virtual-plc://io",
                "PLC I/O adapter ready.",
                DateTimeOffset.UtcNow));
        }

        public Task<IoSnapshot> ReadIoAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new IoSnapshot(
                new[]
                {
                    new IoBitSnapshot("DI_ESTOP_ON", "X000", IoBitDirection.Input, false, false),
                    new IoBitSnapshot("DO_TOWER_RED", "Y004", IoBitDirection.Output, false, false)
                },
                DateTimeOffset.UtcNow));
        }

        public Task<MachineCommandResult> WriteOutputAsync(
            string bitName,
            bool value,
            MachineCommandRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(MachineCommandResult.Success($"{bitName} set to {value}.", TimeSpan.Zero, request.CorrelationId));
        }
    }
}
