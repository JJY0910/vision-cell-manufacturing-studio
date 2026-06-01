using FluentAssertions;
using VisionCell.Application.Interlocks;
using VisionCell.Application.Motion;
using VisionCell.Application.Teaching;
using VisionCell.Core.Commands;
using VisionCell.Core.Errors;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.App.Modules.Dashboard.ViewModels;
using VisionCell.App.Modules.Equipment.ViewModels;
using VisionCell.App.Modules.Inspection.ViewModels;
using VisionCell.App.Modules.Motion.ViewModels;
using VisionCell.App.Modules.OfflineDebug.ViewModels;
using VisionCell.App.Modules.Recipe.ViewModels;
using VisionCell.App.Modules.Reports.ViewModels;
using VisionCell.App.Modules.Settings.ViewModels;
using VisionCell.App.Modules.Teaching.ViewModels;
using VisionCell.App.Navigation;
using VisionCell.App.Shell;
using VisionCell.Equipment.Cameras;
using VisionCell.Equipment.Controllers;
using VisionCell.Equipment.Io;
using VisionCell.Equipment.Safety;
using VisionCell.Motion.Axes;
using VisionCell.Motion.Commands;
using VisionCell.Motion.Teaching;
using VisionCell.Simulator;
using Xunit;

namespace VisionCell_App_Tests;

