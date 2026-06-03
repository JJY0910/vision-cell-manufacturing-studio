using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VisionCell.App;
using VisionCell.Application.Alarms;
using VisionCell.Application.Equipment;
using VisionCell.Application.Interlocks;
using VisionCell.Application.Inspection;
using VisionCell.Application.Motion;
using VisionCell.Application.Recipes;
using VisionCell.Application.Teaching;
using VisionCell.App.Configuration;
using VisionCell.App.Interaction;
using VisionCell.App.Modules.Alarm.ViewModels;
using VisionCell.Core.Commands;
using VisionCell.Core.Alarms;
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
using VisionCell.Equipment.Faults;
using VisionCell.Equipment.Io;
using VisionCell.Equipment.Safety;
using VisionCell.Motion.Axes;
using VisionCell.Motion.Commands;
using VisionCell.Motion.Teaching;
using VisionCell.Persistence.Inspection;
using VisionCell.Persistence.Alarms;
using VisionCell.Persistence.Equipment;
using VisionCell.Simulator;
using VisionCell.Vision.Inspection;
using Xunit;

namespace VisionCell_App_Tests;

public sealed class DashboardAndShellViewModelTests
{
    [Fact]
    public async Task Dashboard_ConnectAsync_Should_Surface_Connection_Axis_Io_And_EventLog_State()
    {
        var dashboard = CreateDashboardViewModel();

        await dashboard.ConnectAsync(CancellationToken.None);

        dashboard.IsConnected.Should().BeTrue();
        dashboard.ConnectionStatus.Should().Be("Connected");
        dashboard.Axes.Should().HaveCount(4);
        dashboard.Axes[0].PositionText.Should().Contain("Pos");
        dashboard.Axes[0].MotionState.Should().Be("Idle");
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
            CreateAlarmViewModel(),
            CreateDashboardViewModel(),
            CreateEquipmentViewModel(),
            CreateMotionViewModel(),
            CreateTeachingViewModel(),
            CreateRecipeViewModel(),
            CreateInspectionViewModel(),
            CreateOfflineDebugViewModel(),
            new ReportsViewModel(),
            new SettingsViewModel());

        var motionItem = shell.NavigationItems.Single(item => item.Key == "Motion");
        motionItem.NavigateCommand.Execute(null);