public sealed class DashboardAndShellViewModelTests
{
    [Fact]
    public async Task Dashboard_ConnectAsync_Should_Surface_Connection_Axis_Io_And_EventLog_State()
    {
        var dashboard = new DashboardViewModel(new VirtualEquipmentController(), new CommandInterlockService());

        await dashboard.ConnectAsync(CancellationToken.None);

        dashboard.IsConnected.Should().BeTrue();
        dashboard.ConnectionStatus.Should().Be("Connected");
        dashboard.Axes.Should().HaveCount(4);
        dashboard.IoBits.Should().Contain(bit => bit.Name == "DI_ESTOP_ON");
        dashboard.Events.Should().Contain(systemEvent => systemEvent.EventType == "Connect");
        dashboard.GetCommandAvailability(CommandKind.Connect).IsEnabled.Should().BeFalse();
        dashboard.GetCommandAvailability(CommandKind.Disconnect).IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Shell_Navigation_Should_Change_Current_ViewModel_And_Selected_Item()
    {
        var shell = new ShellViewModel(
            new NavigationService(),
            new DashboardViewModel(new VirtualEquipmentController(), new CommandInterlockService()),
            new EquipmentViewModel(),
            CreateMotionViewModel(),
            CreateTeachingViewModel(),
            new RecipeViewModel(),
            new InspectionViewModel(),
            new OfflineDebugViewModel(),
            new ReportsViewModel(),
            new SettingsViewModel());

        var motionItem = shell.NavigationItems.Single(item => item.Key == "Motion");
        motionItem.NavigateCommand.Execute(null);

        shell.CurrentViewModel.Should().BeOfType<MotionViewModel>();
        motionItem.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void Dashboard_Initial_State_Should_Disable_Dangerous_Commands()
    {
        var dashboard = new DashboardViewModel(new VirtualEquipmentController(), new CommandInterlockService());

        dashboard.GetCommandAvailability(CommandKind.Connect).IsEnabled.Should().BeTrue();
        dashboard.GetCommandAvailability(CommandKind.Home).IsEnabled.Should().BeFalse();
        dashboard.GetCommandAvailability(CommandKind.Jog).IsEnabled.Should().BeFalse();
        dashboard.GetCommandAvailability(CommandKind.MoveAbsolute).IsEnabled.Should().BeFalse();
        dashboard.GetCommandAvailability(CommandKind.RunInspection).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Motion_RefreshHistoryAsync_Should_Load_Recent_Command_State()
    {
        var createdAt = new DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.Zero);
        var motion = CreateMotionViewModel(new FakeMotionCommandHistoryReader(
            new MotionCommandHistoryRecord(
                Guid.NewGuid(),
                CorrelationId.New().ToString(),
                "Move Absolute",
                "X",
                CommandStatus.Success,
                null,
                "Move Absolute completed.",
                TimeSpan.FromMilliseconds(42),
                createdAt)));

        await motion.RefreshHistoryAsync(CancellationToken.None);

        motion.HasHistory.Should().BeTrue();
        motion.RecentCommands.Should().ContainSingle();
        motion.RecentCommands[0].CommandName.Should().Be("Move Absolute");
        motion.RecentCommands[0].AxisId.Should().Be("X");
        motion.RecentCommands[0].Status.Should().Be(CommandStatus.Success);
        motion.RecentCommands[0].ErrorCode.Should().Be("-");
        motion.HistoryStatus.Should().Be("1 command records loaded");
        motion.LastHistoryRefreshAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Motion_RefreshHistoryAsync_Should_Surface_Empty_State()
    {
        var motion = CreateMotionViewModel(new FakeMotionCommandHistoryReader());

        await motion.RefreshHistoryAsync(CancellationToken.None);

        motion.HasHistory.Should().BeFalse();
        motion.RecentCommands.Should().BeEmpty();
        motion.HistoryStatus.Should().Be("No motion command history");
    }

    [Fact]
    public async Task Motion_RefreshSnapshotAsync_Should_Enable_ServoOn_When_Controller_Is_Ready()
    {
        var motion = CreateMotionViewModel(equipmentController: new FakeEquipmentController(
            CreateSnapshot(connected: true, servoOn: false, homed: false)));

        await motion.RefreshSnapshotAsync(CancellationToken.None);

        motion.ControllerStatus.Should().Be("Controller: Connected");
        motion.ServoStatus.Should().Be("Servo: Off");
        motion.AxisStatus.Should().Contain("0/4 homed");
        motion.Axes.Should().HaveCount(4);
        motion.Axes[0].Label.Should().Be("X");
        motion.Axes[0].PositionText.Should().Contain("0.000");
        motion.Axes[0].SoftLimitText.Should().Contain("Limit");
        motion.ServoOnCommand.CanExecute(null).Should().BeTrue();
        motion.HomeCommand.CanExecute(null).Should().BeFalse();
        motion.HomeDisabledReason.Should().Contain("Home requires servo on.");
    }

    [Fact]
    public async Task Motion_ExecuteServoOnAsync_Should_Run_UseCase_And_Refresh_History()
    {
        var useCase = new FakeMotionCommandUseCase();
        var historyReader = new FakeMotionCommandHistoryReader(
            new MotionCommandHistoryRecord(
                Guid.NewGuid(),
                CorrelationId.New().ToString(),
                "Servo On",
                "-",
                CommandStatus.Success,
                null,
                "Servo On completed.",
                TimeSpan.FromMilliseconds(8),
                DateTimeOffset.UtcNow));
        var motion = CreateMotionViewModel(
            historyReader,
            useCase,
            new FakeEquipmentController(CreateSnapshot(connected: true, servoOn: false, homed: false)));

        await motion.RefreshSnapshotAsync(CancellationToken.None);
        await motion.ExecuteServoOnAsync(CancellationToken.None);

        useCase.Requests.Should().ContainSingle();
        var request = useCase.Requests[0];
        request.Command.Should().Be(CommandKind.ServoOn);
        request.InterlockContext.Connected.Should().BeTrue();
        request.GetParameters().Should().Contain("ServoState", "On");
        motion.CommandStatus.Should().Contain("Servo On Success");
        motion.RecentCommands.Should().ContainSingle();
        motion.LastCommandCorrelationId.Should().NotBe("-");
    }

    [Fact]
    public async Task Motion_ExecuteMoveAbsoluteAsync_Should_Send_Typed_Target_Parameters()
    {
        var useCase = new FakeMotionCommandUseCase();
        var motion = CreateMotionViewModel(
            commandUseCase: useCase,
            equipmentController: new FakeEquipmentController(CreateSnapshot(connected: true, servoOn: true, homed: true)));
        motion.MoveTargetXText = "12.500";
        motion.MoveTargetYText = "-4.000";
        motion.MoveTargetZText = "6.000";
        motion.MoveTargetThetaText = "15.000";
        motion.SelectedMoveProfilePreset = MotionProfilePreset.Fast;

        await motion.RefreshSnapshotAsync(CancellationToken.None);
        await motion.ExecuteMoveAbsoluteAsync(CancellationToken.None);

        useCase.Requests.Should().ContainSingle();
        var parameters = useCase.Requests[0].GetParameters();
        useCase.Requests[0].Command.Should().Be(CommandKind.MoveAbsolute);
        parameters.Should().Contain("X", "12.5");
        parameters.Should().Contain("Y", "-4");
        parameters.Should().Contain("Z", "6");
        parameters.Should().Contain("Theta", "15");
        parameters.Should().Contain("Velocity", "125");
        parameters.Should().Contain("Acceleration", "300");
        parameters.Should().Contain("Deceleration", "250");
        parameters.Should().Contain("Jerk", "1500");
        parameters.Should().Contain("ArrivalTolerance", "0.02");
        parameters.Should().Contain("ProfilePreset", "Fast");
    }

    [Fact]
    public void Motion_SelectMoveProfilePreset_Should_Update_Profile_Input_Text()
    {
        var motion = CreateMotionViewModel();

        motion.SelectedMoveProfilePreset = MotionProfilePreset.Fine;

        motion.MoveVelocityText.Should().Be("10");
        motion.MoveAccelerationText.Should().Be("80");
        motion.MoveDecelerationText.Should().Be("80");
        motion.MoveJerkText.Should().Be("400");
        motion.MoveArrivalToleranceText.Should().Be("0.005");
    }

    [Fact]
    public async Task Motion_ExecuteMoveAbsoluteAsync_Should_Reject_Invalid_Profile_Input()
    {
        var useCase = new FakeMotionCommandUseCase();
        var motion = CreateMotionViewModel(
            commandUseCase: useCase,
            equipmentController: new FakeEquipmentController(CreateSnapshot(connected: true, servoOn: true, homed: true)));
        motion.MoveVelocityText = "0";

        await motion.RefreshSnapshotAsync(CancellationToken.None);
        await motion.ExecuteMoveAbsoluteAsync(CancellationToken.None);

        useCase.Requests.Should().BeEmpty();
        motion.CommandStatus.Should().Contain("Velocity must be greater than zero");
    }

    [Fact]
    public async Task Teaching_SaveCurrentPositionAsync_Should_Save_And_Refresh_List()
    {
        var useCase = new FakeTeachingPointUseCase();
        var teaching = CreateTeachingViewModel(useCase);
        teaching.NameText = "Safe Park";
        teaching.SelectedRole = TeachingRole.Safe;
        teaching.MemoText = "verified";

        await teaching.SaveCurrentPositionAsync(CancellationToken.None);

        useCase.SaveRequests.Should().ContainSingle();
        useCase.SaveRequests[0].Name.Should().Be("Safe Park");
        useCase.SaveRequests[0].Role.Should().Be(TeachingRole.Safe);
        teaching.Points.Should().ContainSingle();
        teaching.SelectedPoint.Should().NotBeNull();
        teaching.StatusText.Should().Contain("teaching points loaded");
    }

    [Fact]
    public async Task Teaching_GoToSelectedAsync_Should_Execute_Selected_Point()
    {
        var point = TeachingPointFactory.Create(
            "Review",
            TeachingRole.Review,
            new Position4D(1.0, 2.0, 3.0, 4.0),
            PositionTolerance.Default).Point!;
        var useCase = new FakeTeachingPointUseCase(point);
        var teaching = CreateTeachingViewModel(
            useCase,
            new FakeEquipmentController(CreateSnapshot(connected: true, servoOn: true, homed: true)));

        await teaching.RefreshAsync(CancellationToken.None);
        teaching.SelectedPoint = teaching.Points.Single();
        await teaching.GoToSelectedAsync(CancellationToken.None);

        useCase.GoToRequests.Should().ContainSingle();
        useCase.GoToRequests[0].TeachingPointId.Should().Be(point.Id);
        useCase.GoToRequests[0].InterlockContext.ServoOn.Should().BeTrue();
        teaching.StatusText.Should().Contain("completed");
    }

    [Fact]
    public async Task Motion_ExecuteJogNegativeAsync_Should_Send_Selected_Axis_And_Step()
    {
        var useCase = new FakeMotionCommandUseCase();
        var motion = CreateMotionViewModel(
            commandUseCase: useCase,
            equipmentController: new FakeEquipmentController(CreateSnapshot(connected: true, servoOn: true, homed: true)));
        motion.SelectedJogAxis = "Y";
        motion.JogStepText = "2.500";

        await motion.RefreshSnapshotAsync(CancellationToken.None);
        await motion.ExecuteJogNegativeAsync(CancellationToken.None);

        useCase.Requests.Should().ContainSingle();
        var parameters = useCase.Requests[0].GetParameters();
        useCase.Requests[0].Command.Should().Be(CommandKind.Jog);
        parameters.Should().Contain("Axis", "Y");
        parameters.Should().Contain("Direction", "-");
        parameters.Should().Contain("Step", "2.5");
    }

    private static MotionViewModel CreateMotionViewModel(
        FakeMotionCommandHistoryReader? historyReader = null,
        FakeMotionCommandUseCase? commandUseCase = null,
        FakeEquipmentController? equipmentController = null)
    {
        return new MotionViewModel(
            commandUseCase ?? new FakeMotionCommandUseCase(),
            historyReader ?? new FakeMotionCommandHistoryReader(),
            equipmentController ?? new FakeEquipmentController(CreateSnapshot()));
    }

    private static TeachingViewModel CreateTeachingViewModel(
        FakeTeachingPointUseCase? useCase = null,
        FakeEquipmentController? equipmentController = null)
    {
        return new TeachingViewModel(
            useCase ?? new FakeTeachingPointUseCase(),
            equipmentController ?? new FakeEquipmentController(CreateSnapshot(connected: true, servoOn: true, homed: true)));
    }

    private sealed class FakeMotionCommandHistoryReader : IMotionCommandHistoryReader
    {
        private readonly IReadOnlyList<MotionCommandHistoryRecord> _records;

        public FakeMotionCommandHistoryReader(params MotionCommandHistoryRecord[] records)
        {
            _records = records;
        }

        public Task<IReadOnlyList<MotionCommandHistoryRecord>> ListRecentAsync(int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<MotionCommandHistoryRecord>>(_records.Take(limit).ToArray());
        }
    }

    private sealed class FakeMotionCommandUseCase : IMotionCommandUseCase
    {
        public List<MotionCommandExecutionRequest> Requests { get; } = new();

        public Task<MotionCommandExecutionResult> ExecuteAsync(
            MotionCommandExecutionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var commandRequest = new MachineCommandRequest(
                FormatCommand(request.Command),
                CorrelationId.New(),
                request.Timeout,
                DateTimeOffset.UtcNow,
                request.GetParameters());
            var commandResult = MachineCommandResult.Success(
                $"{commandRequest.CommandName} completed.",
                TimeSpan.FromMilliseconds(8),
                commandRequest.CorrelationId);

            return Task.FromResult(new MotionCommandExecutionResult(commandRequest, commandResult));
        }
    }

    private sealed class FakeTeachingPointUseCase : ITeachingPointUseCase
    {
        private readonly List<TeachingPoint> _points = new();

        public FakeTeachingPointUseCase(params TeachingPoint[] points)
        {
            _points.AddRange(points);
        }

        public List<TeachingPointSaveRequest> SaveRequests { get; } = new();
        public List<TeachingPointGoToRequest> GoToRequests { get; } = new();

        public Task<IReadOnlyList<TeachingPoint>> ListAsync(int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TeachingPoint>>(_points.Take(limit).ToArray());
        }

        public Task<TeachingPointSaveResult> SaveCurrentPositionAsync(
            TeachingPointSaveRequest request,
            CancellationToken cancellationToken)
        {
            SaveRequests.Add(request);
            var point = TeachingPointFactory.Create(
                request.Name,
                request.Role,
                new Position4D(1.0, 2.0, 3.0, 4.0),
                request.Tolerance,
                request.Memo).Point!;
            _points.Insert(0, point);
            return Task.FromResult(TeachingPointSaveResult.Success(point));
        }

        public Task<TeachingPointGoToResult> GoToAsync(
            TeachingPointGoToRequest request,
            CancellationToken cancellationToken)
        {
            GoToRequests.Add(request);
            var point = _points.Single(item => item.Id == request.TeachingPointId);
            var commandRequest = new MachineCommandRequest(
                "Move Absolute",
                CorrelationId.New(),
                request.Timeout,
                DateTimeOffset.UtcNow,
                new Dictionary<string, string>());
            var commandResult = MachineCommandResult.Success(
                "Move completed.",
                TimeSpan.FromMilliseconds(10),
                commandRequest.CorrelationId);
            return Task.FromResult(TeachingPointGoToResult.Success(
                point,
                new MotionCommandExecutionResult(commandRequest, commandResult)));
        }
    }

    private sealed class FakeEquipmentController : IEquipmentController
    {
        public FakeEquipmentController(EquipmentSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public EquipmentSnapshot Snapshot { get; set; }

        public Task<EquipmentSnapshot> GetSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(Snapshot);
        }

        public Task<MachineCommandResult> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(MachineCommandResult.Success("Connected.", TimeSpan.Zero, CorrelationId.New()));
        }

        public Task<MachineCommandResult> DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult(MachineCommandResult.Success("Disconnected.", TimeSpan.Zero, CorrelationId.New()));
        }

        public CommandAvailability GetCommandAvailability(CommandKind command, InterlockContext context)
        {
            return CommandInterlockRules.Evaluate(command, context);
        }

        public Task<MachineCommandResult> ExecuteCommandAsync(
            CommandKind command,
            InterlockContext context,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(MachineCommandResult.Failed(
                ErrorCode.CommandRejected,
                "Fake controller does not execute motion commands in app tests.",
                TimeSpan.Zero,
                CorrelationId.New()));
        }
    }

    private static EquipmentSnapshot CreateSnapshot(
        bool connected = false,
        bool servoOn = false,
        bool homed = false)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var axes = AxisDefaults.CreatePowerOffAxes()
            .Select(axis => axis with
            {
                ServoOn = servoOn,
                IsHomed = homed,
                Position = homed ? 0.0 : axis.Position,
                Target = homed ? 0.0 : axis.Target
            })
            .ToArray();

        var io = new IoSnapshot(
            new[]
            {
                new IoBitSnapshot("DI_DOOR_CLOSED", "X000", IoBitDirection.Input, true, false),
                new IoBitSnapshot("DI_ESTOP_ON", "X001", IoBitDirection.Input, false, false),
                new IoBitSnapshot("DI_AIR_PRESSURE_OK", "X002", IoBitDirection.Input, true, false),
                new IoBitSnapshot("DO_SERVO_ENABLE", "Y000", IoBitDirection.Output, servoOn, false)
            },
            timestamp);

        return new EquipmentSnapshot(
            connected,
            connected ? MachineMode.Manual : MachineMode.Offline,
            new SafetySnapshot(true, false, true, false, servoOn),
            axes,
            io,
            new CameraSnapshot(connected, "Virtual 3D camera", timestamp),
            null,
            timestamp);
    }

    private static string FormatCommand(CommandKind command)
    {
        return command switch
        {
            CommandKind.ServoOn => "Servo On",
            CommandKind.ServoOff => "Servo Off",
            CommandKind.MoveAbsolute => "Move Absolute",
            _ => command.ToString()
        };
    }
}