        shell.CurrentViewModel.Should().BeOfType<MotionViewModel>();
        shell.CurrentScreenTitle.Should().Be("Motion");
        motionItem.IsSelected.Should().BeTrue();
        shell.NavigationItems.Should().Contain(item => item.Key == "Alarm");
    }

    [Fact]
    public async Task Equipment_FaultInjectionAsync_Should_Update_Io_Fault_State_And_Record_Event()
    {
        var controller = new VirtualEquipmentController();
        var alarmRecorder = new FakeEquipmentAlarmRecorder();
        await controller.ConnectAsync(TimeSpan.FromSeconds(3), CancellationToken.None);
        var equipment = CreateEquipmentViewModel(controller, alarmRecorder);

        await equipment.RefreshAsync(CancellationToken.None);
        equipment.InjectEmergencyStopCommand.CanExecute(null).Should().BeTrue();
        equipment.FaultInjectionDisabledReason.Should().Contain("available");
        equipment.FaultSummaryText.Should().Be("Active faults: 0 / 6");
        equipment.IoSummaryText.Should().Contain("I/O forced: 0 /");

        await equipment.InjectEmergencyStopCommand.ExecuteAsync(null);

        equipment.ModeStatus.Should().Be("Alarm");
        equipment.SafetyStatus.Should().Contain("EStop On");
        equipment.IoBits.Should().Contain(bit => bit.Name == "DI_ESTOP_ON" && bit.Value && bit.IsForced);
        equipment.Faults.Should().Contain(fault => fault.Name == "EStop" && fault.IsActive);
        equipment.FaultSummaryText.Should().Be("Active faults: 1 / 6");
        equipment.IoSummaryText.Should().Contain("I/O forced: 1 /");
        equipment.Events.Should().Contain(systemEvent => systemEvent.EventType == "Fault Injection");
        alarmRecorder.Failures.Should().ContainSingle(failure => failure.ErrorCode.Code == "EQP-003");

        await equipment.ClearAllFaultsCommand.ExecuteAsync(null);

        equipment.ModeStatus.Should().Be("Manual");
        equipment.SafetyStatus.Should().Contain("EStop Off");
        equipment.Faults.Should().Contain(fault => fault.Name == "EStop" && !fault.IsActive);
        equipment.FaultSummaryText.Should().Be("Active faults: 0 / 6");
        equipment.IoSummaryText.Should().Contain("I/O forced: 0 /");
        equipment.IoTransitions.Should().NotBeEmpty();
        equipment.IoTransitionStatus.Should().Contain("I/O transitions:");
        equipment.IoTransitions.Should().Contain(transition =>
            transition.Name == "DI_ESTOP_ON" &&
            transition.PreviousState == "Off" &&
            transition.CurrentState == "On / Forced");
    }

    [Fact]
    public void Equipment_FaultInjectionCommands_Should_Surface_Disabled_Reason_When_Disconnected()
    {
        var equipment = CreateEquipmentViewModel();

        equipment.InjectEmergencyStopCommand.CanExecute(null).Should().BeFalse();
        equipment.ClearAllFaultsCommand.CanExecute(null).Should().BeFalse();
        equipment.FaultInjectionDisabledReason.Should().Be("Connect the simulator before injecting faults.");
        equipment.FaultSummaryText.Should().Be("Active faults: 0 / 6");
        equipment.IoSummaryText.Should().Be("I/O forced: 0 / 0");
    }

    [Fact]
    public async Task Alarm_RefreshAndAcknowledgeAsync_Should_Load_State_And_Save_Recovery_Memo()
    {
        var alarm = new EquipmentAlarm(
            Guid.NewGuid(),
            "MOT-003",
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Motion,
            "Motion command timed out.",
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            correlationId: "corr-001");
        var useCase = new FakeAlarmCenterUseCase(alarm);
        var viewModel = CreateAlarmViewModel(useCase);

        await viewModel.RefreshAsync(CancellationToken.None);
        viewModel.AcknowledgeCommand.CanExecute(null).Should().BeTrue();
        viewModel.IsActionMemoEditable.Should().BeTrue();
        viewModel.AcknowledgeDisabledReason.Should().Contain("Store the recovery memo");
        viewModel.SelectedAlarm!.RecoveryHint.Should().Contain("soft limit");
        viewModel.RecoveryBoundaryItems.Should().Contain(item =>
            item.Boundary == "Operator acknowledgement" &&
            item.State == "Available");
        viewModel.RecoveryBoundaryItems.Should().Contain(item =>
            item.Boundary == "Hardware reset" &&
            item.State == "Not connected");
        viewModel.RecoveryBoundaryItems.Should().Contain(item =>
            item.Boundary == "PLC/vendor alarm source" &&
            item.State == "Not validated");

        viewModel.ActionMemoText = "Checked soft limit and reset axis.";
        await viewModel.AcknowledgeSelectedAsync(CancellationToken.None);

        viewModel.Alarms.Should().ContainSingle();
        viewModel.ActiveCount.Should().Be(0);
        viewModel.AcknowledgedCount.Should().Be(1);
        viewModel.SelectedAlarm!.StateText.Should().Be("Acknowledged");
        viewModel.SelectedAlarm.ActionMemo.Should().Be("Checked soft limit and reset axis.");
        viewModel.SelectedAlarm.RecoveryHint.Should().Contain("motion timeout");
        viewModel.StatusText.Should().Contain("acknowledged");
        viewModel.AcknowledgeCommand.CanExecute(null).Should().BeFalse();
        viewModel.IsActionMemoEditable.Should().BeFalse();
        viewModel.AcknowledgeDisabledReason.Should().Contain("already acknowledged");
        viewModel.HasAlert.Should().BeFalse();
        viewModel.AlertMessage.Should().BeNull();
        useCase.Acknowledged.Should().ContainSingle(item => item.AlarmId == alarm.Id);
    }

    [Fact]
    public async Task Alarm_RefreshAsync_Should_Surface_Acknowledge_Disabled_Reason_When_Empty()
    {
        var viewModel = CreateAlarmViewModel(new FakeAlarmCenterUseCase());

        await viewModel.RefreshAsync(CancellationToken.None);

        viewModel.SelectedAlarm.Should().BeNull();
        viewModel.AcknowledgeCommand.CanExecute(null).Should().BeFalse();
        viewModel.IsActionMemoEditable.Should().BeFalse();
        viewModel.AcknowledgeDisabledReason.Should().Contain("Select an alarm");
        viewModel.StatusText.Should().Be("No alarm records");
    }

    [Fact]
    public async Task Alarm_Filters_Should_Triage_By_Active_Severity_And_Area()
    {
        var activeCriticalMotion = new EquipmentAlarm(
            Guid.NewGuid(),
            "MOT-003",
            EquipmentAlarmSeverity.Critical,
            EquipmentArea.Motion,
            "Motion timeout.",
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        var acknowledgedCriticalMotion = new EquipmentAlarm(
            Guid.NewGuid(),
            "MOT-004",
            EquipmentAlarmSeverity.Critical,
            EquipmentArea.Motion,
            "Motion warning acknowledged.",
            new DateTimeOffset(2026, 6, 1, 12, 1, 0, TimeSpan.Zero),
            acknowledgedAt: new DateTimeOffset(2026, 6, 1, 12, 2, 0, TimeSpan.Zero));
        var activeWarningCamera = new EquipmentAlarm(
            Guid.NewGuid(),
            "CAM-001",
            EquipmentAlarmSeverity.Warning,
            EquipmentArea.Camera,
            "Camera not ready.",
            new DateTimeOffset(2026, 6, 1, 12, 3, 0, TimeSpan.Zero));
        var activeErrorInspection = new EquipmentAlarm(
            Guid.NewGuid(),
            "INS-001",
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Inspection,
            "Inspection failed.",
            new DateTimeOffset(2026, 6, 1, 12, 4, 0, TimeSpan.Zero));
        var viewModel = CreateAlarmViewModel(new FakeAlarmCenterUseCase(
            activeCriticalMotion,
            acknowledgedCriticalMotion,
            activeWarningCamera,
            activeErrorInspection));

        await viewModel.RefreshAsync(CancellationToken.None);
        viewModel.TotalAlarmCount.Should().Be(4);
        viewModel.Alarms.Should().HaveCount(4);
        viewModel.FilterSummaryText.Should().Contain("4 of 4 visible");

        viewModel.ShowActiveOnly = true;
        viewModel.Alarms.Should().HaveCount(3);
        viewModel.AcknowledgedCount.Should().Be(0);

        viewModel.SelectedSeverityFilter = nameof(EquipmentAlarmSeverity.Critical);
        viewModel.SelectedAreaFilter = nameof(EquipmentArea.Motion);

        viewModel.Alarms.Should().ContainSingle();
        viewModel.SelectedAlarm.Should().NotBeNull();
        viewModel.SelectedAlarm!.Id.Should().Be(activeCriticalMotion.Id);
        viewModel.FilterSummaryText.Should().Contain("Active only");
        viewModel.FilterSummaryText.Should().Contain("Severity Critical");
        viewModel.FilterSummaryText.Should().Contain("Area Motion");

        viewModel.SelectedSeverityFilter = "All";
        viewModel.SelectedAreaFilter = nameof(EquipmentArea.Camera);

        viewModel.Alarms.Should().ContainSingle();
        viewModel.SelectedAlarm!.Id.Should().Be(activeWarningCamera.Id);
    }

    [Fact]
    public async Task AppServiceConfiguration_Should_Save_Recipe_Through_Registered_Library()
    {
        var root = Path.Combine(Path.GetTempPath(), "VisionCellAppTests", Guid.NewGuid().ToString("N"));
        try
        {
            var services = new ServiceCollection();
            services.AddVisionCellAppServices(
                Path.Combine(root, "visioncell.db"),
                Path.Combine(root, "recipes"));
            using var provider = services.BuildServiceProvider();

            var useCase = provider.GetRequiredService<IRecipeLibraryUseCase>();
            var result = await useCase.SaveAsync(
                new RecipeLibrarySaveRequest(CreateAppCompositionRecipe()),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Entry.Should().NotBeNull();
            File.Exists(result.Entry!.DocumentPath).Should().BeTrue();

            var index = provider.GetRequiredService<IRecipeIndexRepository>();
            var indexed = await index.FindAsync("APP-COMPOSITION-RCP", "1.0.0", CancellationToken.None);
            indexed.Should().NotBeNull();
            indexed!.Checksum.Should().Be(result.Entry.Checksum);
            indexed.DocumentPath.Should().Be(result.Entry.DocumentPath);

            var activeContext = provider.GetRequiredService<IActiveRecipeContext>();
            var notSelected = await activeContext.GetActiveAsync(CancellationToken.None);
            notSelected.Status.Should().Be(ActiveRecipeContextStatus.NotSelected);

            var activated = await index.SetActiveAsync("APP-COMPOSITION-RCP", "1.0.0", CancellationToken.None);
            var active = await activeContext.GetActiveAsync(CancellationToken.None);
            activated.Should().BeTrue();
            active.IsSuccess.Should().BeTrue();
            active.RecipeId.Should().Be("APP-COMPOSITION-RCP");
            active.Version.Should().Be("1.0.0");

            provider.GetRequiredService<IVisionInspectionEngine>()
                .Should()
                .BeOfType<Deterministic2DInspectionEngine>();
            provider.GetRequiredService<IHeightMapInspectionEngine>()
                .Should()
                .BeOfType<DeterministicHeightMapInspectionEngine>();
            provider.GetRequiredService<SyntheticHeightMapFactory>()
                .Should()
                .NotBeNull();
            provider.GetRequiredService<IMotionPanelUseCase>()
                .Should()
                .BeOfType<MotionPanelUseCase>();
            provider.GetRequiredService<IEquipmentFaultInjectionUseCase>()
                .Should()
                .BeOfType<EquipmentFaultInjectionUseCase>();
            provider.GetRequiredService<IEquipmentFaultInjector>()
                .Should()
                .BeOfType<VirtualEquipmentController>();
            provider.GetRequiredService<IAlarmCenterUseCase>()
                .Should()
                .BeOfType<AlarmCenterUseCase>();
            provider.GetRequiredService<IEquipmentAlarmRepository>()
                .Should()
                .BeOfType<SqliteEquipmentAlarmRepository>();
            provider.GetRequiredService<IEquipmentAlarmRecorder>()
                .Should()
                .BeOfType<EquipmentAlarmRecorder>();
            provider.GetRequiredService<IEquipmentIoTransitionRepository>()
                .Should()
                .BeOfType<SqliteEquipmentIoTransitionRepository>();
            provider.GetRequiredService<IInspectionResultRepository>()
                .Should()
                .BeOfType<SqliteInspectionResultRepository>();
            provider.GetRequiredService<IInspectionReinspectComparisonRepository>()
                .Should()
                .BeOfType<SqliteInspectionReinspectComparisonRepository>();
            provider.GetRequiredService<IInspectionReinspectComparisonReader>()
                .Should()
                .BeOfType<SqliteInspectionReinspectComparisonRepository>();
            provider.GetRequiredService<IInspectionReinspectRecipePolicyUseCase>()
                .Should()
                .BeOfType<InspectionReinspectRecipePolicyUseCase>();
            provider.GetRequiredService<IInspectionReinspectSourceImageReadinessUseCase>()
                .Should()
                .BeOfType<InspectionReinspectSourceImageReadinessUseCase>();
            provider.GetRequiredService<IInspectionArtifactWriter>()
                .Should()
                .BeOfType<FileSystemInspectionArtifactWriter>();
            provider.GetRequiredService<IInspectionArtifactReader>()
                .Should()
                .BeOfType<FileSystemInspectionArtifactWriter>();
            provider.GetRequiredService<IArtifactViewerService>()
                .Should()
                .BeOfType<ShellArtifactViewerService>();
            provider.GetRequiredService<EquipmentRuntimeProfile>().Mode.Should().Be(EquipmentRuntimeMode.Virtual);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void AppServiceConfiguration_Should_Reject_Real_Hardware_Profile_Until_Validated()
    {
        var services = new ServiceCollection();
        var profile = new EquipmentRuntimeProfile(
            EquipmentRuntimeMode.RealHardware,
            "RealEquipmentController",
            "Bench profile pending hardware validation.");

        var act = () => services.AddVisionCellAppServices(
            Path.Combine(Path.GetTempPath(), "visioncell.db"),
            Path.Combine(Path.GetTempPath(), "recipes"),
            equipmentRuntimeProfile: profile);

        act.Should()
            .Throw<NotSupportedException>()
            .WithMessage("*not implemented or validated*Missing evidence:*RealEquipmentController implementation*PLC I/O adapter bench validation*HARDWARE_INTEGRATION_PLAN*");
    }

    [Fact]
    public void RealHardwareReadinessGate_Should_List_Required_Evidence()
    {
        var blocked = RealHardwareReadinessGate.Evaluate(RealHardwareReadinessEvidence.Unvalidated);

        blocked.CanEnableRealHardware.Should().BeFalse();
        blocked.MissingEvidence.Should().Contain("RealEquipmentController implementation");
        blocked.MissingEvidence.Should().Contain("motion adapter bench validation");
        blocked.MissingEvidence.Should().Contain("camera adapter bench validation");
        blocked.MissingEvidence.Should().Contain("PLC I/O adapter bench validation");
        blocked.MissingEvidence.Should().Contain("safety reset validation");

        var complete = RealHardwareReadinessGate.Evaluate(new RealHardwareReadinessEvidence(
            RealEquipmentControllerImplemented: true,
            MotionAdapterBenchValidated: true,
            CameraAdapterBenchValidated: true,
            PlcIoAdapterBenchValidated: true,
            SafetyResetValidated: true,
            HardwareIntegrationPlanReviewed: true));

        complete.CanEnableRealHardware.Should().BeTrue();
        complete.FormatMissingEvidence().Should().Be("none");
    }

    [Fact]
    public void SettingsViewModel_Should_Surface_ReadOnly_Runtime_And_RealHardware_Gate()
    {
        var settings = new SettingsViewModel();

        settings.RuntimeStatusText.Should().Contain("read-only");
        settings.RuntimeScopeItems.Should().Contain(item =>
            item.Name == "Equipment Mode" &&
            item.Value == "VirtualEquipmentController");
        settings.RuntimeScopeItems.Should().Contain(item =>
            item.Name == "Validation Scope" &&
            item.Value == "docs/VALIDATION_SCOPE.md");
        settings.ReadinessSummary.Should().Contain("RealHardware profile remains blocked");
        settings.ReadinessGateItems.Should().HaveCount(6);
        settings.ReadinessGateItems.Should().Contain(item => item.Name == "RealEquipmentController implementation");
        settings.ReadinessGateItems.Should().Contain(item => item.Name == "PLC I/O adapter bench validation");
        settings.ReadinessGateItems.Should().OnlyContain(item => item.Value == "Missing");
        settings.AdapterBoundaryItems.Should().HaveCount(3);
        settings.AdapterBoundaryItems.Should().Contain(item =>
            item.Name == "Motion Controller" &&
            item.Contract == "IMotionControllerAdapter -> MotionControllerAdapter" &&
            item.CurrentScope.Contains("VirtualEquipmentController") &&
            item.Readiness == "Missing: motion adapter bench validation");
        settings.AdapterBoundaryItems.Should().Contain(item =>
            item.Name == "Camera" &&
            item.Contract == "ICameraAdapter -> CameraAdapter" &&
            item.CurrentScope == "VirtualCameraDevice" &&
            item.Readiness == "Missing: camera adapter bench validation");
        settings.AdapterBoundaryItems.Should().Contain(item =>
            item.Name == "PLC I/O" &&
            item.Contract == "IPlcIoAdapter -> PlcIoAdapter" &&
            item.Readiness == "Missing: PLC I/O adapter bench validation");
    }

    [Fact]
    public void AppServiceConfiguration_Should_Register_Virtual_Equipment_Runtime_Profile_By_Default()
    {
        var services = new ServiceCollection();

        services.AddVisionCellAppServices(
            Path.Combine(Path.GetTempPath(), "visioncell.db"),
            Path.Combine(Path.GetTempPath(), "recipes"));

        var descriptor = services.Single(service => service.ServiceType == typeof(EquipmentRuntimeProfile));
        var profile = descriptor.ImplementationInstance.Should().BeOfType<EquipmentRuntimeProfile>().Subject;
        profile.Mode.Should().Be(EquipmentRuntimeMode.Virtual);
        profile.ProfileName.Should().Be("VirtualEquipmentController");
        profile.ValidationScope.Should().Contain("Simulator-only");
    }

    [Fact]
    public void Dashboard_Initial_State_Should_Disable_Dangerous_Commands()
    {
        var dashboard = CreateDashboardViewModel();

        dashboard.GetCommandAvailability(CommandKind.Connect).IsEnabled.Should().BeTrue();
        dashboard.GetCommandAvailability(CommandKind.Home).IsEnabled.Should().BeFalse();
        dashboard.GetCommandAvailability(CommandKind.Jog).IsEnabled.Should().BeFalse();
        dashboard.GetCommandAvailability(CommandKind.MoveAbsolute).IsEnabled.Should().BeFalse();
        dashboard.GetCommandAvailability(CommandKind.EnterAutoMode).IsEnabled.Should().BeFalse();
        dashboard.GetCommandAvailability(CommandKind.RunInspection).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Dashboard_EnterAutoModeAsync_Should_Execute_Mode_Command_And_Refresh_Snapshot()
    {
        var controller = new FakeEquipmentController(CreateSnapshot(connected: true, servoOn: true, homed: true));
        controller.ExecuteHandler = (command, _, _, _) =>
        {
            if (command == CommandKind.EnterAutoMode)
            {
                controller.Snapshot = controller.Snapshot with { Mode = MachineMode.Auto };
            }

            return Task.FromResult(MachineCommandResult.Success("Machine mode changed to Auto.", TimeSpan.FromMilliseconds(12), CorrelationId.New()));
        };
        var dashboard = CreateDashboardViewModel(controller);

        await dashboard.RefreshAsync(CancellationToken.None);
        dashboard.EnterAutoModeCommand.CanExecute(null).Should().BeTrue();

        await dashboard.EnterAutoModeAsync(CancellationToken.None);

        controller.LastCommand.Should().Be(CommandKind.EnterAutoMode);
        dashboard.ModeStatus.Should().Be("Auto");
        dashboard.Events.Should().Contain(systemEvent => systemEvent.EventType == "Enter Auto");
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
    public async Task OfflineDebug_RefreshResultsAsync_Should_Load_Recent_Result_State()
    {
        var createdAt = new DateTimeOffset(2026, 6, 1, 12, 30, 0, TimeSpan.Zero);
        var artifactReader = new FakeInspectionArtifactReader();
        var offlineDebug = CreateOfflineDebugViewModel(new FakeInspectionResultReader(
            CreateInspectionResultRecord(
                Judgment.Fail,
                createdAt,
                new[]
                {
                    new InspectionDefectRecord("Missing", 0.91, "ROI-01", 10, 20, 30, 40, "Missing area.")
                })),
            artifactReader);

        await offlineDebug.RefreshResultsAsync(CancellationToken.None);

        artifactReader.MetadataRequests.Should().HaveCount(2);
        offlineDebug.HasResults.Should().BeTrue();
        offlineDebug.Results.Should().ContainSingle();
        offlineDebug.SelectedResult.Should().NotBeNull();
        offlineDebug.SelectedResult!.Judgment.Should().Be(Judgment.Fail);
        offlineDebug.SelectedResult.DefectCount.Should().Be(1);
        offlineDebug.SelectedResult.OverlayImagePath.Should().Contain(".overlay.bmp");
        offlineDebug.SelectedResult.HeightMapPath.Should().Contain(".height.bmp");
        offlineDebug.SelectedResult.OverlayArtifactStatus.Should().Contain("Available");
        offlineDebug.SelectedResult.HeightMapArtifactStatus.Should().Contain("Available");
        offlineDebug.SelectedResult.ArtifactStatusSummary.Should().Contain("Overlay");
        offlineDebug.SelectedResult.ArtifactStatusSummary.Should().Contain("Height Map");
        offlineDebug.SelectedResult.Defects.Should().ContainSingle(defect => defect.Type == "Missing" && defect.RoiId == "ROI-01");
        offlineDebug.SelectedResult.OverlayItems.Should().ContainSingle();
        var overlayItem = offlineDebug.SelectedResult.OverlayItems[0];
        overlayItem.X.Should().Be(10);
        overlayItem.Y.Should().Be(20);
        overlayItem.Width.Should().Be(30);
        overlayItem.Height.Should().Be(40);
        overlayItem.DisplayText.Should().Contain("ROI-01");
        overlayItem.DisplayText.Should().Contain("0.910");
        offlineDebug.FailCount.Should().Be(1);
        offlineDebug.PassCount.Should().Be(0);
        offlineDebug.DefectCount.Should().Be(1);
        offlineDebug.StatusText.Should().Be("1 inspection result records loaded");
        offlineDebug.LastRefreshText.Should().NotBe("-");
    }

    [Fact]
    public async Task OfflineDebug_RefreshResultsAsync_Should_Load_Reinspect_Comparison_History()
    {
        var comparison = CreateReinspectComparison("offline-reinspect-history-001");
        var offlineDebug = CreateOfflineDebugViewModel(
            new FakeInspectionResultReader(),
            reinspectComparisonReader: new FakeInspectionReinspectComparisonReader(comparison));

        await offlineDebug.RefreshResultsAsync(CancellationToken.None);

        offlineDebug.HasReinspectComparisons.Should().BeTrue();
        offlineDebug.ReinspectComparisons.Should().ContainSingle();
        offlineDebug.ReinspectComparisons[0].ReplayCorrelationId.Should().Be(comparison.ReplayCorrelationId);
        offlineDebug.ReinspectComparisons[0].PersistenceStatus.Should().Contain("Persisted");
        offlineDebug.ReinspectComparisons[0].JudgmentTransition.Should().Be("Pass -> Pass");
    }

    [Fact]
    public async Task OfflineDebug_LoadSelectedArtifactsAsync_Should_Load_Previews_And_Prepare_Reinspect()
    {
        var artifactReader = new FakeInspectionArtifactReader();
        var result = CreateInspectionResultRecord(
            Judgment.Pass,
            new DateTimeOffset(2026, 6, 1, 12, 30, 0, TimeSpan.Zero));
        var offlineDebug = CreateOfflineDebugViewModel(
            new FakeInspectionResultReader(result),
            artifactReader);

        await offlineDebug.RefreshResultsAsync(CancellationToken.None);
        await offlineDebug.LoadSelectedArtifactsAsync(CancellationToken.None);
        await offlineDebug.PrepareReinspectAsync(CancellationToken.None);

        artifactReader.PreviewRequests.Should().HaveCount(2);
        offlineDebug.OverlayPreviewImageSource.Should().NotBeNull();
        offlineDebug.OverlayImagePixelWidth.Should().Be(2);
        offlineDebug.OverlayImagePixelHeight.Should().Be(2);
        offlineDebug.HeightMapPreviewImageSource.Should().NotBeNull();
        offlineDebug.ArtifactPreviewStatusText.Should().Contain("Artifact preview available");
        offlineDebug.PreparedReinspect.Should().NotBeNull();
        offlineDebug.PreparedReinspect!.SourceResultId.Should().Be(result.Id);
        offlineDebug.PreparedReinspect.LotId.Should().Be(result.LotId);
        offlineDebug.PreparedReinspect.RecipeId.Should().Be(result.RecipeId);
        offlineDebug.PreparedReinspect.RecipeVersion.Should().Be(result.RecipeVersion);
        offlineDebug.PreparedReinspect.PreviousJudgment.Should().Be(result.Judgment.ToString());
        offlineDebug.PreparedReinspect.PreviousCycleTime.Should().Be(result.CycleTime);
        offlineDebug.PreparedReinspect.PreviousDefectCount.Should().Be(result.Defects.Count);
        offlineDebug.PreparedReinspect.SourceCorrelationId.Should().Be(result.CorrelationId);
        offlineDebug.PreparedReinspect.SourceImagePath.Should().Be(result.SourceImagePath);
        offlineDebug.PreparedReinspect.OverlayImagePath.Should().Be(result.OverlayImagePath);
        offlineDebug.PreparedReinspect.HeightMapPath.Should().Be(result.HeightMapPath);
        offlineDebug.PreparedReinspect.CanRunInspection.Should().BeTrue();
        offlineDebug.PreparedReinspect.DisabledReason.Should().Contain("metadata comparison");
        offlineDebug.ReinspectStatusText.Should().Contain(result.LotId);
        offlineDebug.PreparedReinspectSummary.Should().Contain(result.LotId);
        offlineDebug.PreparedReinspectArtifactSummary.Should().Contain(result.OverlayImagePath);
        offlineDebug.ReinspectRunDisabledReason.Should().Contain("metadata comparison");
        offlineDebug.ReinspectReadinessItems.Should().Contain(item =>
            item.Step == "Metadata comparison" &&
            item.State == "Available");
        offlineDebug.ReinspectReadinessItems.Should().Contain(item =>
            item.Step == "Source-image replay" &&
            item.State == "Boundary available");
        offlineDebug.ReinspectReadinessItems.Should().Contain(item =>
            item.Step == "Recipe policy" &&
            item.State == "Available");
        offlineDebug.ReinspectReadinessItems.Should().Contain(item =>
            item.Step == "Metadata history persistence" &&
            item.State == "Available");
        offlineDebug.ReinspectReadinessItems.Should().Contain(item =>
            item.Step == "Real sequence execution" &&
            item.State == "Not validated");
        offlineDebug.RunReinspectCommand.CanExecute(null).Should().BeTrue();
        offlineDebug.ReinspectRecipePolicy.Should().NotBeNull();
        offlineDebug.ReinspectRecipePolicy!.Status.Should().Be(InspectionReinspectRecipePolicyStatus.ActiveMatchesHistorical);
        offlineDebug.ReinspectRecipePolicySummary.Should().Contain("Current and historical Recipe match");
        offlineDebug.ReinspectRecipePolicyDetail.Should().Contain("Metadata comparison");
        offlineDebug.ReinspectSourceImageReadiness.Should().NotBeNull();
        offlineDebug.ReinspectSourceImageReadiness!.Status.Should().Be(InspectionReinspectSourceImageReadinessStatus.SourceArtifactArchived);
        offlineDebug.ReinspectSourceImageReadinessSummary.Should().Contain("Source artifact archived");
        offlineDebug.ReinspectSourceImageReadinessDetail.Should().Contain(result.SourceImagePath);
        await offlineDebug.RunReinspectAsync(CancellationToken.None);
        offlineDebug.ReinspectComparison.Should().NotBeNull();
        offlineDebug.ReinspectComparison!.Status.Should().Be(InspectionReinspectComparisonStatus.Matched);
        offlineDebug.ReinspectComparison.PreviousJudgment.Should().Be(result.Judgment.ToString());
        offlineDebug.ReinspectComparison.ReplayedJudgment.Should().Be(result.Judgment.ToString());
        offlineDebug.ReinspectComparisonSummary.Should().Contain("Matched");
        offlineDebug.ReinspectComparisonDetail.Should().Contain("Not persisted");
        offlineDebug.HasReinspectComparisons.Should().BeTrue();
        offlineDebug.ReinspectComparisons.Should().ContainSingle();
        offlineDebug.ReinspectComparisons[0].ReplayCorrelationId.Should().Be(offlineDebug.ReinspectComparison.ReplayCorrelationId);
        offlineDebug.ReinspectComparisons[0].PersistenceStatus.Should().Contain("Not persisted");
        offlineDebug.HasAlert.Should().BeFalse();
        offlineDebug.AlertMessage.Should().BeNull();

        offlineDebug.SelectedResult = null;

        offlineDebug.PreparedReinspect.Should().BeNull();
        offlineDebug.ReinspectComparison.Should().BeNull();
        offlineDebug.ReinspectRecipePolicy.Should().BeNull();
        offlineDebug.ReinspectSourceImageReadiness.Should().BeNull();
        offlineDebug.OverlayPreviewImageSource.Should().BeNull();
        offlineDebug.OverlayImagePixelWidth.Should().Be(0);
        offlineDebug.OverlayImagePixelHeight.Should().Be(0);
        offlineDebug.HeightMapPreviewImageSource.Should().BeNull();
        offlineDebug.ReinspectStatusText.Should().Contain("Select an inspection result");
        offlineDebug.ReinspectRunDisabledReason.Should().Contain("Select an inspection result");
    }

    [Fact]
    public async Task OfflineDebug_RefreshResultsAsync_Should_Surface_Empty_State()
    {
        var offlineDebug = CreateOfflineDebugViewModel(new FakeInspectionResultReader());

        await offlineDebug.RefreshResultsAsync(CancellationToken.None);

        offlineDebug.HasResults.Should().BeFalse();
        offlineDebug.Results.Should().BeEmpty();
        offlineDebug.SelectedResult.Should().BeNull();
        offlineDebug.StatusText.Should().Be("No inspection result records");
    }

    [Fact]
    public async Task OfflineDebug_RefreshResultsAsync_Should_Surface_Reader_Failure()
    {
        var offlineDebug = CreateOfflineDebugViewModel(new FakeInspectionResultReader
        {
            ListHandler = (_, _) => throw new InvalidOperationException("result store unavailable")
        });

        await offlineDebug.RefreshResultsAsync(CancellationToken.None);

        offlineDebug.StatusText.Should().Contain("result store unavailable");
        offlineDebug.HasAlert.Should().BeTrue();
        offlineDebug.AlertMessage.Should().Contain("result store unavailable");
        offlineDebug.HasResults.Should().BeFalse();
        offlineDebug.Results.Should().BeEmpty();
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
    public async Task Motion_RefreshSnapshotAsync_Should_Surface_Timeout_State()
    {
        var controller = new FakeEquipmentController(CreateSnapshot())
        {
            SnapshotHandler = (_, _) => throw new OperationCanceledException("snapshot timeout")
        };
        var motion = CreateMotionViewModel(equipmentController: controller);

        await motion.RefreshSnapshotAsync(CancellationToken.None);

        motion.CommandStatus.Should().Be("Snapshot refresh timed out");
        motion.Axes.Should().BeEmpty();
    }

    [Fact]
    public async Task Motion_ExecuteServoOnAsync_Should_Not_Run_When_Snapshot_Times_Out()
    {
        var useCase = new FakeMotionCommandUseCase();
        var controller = new FakeEquipmentController(CreateSnapshot(connected: true, servoOn: false))
        {
            SnapshotHandler = (_, _) => throw new OperationCanceledException("snapshot timeout")
        };
        var motion = CreateMotionViewModel(commandUseCase: useCase, equipmentController: controller);

        await motion.ExecuteServoOnAsync(CancellationToken.None);

        useCase.Requests.Should().BeEmpty();
        motion.CommandStatus.Should().Be("Servo On timed out");
        motion.RecentCommands.Should().BeEmpty();
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
        var activeRecipe = new FakeActiveRecipeContext(CreateActiveRecipeContextResult("RCP-ACTIVE", "2.0.0"));
        var teaching = CreateTeachingViewModel(useCase, activeRecipeContext: activeRecipe);
        teaching.NameText = "Safe Park";
        teaching.SelectedRole = TeachingRole.Safe;
        teaching.MemoText = "verified";

        await teaching.SaveCurrentPositionAsync(CancellationToken.None);

        useCase.SaveRequests.Should().ContainSingle();
        useCase.SaveRequests[0].Name.Should().Be("Safe Park");
        useCase.SaveRequests[0].Role.Should().Be(TeachingRole.Safe);
        useCase.SaveRequests[0].RecipeId.Should().Be("RCP-ACTIVE");
        teaching.ActiveRecipeIdText.Should().Be("RCP-ACTIVE");
        teaching.ActiveRecipeContextText.Should().Be("RCP-ACTIVE v2.0.0");
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
        var teaching = CreateTeachingViewModel(useCase);

        await teaching.RefreshAsync(CancellationToken.None);
        teaching.SelectedPoint = teaching.Points.Single();
        await teaching.GoToSelectedAsync(CancellationToken.None);

        useCase.GoToRequests.Should().ContainSingle();
        useCase.GoToRequests[0].TeachingPointId.Should().Be(point.Id);
        useCase.GoToRequests[0].SnapshotTimeout.Should().Be(TimeSpan.FromMilliseconds(500));
        useCase.GoToRequests[0].CommandTimeout.Should().Be(TimeSpan.FromSeconds(3));
        teaching.StatusText.Should().Contain("completed");
    }

    [Fact]
    public async Task Teaching_UpdateSelectedAsync_Should_Send_Selected_Point_Edit()
    {
        var point = TeachingPointFactory.Create(
            "Camera",
            TeachingRole.Camera,
            new Position4D(1.0, 2.0, 3.0, 4.0),
            PositionTolerance.Default,
            "before").Point!;
        var useCase = new FakeTeachingPointUseCase(point);
        var activeRecipe = new FakeActiveRecipeContext(CreateActiveRecipeContextResult("RCP-EDIT-ACTIVE", "1.2.0"));
        var teaching = CreateTeachingViewModel(useCase, activeRecipeContext: activeRecipe);

        await teaching.RefreshAsync(CancellationToken.None);
        teaching.NameText = "Camera Revised";
        teaching.SelectedRole = TeachingRole.Review;
        teaching.MemoText = "after";
        teaching.ToleranceXText = "0.020";
        await teaching.UpdateSelectedAsync(CancellationToken.None);

        useCase.UpdateRequests.Should().ContainSingle();
        useCase.UpdateRequests[0].TeachingPointId.Should().Be(point.Id);
        useCase.UpdateRequests[0].Name.Should().Be("Camera Revised");
        useCase.UpdateRequests[0].Role.Should().Be(TeachingRole.Review);
        useCase.UpdateRequests[0].Position.Should().Be(point.Position);
        useCase.UpdateRequests[0].Tolerance.X.Should().Be(0.02);
        useCase.UpdateRequests[0].RecipeId.Should().Be("RCP-EDIT-ACTIVE");
        teaching.Points.Should().ContainSingle(item => item.Name == "Camera Revised");
        teaching.StatusText.Should().Contain("teaching points loaded");
    }

    [Fact]
    public async Task Teaching_DeleteSelectedAsync_Should_Delete_Selected_Point_And_Refresh()
    {
        var point = TeachingPointFactory.Create(
            "Park",
            TeachingRole.Park,
            new Position4D(1.0, 2.0, 3.0, 4.0),
            PositionTolerance.Default).Point!;
        var useCase = new FakeTeachingPointUseCase(point);
        var confirmation = new FakeUserConfirmationService(true);
        var activeRecipe = new FakeActiveRecipeContext(CreateActiveRecipeContextResult("RCP-DELETE-ACTIVE", "3.0.0"));
        var teaching = CreateTeachingViewModel(
            useCase,
            confirmationService: confirmation,
            activeRecipeContext: activeRecipe);

        await teaching.RefreshAsync(CancellationToken.None);
        await teaching.DeleteSelectedAsync(CancellationToken.None);

        confirmation.Prompts.Should().ContainSingle();
        confirmation.Prompts[0].Message.Should().Contain("Park");
        useCase.DeleteRequests.Should().ContainSingle();
        useCase.DeleteRequests[0].TeachingPointId.Should().Be(point.Id);
        useCase.DeleteRequests[0].RecipeId.Should().Be("RCP-DELETE-ACTIVE");
        teaching.Points.Should().BeEmpty();
        teaching.SelectedPoint.Should().BeNull();
        teaching.StatusText.Should().Be("No teaching points saved");
    }

    [Fact]
    public async Task Teaching_SaveCurrentPositionAsync_Should_Fallback_To_Manual_Recipe_When_No_Active_Context()
    {
        var useCase = new FakeTeachingPointUseCase();
        var teaching = CreateTeachingViewModel(useCase);
        teaching.ActiveRecipeIdText = " RCP-MANUAL ";

        await teaching.SaveCurrentPositionAsync(CancellationToken.None);

        useCase.SaveRequests.Should().ContainSingle();
        useCase.SaveRequests[0].RecipeId.Should().Be("RCP-MANUAL");
        teaching.ActiveRecipeContextText.Should().Contain("No active recipe");
    }

    [Fact]
    public async Task Teaching_DeleteSelectedAsync_Should_Not_Delete_When_Confirmation_Is_Cancelled()
    {
        var point = TeachingPointFactory.Create(
            "Park",
            TeachingRole.Park,
            new Position4D(1.0, 2.0, 3.0, 4.0),
            PositionTolerance.Default).Point!;
        var useCase = new FakeTeachingPointUseCase(point);
        var confirmation = new FakeUserConfirmationService(false);
        var teaching = CreateTeachingViewModel(useCase, confirmationService: confirmation);

        await teaching.RefreshAsync(CancellationToken.None);
        await teaching.DeleteSelectedAsync(CancellationToken.None);

        confirmation.Prompts.Should().ContainSingle();
        useCase.DeleteRequests.Should().BeEmpty();
        teaching.Points.Should().ContainSingle();
        teaching.SelectedPoint.Should().NotBeNull();
        teaching.StatusText.Should().Contain("cancelled");
    }

    [Fact]
    public async Task Teaching_RefreshAsync_Should_Load_Selected_Point_History()
    {
        var point = TeachingPointFactory.Create(
            "Camera",
            TeachingRole.Camera,
            new Position4D(1.0, 2.0, 3.0, 4.0),
            PositionTolerance.Default).Point!;
        var historyEntry = TeachingHistoryEntry.Create(
            point.Id,
            null,
            TeachingHistoryAction.Updated,
            "{\"Name\":\"Camera\"}",
            "{\"Name\":\"Camera Revised\"}",
            () => new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero));
        var useCase = new FakeTeachingPointUseCase(point);
        useCase.HistoryEntries.Add(historyEntry);
        var teaching = CreateTeachingViewModel(
            useCase);

        await teaching.RefreshAsync(CancellationToken.None);

        useCase.ListHistoryRequests.Should().Contain(request => request.TeachingPointId == point.Id);
        teaching.HasSelectedPointHistory.Should().BeTrue();
        teaching.SelectedPointHistory.Should().ContainSingle();
        teaching.SelectedPointHistory[0].ActionText.Should().Be("Updated");
        teaching.SelectedPointHistory[0].BeforeText.Should().Contain("Camera");
        teaching.SelectedPointHistory[0].AfterText.Should().Contain("Camera Revised");
        teaching.HistoryStatus.Should().Be("1 teaching history records loaded");
    }

    [Fact]
    public async Task Teaching_RefreshSelectedPointHistoryAsync_Should_Surface_Load_Failure()
    {
        var point = TeachingPointFactory.Create(
            "Camera",
            TeachingRole.Camera,
            new Position4D(1.0, 2.0, 3.0, 4.0),
            PositionTolerance.Default).Point!;
        var useCase = new FakeTeachingPointUseCase(point)
        {
            ListHandler = (_, _, _) => throw new InvalidOperationException("history store unavailable")
        };
        var teaching = CreateTeachingViewModel(
            useCase);

        await teaching.RefreshAsync(CancellationToken.None);

        teaching.HasSelectedPointHistory.Should().BeFalse();
        teaching.SelectedPointHistory.Should().BeEmpty();
        teaching.HistoryStatus.Should().Contain("history store unavailable");
    }

    [Fact]
    public async Task Inspection_RunInspectionAsync_Should_Report_Ready_For_Valid_Active_Recipe()
    {
        var activeRecipe = CreateRecipeIndexEntry(
            "RCP-INSPECT",
            "1.0.0",
            "Inspection Recipe",
            isActive: true,
            isValid: true);
        var runUseCase = new FakeInspectionRunUseCase(CreateInspectionRunResult(
            InspectionRunStatus.Accepted,
            "Inspection sequence accepted for recipe 'RCP-INSPECT' v1.0.0.",
            activeRecipe,
            CameraGrabResult.Success(
                CreateCameraFrame("RCP-INSPECT", "1.0.0"),
                "Grabbed 16x12 Gray8 frame from Fake camera.",
                TimeSpan.FromMilliseconds(3),
                CorrelationId.New()),
            visionResult: new VisionInspectionResult(
                Judgment.Fail,
                new[] { new Defect("Missing", 0.92, 1, 2, 3, 4, "Missing area.") },
                "2D inspection Fail: 1 defect.",
                "RCP-INSPECT",
                "1.0.0",
                TimeSpan.FromMilliseconds(2),
                DateTimeOffset.UtcNow),
            heightMapResult: new VisionInspectionResult(
                Judgment.Fail,
                new[] { new Defect("Lift", 0.81, 5, 6, 7, 8, "Lift detected.") },
                "3D inspection Fail: 1 defect.",
                "RCP-INSPECT",
                "1.0.0",
                TimeSpan.FromMilliseconds(2),
                DateTimeOffset.UtcNow)));
        var inspection = CreateInspectionViewModel(inspectionRunUseCase: runUseCase);

        await inspection.RunInspectionAsync(CancellationToken.None);

        runUseCase.Requests.Should().ContainSingle();
        inspection.ActiveRecipeText.Should().Be("RCP-INSPECT v1.0.0");
        inspection.StatusText.Should().Contain("Inspection sequence accepted");
        inspection.StatusText.Should().Contain("RCP-INSPECT");
        inspection.SequenceSteps.Should().Contain(step => step.Name == "Start Sequence" && step.Status == "Success");
        inspection.SequenceSteps.Should().Contain(step => step.Name == "Judge" && step.Detail.Contains("Pass", StringComparison.Ordinal));
        inspection.LastGrabText.Should().Contain("16 x 12");
        inspection.LastGrabText.Should().Contain("Fake camera");
        inspection.LastGrabImageSource.Should().NotBeNull();
        inspection.LastGrabImagePixelWidth.Should().Be(16);
        inspection.LastGrabImagePixelHeight.Should().Be(12);
        inspection.LastGrabOverlayItems.Should().HaveCount(2);
        inspection.LastGrabOverlayItems.Should().Contain(item => item.Label == "2D Missing" && item.X == 1 && item.Y == 2);
        inspection.LastGrabOverlayItems.Should().Contain(item => item.Label == "3D Lift" && item.Width == 7 && item.Height == 8);
        inspection.LastRunCorrelationId.Should().NotBe("-");
        inspection.LastCheckText.Should().NotBe("-");
        inspection.HasAlert.Should().BeFalse();
        inspection.AlertMessage.Should().BeNull();
    }

    [Fact]
    public async Task Inspection_RunInspectionAsync_Should_Reject_When_Active_Recipe_Is_Not_Selected()
    {
        var runUseCase = new FakeInspectionRunUseCase(CreateInspectionRunResult(
            InspectionRunStatus.ActiveRecipeNotSelected,
            "No active recipe is selected."));
        var inspection = CreateInspectionViewModel(inspectionRunUseCase: runUseCase);

        await inspection.RunInspectionAsync(CancellationToken.None);

        inspection.ActiveRecipeText.Should().Be("-");
        inspection.StatusText.Should().Contain("Run inspection rejected");
        inspection.StatusText.Should().Contain("No active recipe");
        inspection.HasAlert.Should().BeTrue();
        inspection.AlertMessage.Should().Contain("Run inspection rejected");
    }

    [Fact]
    public async Task Inspection_RunInspectionAsync_Should_Reject_Invalid_Active_Recipe()
    {
        var runUseCase = new FakeInspectionRunUseCase(CreateInspectionRunResult(
            InspectionRunStatus.ActiveRecipeInvalid,
            "Active recipe 'RCP-BAD' v0.1.0 is invalid: Missing ROI"));
        var inspection = CreateInspectionViewModel(inspectionRunUseCase: runUseCase);

        await inspection.RunInspectionAsync(CancellationToken.None);

        inspection.ActiveRecipeText.Should().Be("-");
        inspection.PrecheckStatusText.Should().Contain("Missing ROI");
        inspection.StatusText.Should().Contain("Run inspection rejected");
    }

    [Fact]
    public async Task Inspection_RefreshActiveRecipeAsync_Should_Load_Precheck_Through_Run_UseCase()
    {
        var activeRecipe = CreateRecipeIndexEntry(
            "RCP-PRECHECK",
            "1.0.0",
            "Precheck Recipe",
            isActive: true,
            isValid: true);
        var runUseCase = new FakeInspectionRunUseCase
        {
            PrecheckResult = ActiveRecipeContextResult.Success(activeRecipe)
        };
        var inspection = CreateInspectionViewModel(runUseCase);

        await inspection.RefreshActiveRecipeAsync(CancellationToken.None);

        runUseCase.PrecheckRequests.Should().Be(1);
        inspection.ActiveRecipeText.Should().Be("RCP-PRECHECK v1.0.0");
        inspection.PrecheckStatusText.Should().Contain("RCP-PRECHECK");
        inspection.StatusText.Should().Be("Inspection precheck ready");
        inspection.LastCheckText.Should().NotBe("-");
        inspection.HasAlert.Should().BeFalse();
        inspection.AlertMessage.Should().BeNull();
    }

    [Fact]
    public async Task Inspection_RefreshActiveRecipeAsync_Should_Surface_RepositoryUnavailable()
    {
        var runUseCase = new FakeInspectionRunUseCase
        {
            PrecheckResult = ActiveRecipeContextResult.RepositoryUnavailable("recipe index unavailable")
        };
        var inspection = CreateInspectionViewModel(runUseCase);

        await inspection.RefreshActiveRecipeAsync(CancellationToken.None);

        runUseCase.PrecheckRequests.Should().Be(1);
        inspection.ActiveRecipeText.Should().Be("-");
        inspection.PrecheckStatusText.Should().Contain("recipe index unavailable");
        inspection.StatusText.Should().Contain("Inspection precheck blocked");
        inspection.HasAlert.Should().BeTrue();
        inspection.AlertMessage.Should().Contain("Inspection precheck blocked");
    }

    [Fact]
    public async Task Recipe_RefreshAsync_Should_Load_Index_State_And_Select_Active_Recipe()
    {
        var activeEntry = CreateRecipeIndexEntry(
            "PKG-MEMORY",
            "1.0.1",
            "Memory Module",
            isActive: true,
            isValid: true,
            updatedAt: new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero));
        var invalidEntry = CreateRecipeIndexEntry(
            "PKG-POWER",
            "0.9.0",
            "Power Module",
            isActive: false,
            isValid: false,
            validationSummary: "Missing inspection teaching point");
        var recipe = CreateRecipeViewModel(new FakeRecipeIndexRepository(activeEntry, invalidEntry));

        await recipe.RefreshAsync(CancellationToken.None);

        recipe.HasRecipes.Should().BeTrue();
        recipe.Recipes.Should().HaveCount(2);
        recipe.ValidRecipeCount.Should().Be(1);
        recipe.InvalidRecipeCount.Should().Be(1);
        recipe.ActiveRecipeCount.Should().Be(1);
        recipe.ActiveRecipeText.Should().Be("PKG-MEMORY v1.0.1");
        recipe.SelectedRecipe.Should().NotBeNull();
        recipe.SelectedRecipe!.RecipeId.Should().Be("PKG-MEMORY");
        recipe.SelectedRecipe.ValidationStateText.Should().Be("Valid");
        recipe.Recipes.Single(entry => entry.RecipeId == "PKG-POWER")
            .ValidationSummaryText.Should().Contain("Missing inspection");
        recipe.StatusText.Should().Be("2 recipe index records loaded");
        recipe.LastRefreshText.Should().NotBe("-");
        recipe.HasAlert.Should().BeFalse();
        recipe.AlertMessage.Should().BeNull();
    }

    [Fact]
    public async Task Recipe_RefreshAsync_Should_Surface_Empty_State()
    {
        var recipe = CreateRecipeViewModel(new FakeRecipeIndexRepository());

        await recipe.RefreshAsync(CancellationToken.None);

        recipe.HasRecipes.Should().BeFalse();
        recipe.Recipes.Should().BeEmpty();
        recipe.ValidRecipeCount.Should().Be(0);
        recipe.InvalidRecipeCount.Should().Be(0);
        recipe.ActiveRecipeText.Should().Be("-");
        recipe.SelectedRecipe.Should().BeNull();
        recipe.StatusText.Should().Be("No recipe index records");
    }

    [Fact]
    public async Task Recipe_RefreshAsync_Should_Surface_Load_Failure()
    {
        var repository = new FakeRecipeIndexRepository
        {
            ListHandler = (_, _) => throw new InvalidOperationException("recipe index unavailable")
        };
        var recipe = CreateRecipeViewModel(repository);

        await recipe.RefreshAsync(CancellationToken.None);

        recipe.Recipes.Should().BeEmpty();
        recipe.StatusText.Should().Contain("recipe index unavailable");
        recipe.HasAlert.Should().BeTrue();
        recipe.AlertMessage.Should().Contain("recipe index unavailable");
        recipe.RefreshCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Recipe_SaveRecipeAsync_Should_Save_And_Refresh_Index()
    {
        var repository = new FakeRecipeIndexRepository();
        var library = new FakeRecipeLibraryUseCase
        {
            SaveHandler = async (request, cancellationToken) =>
            {
                var saved = CreateRecipeIndexEntry(
                    request.Recipe!.RecipeId,
                    request.Recipe.Version,
                    request.Recipe.ProductName,
                    isActive: false,
                    isValid: true,
                    validationSummary: "Valid",
                    updatedAt: request.Recipe.UpdatedAt);
                await repository.SaveAsync(saved, cancellationToken);
                return RecipeLibrarySaveResult.Success(saved);
            }
        };
        var recipe = CreateRecipeViewModel(repository, library);
        recipe.RecipeIdText = "PKG-NEW";
        recipe.ProductNameText = "New Product";
        recipe.VersionText = "1.2.3";
        recipe.TeachingXText = "12.500";
        recipe.RoiWidthText = "320";

        await recipe.SaveRecipeAsync(CancellationToken.None);

        library.SaveRequests.Should().ContainSingle();
        var savedRecipe = library.SaveRequests[0].Recipe;
        savedRecipe.Should().NotBeNull();
        savedRecipe!.RecipeId.Should().Be("PKG-NEW");
        savedRecipe.ProductName.Should().Be("New Product");
        savedRecipe.Version.Should().Be("1.2.3");
        savedRecipe.Motion.TeachingPoints[0].Position.X.Should().Be(12.5);
        savedRecipe.Vision.Rois[0].Width.Should().Be(320);
        recipe.Recipes.Should().ContainSingle();
        recipe.SelectedRecipe.Should().NotBeNull();
        recipe.SelectedRecipe!.RecipeId.Should().Be("PKG-NEW");
        recipe.StatusText.Should().Be("Saved recipe 'PKG-NEW' v1.2.3");
        recipe.HasAlert.Should().BeFalse();
        recipe.AlertMessage.Should().BeNull();
    }

    [Fact]
    public async Task Recipe_SaveRecipeAsync_Should_Reject_Invalid_Numeric_Input()
    {
        var library = new FakeRecipeLibraryUseCase();
        var recipe = CreateRecipeViewModel(libraryUseCase: library);
        recipe.CameraExposureText = "not-a-number";

        await recipe.SaveRecipeAsync(CancellationToken.None);

        library.SaveRequests.Should().BeEmpty();
        recipe.StatusText.Should().Contain("Camera exposure must be a finite number");
        recipe.HasAlert.Should().BeTrue();
        recipe.AlertMessage.Should().Contain("Recipe input rejected");
    }

    [Fact]
    public async Task Recipe_SaveRecipeAsync_Should_Surface_Library_Validation_Failure()
    {
        var library = new FakeRecipeLibraryUseCase
        {
            SaveHandler = (_, _) => Task.FromResult(RecipeLibrarySaveResult.ValidationFailed(new[]
            {
                new RecipeValidationIssue("Recipe.VersionInvalid", "Recipe version must use major.minor.patch format.")
            }))
        };
        var recipe = CreateRecipeViewModel(libraryUseCase: library);
        recipe.VersionText = "1.0";

        await recipe.SaveRecipeAsync(CancellationToken.None);

        library.SaveRequests.Should().ContainSingle();
        recipe.StatusText.Should().Contain("Recipe version must use major.minor.patch format");
        recipe.HasAlert.Should().BeTrue();
        recipe.AlertMessage.Should().Contain("Recipe save rejected");
        recipe.Recipes.Should().BeEmpty();
    }

    [Fact]
    public async Task Recipe_ActivateRecipeAsync_Should_Set_Selected_Recipe_Active_And_Refresh_Index()
    {
        var previous = CreateRecipeIndexEntry(
            "PKG-A",
            "1.0.0",
            "Package A",
            isActive: true,
            isValid: true);
        var next = CreateRecipeIndexEntry(
            "PKG-B",
            "2.0.0",
            "Package B",
            isActive: false,
            isValid: true);
        var recipe = CreateRecipeViewModel(new FakeRecipeIndexRepository(previous, next));

        await recipe.RefreshAsync(CancellationToken.None);
        recipe.SelectedRecipe = recipe.Recipes.Single(entry => entry.RecipeId == "PKG-B");

        await recipe.ActivateRecipeAsync(CancellationToken.None);

        recipe.ActiveRecipeText.Should().Be("PKG-B v2.0.0");
        recipe.ActiveRecipeCount.Should().Be(1);
        recipe.SelectedRecipe.Should().NotBeNull();
        recipe.SelectedRecipe!.RecipeId.Should().Be("PKG-B");
        recipe.Recipes.Single(entry => entry.RecipeId == "PKG-A").IsActive.Should().BeFalse();
        recipe.StatusText.Should().Be("Activated recipe 'PKG-B' v2.0.0");
        recipe.HasAlert.Should().BeFalse();
        recipe.AlertMessage.Should().BeNull();
    }

    [Fact]
    public async Task Recipe_ActivateRecipeAsync_Should_Require_Selected_Recipe()
    {
        var recipe = CreateRecipeViewModel();

        recipe.ActivateRecipeCommand.CanExecute(null).Should().BeFalse();
        await recipe.ActivateRecipeAsync(CancellationToken.None);

        recipe.StatusText.Should().Be("Select a recipe to activate");
    }

    [Fact]
    public async Task Recipe_ActivateRecipeAsync_Should_Surface_Missing_Target()
    {
        var entry = CreateRecipeIndexEntry(
            "PKG-A",
            "1.0.0",
            "Package A",
            isActive: false,
            isValid: true);
        var repository = new FakeRecipeIndexRepository(entry)
        {
            SetActiveHandler = (_, _, _) => Task.FromResult(false)
        };
        var recipe = CreateRecipeViewModel(repository);

        await recipe.RefreshAsync(CancellationToken.None);
        await recipe.ActivateRecipeAsync(CancellationToken.None);

        recipe.StatusText.Should().Contain("activation rejected");
        recipe.ActiveRecipeText.Should().Be("-");
    }

    [Fact]
    public async Task Recipe_ActivateRecipeAsync_Should_Surface_Refresh_Failure_After_Activation()
    {
        var entry = CreateRecipeIndexEntry(
            "PKG-A",
            "1.0.0",
            "Package A",
            isActive: false,
            isValid: true);
        var calls = 0;
        var repository = new FakeRecipeIndexRepository(entry)
        {
            ListHandler = (_, _) =>
            {
                calls++;
                if (calls > 1)
                {
                    throw new InvalidOperationException("recipe index unavailable");
                }

                return Task.FromResult<IReadOnlyList<RecipeIndexEntry>>(new[] { entry });
            }
        };
        var recipe = CreateRecipeViewModel(repository);

        await recipe.RefreshAsync(CancellationToken.None);
        await recipe.ActivateRecipeAsync(CancellationToken.None);

        recipe.StatusText.Should().Contain("index refresh failed");
        recipe.StatusText.Should().Contain("recipe index unavailable");
        recipe.HasAlert.Should().BeTrue();
        recipe.AlertMessage.Should().Contain("index refresh failed");
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

    private static DashboardViewModel CreateDashboardViewModel(IEquipmentController? equipmentController = null)
    {
        return new DashboardViewModel(new EquipmentDashboardUseCase(
            equipmentController ?? new VirtualEquipmentController(),
            new CommandInterlockService()));
    }

    private static EquipmentViewModel CreateEquipmentViewModel(
        VirtualEquipmentController? controller = null,
        FakeEquipmentAlarmRecorder? alarmRecorder = null)
    {
        var resolvedController = controller ?? new VirtualEquipmentController();
        var transitionRepository = new FakeEquipmentIoTransitionRepository();
        return new EquipmentViewModel(
            new EquipmentDashboardUseCase(resolvedController, new CommandInterlockService()),
            new EquipmentFaultInjectionUseCase(
                resolvedController,
                resolvedController,
                alarmRecorder ?? new FakeEquipmentAlarmRecorder(),
                transitionRepository),
            transitionRepository);
    }

    private static AlarmViewModel CreateAlarmViewModel(FakeAlarmCenterUseCase? useCase = null)
    {
        return new AlarmViewModel(useCase ?? new FakeAlarmCenterUseCase());
    }

    private static MotionViewModel CreateMotionViewModel(
        FakeMotionCommandHistoryReader? historyReader = null,
        FakeMotionCommandUseCase? commandUseCase = null,
        FakeEquipmentController? equipmentController = null)
    {
        var resolvedEquipmentController = equipmentController ?? new FakeEquipmentController(CreateSnapshot());
        return new MotionViewModel(
            commandUseCase ?? new FakeMotionCommandUseCase(),
            historyReader ?? new FakeMotionCommandHistoryReader(),
            new MotionPanelUseCase(resolvedEquipmentController, new CommandInterlockService()));
    }

    private static TeachingViewModel CreateTeachingViewModel(
        FakeTeachingPointUseCase? useCase = null,
        FakeUserConfirmationService? confirmationService = null,
        FakeActiveRecipeContext? activeRecipeContext = null)
    {
        return new TeachingViewModel(
            useCase ?? new FakeTeachingPointUseCase(),
            confirmationService ?? new FakeUserConfirmationService(true),
            activeRecipeContext ?? new FakeActiveRecipeContext());
    }

    private static InspectionViewModel CreateInspectionViewModel(
        FakeInspectionRunUseCase? inspectionRunUseCase = null)
    {
        return new InspectionViewModel(inspectionRunUseCase ?? new FakeInspectionRunUseCase());
    }

    private static RecipeViewModel CreateRecipeViewModel(
        FakeRecipeIndexRepository? repository = null,
        FakeRecipeLibraryUseCase? libraryUseCase = null)
    {
        var resolvedRepository = repository ?? new FakeRecipeIndexRepository();
        var resolvedLibraryUseCase = libraryUseCase ?? new FakeRecipeLibraryUseCase(resolvedRepository);
        resolvedLibraryUseCase.IndexRepository ??= resolvedRepository;
        return new RecipeViewModel(resolvedLibraryUseCase);
    }

    private static OfflineDebugViewModel CreateOfflineDebugViewModel(
        FakeInspectionResultReader? resultReader = null,
        FakeInspectionArtifactReader? artifactReader = null,
        FakeInspectionReinspectComparisonReader? reinspectComparisonReader = null,
        FakeInspectionReinspectRecipePolicyUseCase? reinspectRecipePolicyUseCase = null,
        FakeInspectionReinspectSourceImageReadinessUseCase? sourceImageReadinessUseCase = null,
        FakeUserConfirmationService? confirmationService = null,
        FakeArtifactViewerService? artifactViewerService = null)
    {
        var resolvedArtifactReader = artifactReader ?? new FakeInspectionArtifactReader();
        return new OfflineDebugViewModel(
            resultReader ?? new FakeInspectionResultReader(),
            resolvedArtifactReader,
            new InspectionReinspectUseCase(
                () => new DateTimeOffset(2026, 6, 1, 12, 50, 0, TimeSpan.Zero),
                () => Guid.Parse("11111111-2222-3333-4444-555555555555")),
            reinspectComparisonReader ?? new FakeInspectionReinspectComparisonReader(),
            reinspectRecipePolicyUseCase ?? new FakeInspectionReinspectRecipePolicyUseCase(),
            sourceImageReadinessUseCase ?? new FakeInspectionReinspectSourceImageReadinessUseCase(resolvedArtifactReader),
            confirmationService ?? new FakeUserConfirmationService(true),
            artifactViewerService ?? new FakeArtifactViewerService());
    }

    private static RecipeIndexEntry CreateRecipeIndexEntry(
        string recipeId,
        string version,
        string productName,
        bool isActive,
        bool isValid,
        string? validationSummary = null,
        DateTimeOffset? updatedAt = null)
    {
        var timestamp = updatedAt ?? DateTimeOffset.UtcNow;
        return new RecipeIndexEntry(
            Guid.NewGuid(),
            recipeId,
            version,
            productName,
            $@"assets\recipes\{recipeId}.v{version}.recipe.json",
            "0123456789abcdef",
            isActive,
            isValid,
            validationSummary,
            timestamp.AddMinutes(-5),
            timestamp);
    }

    private static RecipeDefinition CreateAppCompositionRecipe()
    {
        return new RecipeDefinition(
            "APP-COMPOSITION-RCP",
            "App Composition Recipe",
            "1.0.0",
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 1, 0, 0, TimeSpan.Zero),
            new RecipeMotionSection(new[]
            {
                new RecipeTeachingPoint(
                    "CAMERA_POS_01",
                    "Camera Position 01",
                    TeachingRole.Camera,
                    new Position4D(10.0, 20.0, 8.0, 0.0),
                    new PositionTolerance(0.05, 0.05, 0.02, 0.1))
            }),
            new RecipeCameraSettings(5.0, 1.0, 80),
            new RecipeVisionSection(
                new[] { new RecipeRoi("IC_TOP", "IC Top", 116, 74, 92, 88) },
                new RecipeVisionParameters(0.75, 8, 0.65, 1.0, 0.15, 0.15)),
            new RecipeSequence(new[] { "SafetyCheck", "MoveToCamera", "Grab", "Inspect2D", "Inspect3D", "Judge", "Persist" }));
    }

    private static ActiveRecipeContextResult CreateActiveRecipeContextResult(string recipeId, string version)
    {
        return ActiveRecipeContextResult.Success(CreateRecipeIndexEntry(
            recipeId,
            version,
            $"{recipeId} Product",
            isActive: true,
            isValid: true,
            validationSummary: "Valid"));
    }

    private static InspectionRunResult CreateInspectionRunResult(
        InspectionRunStatus status,
        string message,
        RecipeIndexEntry? recipe = null,
        CameraGrabResult? cameraGrabResult = null,
        VisionInspectionResult? visionResult = null,
        VisionInspectionResult? heightMapResult = null)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var request = recipe is null
            ? null
            : new MachineCommandRequest(
                "Run Inspection",
                CorrelationId.New(),
                TimeSpan.FromSeconds(5),
                timestamp,
                new Dictionary<string, string>
                {
                    ["RecipeId"] = recipe.RecipeId,
                    ["RecipeVersion"] = recipe.Version
                });
        var commandResult = request is null
            ? null
            : MachineCommandResult.Success("Run Inspection accepted.", TimeSpan.FromMilliseconds(10), request.CorrelationId);
        var resolvedVisionResult = visionResult ?? (request is null || status != InspectionRunStatus.Accepted
            ? null
            : new VisionInspectionResult(
                Judgment.Pass,
                Array.Empty<Defect>(),
                "2D inspection Pass: 1 ROI(s) evaluated.",
                recipe!.RecipeId,
                recipe.Version,
                TimeSpan.FromMilliseconds(2),
                timestamp.AddMilliseconds(11)));
        var resolvedHeightMapResult = heightMapResult ?? (request is null || status != InspectionRunStatus.Accepted
            ? null
            : new VisionInspectionResult(
                Judgment.Pass,
                Array.Empty<Defect>(),
                "3D inspection Pass: 1 ROI(s) evaluated.",
                recipe!.RecipeId,
                recipe.Version,
                TimeSpan.FromMilliseconds(2),
                timestamp.AddMilliseconds(12)));

        return new InspectionRunResult(
            status,
            message,
            recipe,
            request,
            commandResult,
            null,
            cameraGrabResult,
            resolvedVisionResult,
            resolvedHeightMapResult,
            status == InspectionRunStatus.Accepted ? Guid.NewGuid() : null,
            new[]
            {
                new InspectionSequenceStepRecord("Load Recipe", recipe is null ? InspectionSequenceStepStatus.Failed : InspectionSequenceStepStatus.Success, message, TimeSpan.FromMilliseconds(1)),
                new InspectionSequenceStepRecord("Safety Interlock", recipe is null ? InspectionSequenceStepStatus.Skipped : InspectionSequenceStepStatus.Success, recipe is null ? "Skipped" : "Inspection interlocks passed.", TimeSpan.FromMilliseconds(1)),
                new InspectionSequenceStepRecord("Start Sequence", status == InspectionRunStatus.Accepted ? InspectionSequenceStepStatus.Success : InspectionSequenceStepStatus.Skipped, message, TimeSpan.FromMilliseconds(1)),
                new InspectionSequenceStepRecord("Grab Image", cameraGrabResult?.IsSuccess == true ? InspectionSequenceStepStatus.Success : InspectionSequenceStepStatus.Skipped, cameraGrabResult?.Message ?? "Skipped", TimeSpan.FromMilliseconds(1)),
                new InspectionSequenceStepRecord("Inspect 2D", resolvedVisionResult is null ? InspectionSequenceStepStatus.Skipped : InspectionSequenceStepStatus.Success, resolvedVisionResult?.Message ?? "Skipped", TimeSpan.FromMilliseconds(1)),
                new InspectionSequenceStepRecord("Inspect 3D", resolvedHeightMapResult is null ? InspectionSequenceStepStatus.Skipped : InspectionSequenceStepStatus.Success, resolvedHeightMapResult?.Message ?? "Skipped", TimeSpan.FromMilliseconds(1)),
                new InspectionSequenceStepRecord("Judge", resolvedVisionResult is null || resolvedHeightMapResult is null ? InspectionSequenceStepStatus.Skipped : InspectionSequenceStepStatus.Success, resolvedVisionResult is null || resolvedHeightMapResult is null ? "Skipped" : "Judge: Pass.", TimeSpan.Zero)
            },
            timestamp,
            timestamp.AddMilliseconds(12));
    }

    private static InspectionResultRecord CreateInspectionResultRecord(
        Judgment judgment,
        DateTimeOffset createdAt,
        IReadOnlyList<InspectionDefectRecord>? defects = null)
    {
        var resultId = Guid.NewGuid();
        var resolvedDefects = defects ?? Array.Empty<InspectionDefectRecord>();
        return new InspectionResultRecord(
            resultId,
            CorrelationId.New().ToString(),
            "LOT-20260601123000",
            "RCP-OFFLINE",
            "1.0.0",
            judgment,
            resolvedDefects.Count == 0 ? "No defects" : $"{resolvedDefects.Count} defect(s)",
            $"inspection-artifacts/20260601/{resultId:N}.source.bmp",
            $"inspection-artifacts/20260601/{resultId:N}.overlay.bmp",
            $"inspection-artifacts/20260601/{resultId:N}.height.bmp",
            TimeSpan.FromMilliseconds(123),
            createdAt,
            resolvedDefects);
    }

    private static InspectionReinspectComparisonResult CreateReinspectComparison(string replayCorrelationId)
    {
        return new InspectionReinspectComparisonResult(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            replayCorrelationId,
            "LOT-20260601123000",
            "RCP-OFFLINE",
            "1.0.0",
            "Pass",
            "Pass",
            0,
            0,
            TimeSpan.FromMilliseconds(123),
            TimeSpan.FromMilliseconds(123),
            InspectionReinspectComparisonStatus.Matched,
            new DateTimeOffset(2026, 6, 1, 12, 50, 0, TimeSpan.Zero),
            "Persisted to offline re-inspect history.",
            "Metadata comparison completed from the prepared historical result context.");
    }

    private static CameraFrame CreateCameraFrame(string recipeId, string version)
    {
        return new CameraFrame(
            "Fake camera",
            width: 16,
            height: 12,
            stride: 16,
            CameraPixelFormat.Gray8,
            Enumerable.Range(0, 16 * 12).Select(index => (byte)(index % 255)).ToArray(),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string>
            {
                ["RecipeId"] = recipeId,
                ["RecipeVersion"] = version
            });
    }

    private sealed class FakeRecipeIndexRepository : IRecipeIndexRepository
    {
        private readonly List<RecipeIndexEntry> _entries = new();

        public FakeRecipeIndexRepository(params RecipeIndexEntry[] entries)
        {
            _entries.AddRange(entries);
        }

        public Func<int, CancellationToken, Task<IReadOnlyList<RecipeIndexEntry>>>? ListHandler { get; init; }
        public Func<string, string, CancellationToken, Task<bool>>? SetActiveHandler { get; init; }

        public Task SaveAsync(RecipeIndexEntry entry, CancellationToken cancellationToken)
        {
            _entries.RemoveAll(candidate =>
                string.Equals(candidate.RecipeId, entry.RecipeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.Version, entry.Version, StringComparison.OrdinalIgnoreCase));
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<RecipeIndexEntry?> FindAsync(
            string recipeId,
            string version,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_entries.FirstOrDefault(entry =>
                string.Equals(entry.RecipeId, recipeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Version, version, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<RecipeIndexEntry?> FindActiveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_entries
                .OrderByDescending(entry => entry.UpdatedAt)
                .FirstOrDefault(entry => entry.IsActive));
        }

        public Task<bool> SetActiveAsync(
            string recipeId,
            string version,
            CancellationToken cancellationToken)
        {
            if (SetActiveHandler is not null)
            {
                return SetActiveHandler(recipeId, version, cancellationToken);
            }

            var hasTarget = _entries.Any(entry =>
                string.Equals(entry.RecipeId, recipeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Version, version, StringComparison.OrdinalIgnoreCase));

            if (!hasTarget)
            {
                return Task.FromResult(false);
            }

            for (var index = 0; index < _entries.Count; index++)
            {
                var entry = _entries[index];
                var isTarget =
                    string.Equals(entry.RecipeId, recipeId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.Version, version, StringComparison.OrdinalIgnoreCase);
                _entries[index] = entry with { IsActive = isTarget };
            }

            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<RecipeIndexEntry>> ListRecentAsync(int limit, CancellationToken cancellationToken)
        {
            if (ListHandler is not null)
            {
                return ListHandler(limit, cancellationToken);
            }

            return Task.FromResult<IReadOnlyList<RecipeIndexEntry>>(
                _entries
                    .OrderByDescending(entry => entry.UpdatedAt)
                    .Take(limit)
                    .ToArray());
        }
    }

    private sealed class FakeRecipeLibraryUseCase : IRecipeLibraryUseCase
    {
        public FakeRecipeLibraryUseCase(FakeRecipeIndexRepository? indexRepository = null)
        {
            IndexRepository = indexRepository;
        }

        public FakeRecipeIndexRepository? IndexRepository { get; set; }
        public List<RecipeLibrarySaveRequest> SaveRequests { get; } = new();

        public Func<RecipeLibrarySaveRequest, CancellationToken, Task<RecipeLibrarySaveResult>>? SaveHandler { get; init; }

        public Task<IReadOnlyList<RecipeIndexEntry>> ListRecentAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            return (IndexRepository ?? new FakeRecipeIndexRepository()).ListRecentAsync(limit, cancellationToken);
        }

        public Task<RecipeLibrarySaveResult> SaveAsync(
            RecipeLibrarySaveRequest request,
            CancellationToken cancellationToken)
        {
            SaveRequests.Add(request);
            if (SaveHandler is not null)
            {
                return SaveHandler(request, cancellationToken);
            }

            var recipe = request.Recipe!;
            return Task.FromResult(RecipeLibrarySaveResult.Success(CreateRecipeIndexEntry(
                recipe.RecipeId,
                recipe.Version,
                recipe.ProductName,
                isActive: false,
                isValid: true,
                validationSummary: "Valid",
                updatedAt: recipe.UpdatedAt)));
        }

        public Task<bool> ActivateAsync(
            string recipeId,
            string version,
            CancellationToken cancellationToken)
        {
            return (IndexRepository ?? new FakeRecipeIndexRepository()).SetActiveAsync(recipeId, version, cancellationToken);
        }
    }

    private sealed class FakeInspectionRunUseCase : IInspectionRunUseCase
    {
        private readonly InspectionRunResult _result;

        public FakeInspectionRunUseCase()
            : this(CreateInspectionRunResult(
                InspectionRunStatus.ActiveRecipeNotSelected,
                "No active recipe is selected."))
        {
        }

        public FakeInspectionRunUseCase(InspectionRunResult result)
        {
            _result = result;
        }

        public List<InspectionRunRequest> Requests { get; } = new();
        public int PrecheckRequests { get; private set; }
        public ActiveRecipeContextResult PrecheckResult { get; init; } = ActiveRecipeContextResult.NotSelected();

        public Task<ActiveRecipeContextResult> PrecheckActiveRecipeAsync(CancellationToken cancellationToken)
        {
            PrecheckRequests++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(PrecheckResult);
        }

        public Task<InspectionRunResult> RunAsync(
            InspectionRunRequest request,
            IProgress<InspectionSequenceStepRecord>? progress,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            foreach (var step in _result.Steps)
            {
                progress?.Report(step);
            }

            return Task.FromResult(_result);
        }
    }

    private sealed class FakeActiveRecipeContext : IActiveRecipeContext
    {
        private readonly ActiveRecipeContextResult _result;

        public FakeActiveRecipeContext()
            : this(ActiveRecipeContextResult.NotSelected())
        {
        }

        public FakeActiveRecipeContext(ActiveRecipeContextResult result)
        {
            _result = result;
        }

        public int RequestCount { get; private set; }

        public Task<ActiveRecipeContextResult> GetActiveAsync(CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeUserConfirmationService : IUserConfirmationService
    {
        private readonly bool _result;

        public FakeUserConfirmationService(bool result)
        {
            _result = result;
        }

        public List<(string Title, string Message)> Prompts { get; } = new();

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            CancellationToken cancellationToken)
        {
            Prompts.Add((title, message));
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeArtifactViewerService : IArtifactViewerService
    {
        public List<string> OpenedPaths { get; } = new();

        public Task OpenAsync(
            string artifactPath,
            CancellationToken cancellationToken)
        {
            OpenedPaths.Add(artifactPath);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTeachingHistoryRepository : ITeachingHistoryRepository
    {
        private readonly List<TeachingHistoryEntry> _entries = new();

        public FakeTeachingHistoryRepository(params TeachingHistoryEntry[] entries)
        {
            _entries.AddRange(entries);
        }

        public List<(Guid TeachingPointId, int Limit)> ListRequests { get; } = new();

        public Func<Guid, int, CancellationToken, Task<IReadOnlyList<TeachingHistoryEntry>>>? ListHandler { get; init; }

        public Task SaveAsync(TeachingHistoryEntry entry, CancellationToken cancellationToken)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TeachingHistoryEntry>> ListByPointAsync(
            Guid teachingPointId,
            int limit,
            CancellationToken cancellationToken)
        {
            ListRequests.Add((teachingPointId, limit));
            if (ListHandler is not null)
            {
                return ListHandler(teachingPointId, limit, cancellationToken);
            }

            return Task.FromResult<IReadOnlyList<TeachingHistoryEntry>>(
                _entries
                    .Where(entry => entry.TeachingPointId == teachingPointId)
                    .OrderByDescending(entry => entry.CreatedAt)
                    .Take(limit)
                    .ToArray());
        }
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

    private sealed class FakeAlarmCenterUseCase : IAlarmCenterUseCase
    {
        private readonly List<EquipmentAlarm> _alarms;

        public FakeAlarmCenterUseCase(params EquipmentAlarm[] alarms)
        {
            _alarms = alarms.ToList();
        }

        public List<(Guid AlarmId, string? ActionMemo)> Acknowledged { get; } = new();

        public Task<IReadOnlyList<EquipmentAlarm>> ListRecentAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<EquipmentAlarm>>(
                _alarms
                    .OrderByDescending(alarm => alarm.OccurredAt)
                    .Take(limit)
                    .ToArray());
        }

        public Task AcknowledgeAsync(
            Guid alarmId,
            string? actionMemo,
            CancellationToken cancellationToken)
        {
            var index = _alarms.FindIndex(alarm => alarm.Id == alarmId);
            if (index >= 0)
            {
                _alarms[index] = _alarms[index].Acknowledge(
                    new DateTimeOffset(2026, 6, 1, 12, 10, 0, TimeSpan.Zero),
                    actionMemo);
            }

            Acknowledged.Add((alarmId, actionMemo));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEquipmentAlarmRecorder : IEquipmentAlarmRecorder
    {
        public List<EquipmentAlarm> Alarms { get; } = new();
        public List<(ErrorCode ErrorCode, EquipmentArea Area, string Message, string? CorrelationId)> Failures { get; } = new();

        public Task RecordAsync(EquipmentAlarm alarm, CancellationToken cancellationToken)
        {
            Alarms.Add(alarm);
            return Task.CompletedTask;
        }

        public Task RecordFailureAsync(
            ErrorCode errorCode,
            EquipmentArea area,
            string message,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            Failures.Add((errorCode, area, message, correlationId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEquipmentIoTransitionRepository : IEquipmentIoTransitionRepository
    {
        private readonly List<IoTransitionRecord> _transitions = new();

        public Task SaveAsync(IoTransitionRecord transition, CancellationToken cancellationToken)
        {
            _transitions.Insert(0, transition);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IoTransitionRecord>> ListRecentAsync(int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<IoTransitionRecord>>(_transitions.Take(limit).ToArray());
        }
    }

    private sealed class FakeInspectionResultReader : IInspectionResultReader
    {
        private readonly IReadOnlyList<InspectionResultRecord> _records;

        public FakeInspectionResultReader(params InspectionResultRecord[] records)
        {
            _records = records;
        }

        public Func<int, CancellationToken, Task<IReadOnlyList<InspectionResultRecord>>>? ListHandler { get; init; }

        public Task<IReadOnlyList<InspectionResultRecord>> ListRecentAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            if (ListHandler is not null)
            {
                return ListHandler(limit, cancellationToken);
            }

            return Task.FromResult<IReadOnlyList<InspectionResultRecord>>(_records.Take(limit).ToArray());
        }
    }

    private sealed class FakeInspectionReinspectComparisonReader : IInspectionReinspectComparisonReader
    {
        private readonly IReadOnlyList<InspectionReinspectComparisonResult> _records;

        public FakeInspectionReinspectComparisonReader(params InspectionReinspectComparisonResult[] records)
        {
            _records = records;
        }

        public Task<IReadOnlyList<InspectionReinspectComparisonResult>> ListRecentAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<InspectionReinspectComparisonResult>>(_records.Take(limit).ToArray());
        }
    }

    private sealed class FakeInspectionReinspectRecipePolicyUseCase : IInspectionReinspectRecipePolicyUseCase
    {
        public Task<InspectionReinspectRecipePolicyResult> ResolveAsync(
            InspectionReinspectPreparation preparation,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(InspectionReinspectRecipePolicyResult.ActiveMatchesHistorical(
                preparation,
                preparation.RecipeId,
                preparation.RecipeVersion));
        }
    }

    private sealed class FakeInspectionReinspectSourceImageReadinessUseCase : IInspectionReinspectSourceImageReadinessUseCase
    {
        private readonly InspectionReinspectSourceImageReadinessUseCase _inner;

        public FakeInspectionReinspectSourceImageReadinessUseCase(IInspectionArtifactReader artifactReader)
        {
            _inner = new InspectionReinspectSourceImageReadinessUseCase(artifactReader);
        }

        public Task<InspectionReinspectSourceImageReadinessResult> ResolveAsync(
            InspectionReinspectPreparation preparation,
            CancellationToken cancellationToken)
        {
            return _inner.ResolveAsync(preparation, cancellationToken);
        }
    }

    private sealed class FakeInspectionArtifactReader : IInspectionArtifactReader
    {
        private static readonly DateTimeOffset Timestamp = new(2026, 6, 1, 12, 45, 0, TimeSpan.Zero);
        private static readonly byte[] PreviewPixels =
        {
            0, 0, 0, 255,
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255
        };

        public List<string?> MetadataRequests { get; } = new();
        public List<string?> PreviewRequests { get; } = new();
        public List<InspectionArtifactOpenRequest> OpenRequests { get; } = new();

        public Func<string?, CancellationToken, Task<InspectionArtifactMetadata>>? ReadHandler { get; init; }
        public Func<string?, CancellationToken, Task<InspectionArtifactPreviewResult>>? PreviewHandler { get; init; }
        public Func<InspectionArtifactOpenRequest, CancellationToken, Task<InspectionArtifactOpenResult>>? OpenHandler { get; init; }

        public Task<InspectionArtifactMetadata> ReadMetadataAsync(
            string? artifactPath,
            CancellationToken cancellationToken)
        {
            MetadataRequests.Add(artifactPath);
            if (ReadHandler is not null)
            {
                return ReadHandler(artifactPath, cancellationToken);
            }

            return Task.FromResult(string.IsNullOrWhiteSpace(artifactPath)
                ? InspectionArtifactMetadata.NotRecorded()
                : InspectionArtifactMetadata.Available(artifactPath, 2048, Timestamp));
        }

        public Task<InspectionArtifactPreviewResult> ReadPreviewAsync(
            string? artifactPath,
            CancellationToken cancellationToken)
        {
            PreviewRequests.Add(artifactPath);
            if (PreviewHandler is not null)
            {
                return PreviewHandler(artifactPath, cancellationToken);
            }

            return Task.FromResult(string.IsNullOrWhiteSpace(artifactPath)
                ? InspectionArtifactPreviewResult.FromMetadata(InspectionArtifactMetadata.NotRecorded())
                : InspectionArtifactPreviewResult.Available(
                    artifactPath,
                    width: 2,
                    height: 2,
                    stride: 8,
                    pixelFormat: InspectionArtifactPreviewPixelFormat.Bgra32,
                    pixels: PreviewPixels));
        }

        public Task<InspectionArtifactOpenResult> PrepareOpenAsync(
            InspectionArtifactOpenRequest request,
            CancellationToken cancellationToken)
        {
            OpenRequests.Add(request);
            if (OpenHandler is not null)
            {
                return OpenHandler(request, cancellationToken);
            }

            return Task.FromResult(string.IsNullOrWhiteSpace(request.ArtifactPath)
                ? InspectionArtifactOpenResult.NotRecorded(request.ArtifactKind)
                : InspectionArtifactOpenResult.Ready(
                    request.ArtifactKind,
                    request.ArtifactPath,
                    Path.Combine(
                        Path.GetTempPath(),
                        "VisionCellArtifacts",
                        $"{request.ArtifactKind}.bmp")));
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
        public List<TeachingPointUpdateRequest> UpdateRequests { get; } = new();
        public List<TeachingPointDeleteRequest> DeleteRequests { get; } = new();
        public List<TeachingPointGoToRequest> GoToRequests { get; } = new();
        public List<TeachingHistoryEntry> HistoryEntries { get; } = new();
        public List<(Guid TeachingPointId, int Limit)> ListHistoryRequests { get; } = new();
        public Func<Guid, int, CancellationToken, Task<IReadOnlyList<TeachingHistoryEntry>>>? ListHandler { get; init; }

        public Task<IReadOnlyList<TeachingPoint>> ListAsync(int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TeachingPoint>>(_points.Take(limit).ToArray());
        }

        public Task<IReadOnlyList<TeachingHistoryEntry>> ListHistoryAsync(
            Guid teachingPointId,
            int limit,
            CancellationToken cancellationToken)
        {
            ListHistoryRequests.Add((teachingPointId, limit));
            if (ListHandler is not null)
            {
                return ListHandler(teachingPointId, limit, cancellationToken);
            }

            return Task.FromResult<IReadOnlyList<TeachingHistoryEntry>>(
                HistoryEntries
                    .Where(entry => entry.TeachingPointId == teachingPointId)
                    .Take(limit)
                    .ToArray());
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

        public Task<TeachingPointSaveResult> UpdateAsync(
            TeachingPointUpdateRequest request,
            CancellationToken cancellationToken)
        {
            UpdateRequests.Add(request);
            var index = _points.FindIndex(point => point.Id == request.TeachingPointId);
            if (index < 0)
            {
                return Task.FromResult(TeachingPointSaveResult.Failure(
                    TeachingPointOperationStatus.NotFound,
                    "Teaching point not found."));
            }

            var existing = _points[index];
            var updated = TeachingPointFactory.Create(
                request.Name,
                request.Role,
                request.Position,
                request.Tolerance,
                request.Memo,
                request.TeachingPointId).Point! with
            {
                CreatedAt = existing.CreatedAt
            };
            _points[index] = updated;
            return Task.FromResult(TeachingPointSaveResult.Success(updated));
        }

        public Task<TeachingPointDeleteResult> DeleteAsync(
            TeachingPointDeleteRequest request,
            CancellationToken cancellationToken)
        {
            DeleteRequests.Add(request);
            var point = _points.SingleOrDefault(item => item.Id == request.TeachingPointId);
            if (point is null)
            {
                return Task.FromResult(TeachingPointDeleteResult.Failure(
                    TeachingPointOperationStatus.NotFound,
                    "Teaching point not found."));
            }

            _points.Remove(point);
            return Task.FromResult(TeachingPointDeleteResult.Success(point));
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
                request.CommandTimeout,
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
        public CommandKind? LastCommand { get; private set; }
        public InterlockContext? LastContext { get; private set; }
        public Func<TimeSpan, CancellationToken, Task<EquipmentSnapshot>>? SnapshotHandler { get; init; }

        public Func<CommandKind, InterlockContext, TimeSpan, CancellationToken, Task<MachineCommandResult>> ExecuteHandler { get; set; } =
            (_, _, _, _) => Task.FromResult(MachineCommandResult.Failed(
                ErrorCode.CommandRejected,
                "Fake controller does not execute commands in app tests.",
                TimeSpan.Zero,
                CorrelationId.New()));

        public Task<EquipmentSnapshot> GetSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return SnapshotHandler is not null
                ? SnapshotHandler(timeout, cancellationToken)
                : Task.FromResult(Snapshot);
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
            LastCommand = command;
            LastContext = context;
            return ExecuteHandler(command, context, timeout, cancellationToken);
        }
    }

    private static EquipmentSnapshot CreateSnapshot(
        bool connected = false,
        bool servoOn = false,
        bool homed = false,
        MachineMode? mode = null)
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
            mode ?? (connected ? MachineMode.Manual : MachineMode.Offline),
            new SafetySnapshot(true, false, true, true, servoOn),
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
