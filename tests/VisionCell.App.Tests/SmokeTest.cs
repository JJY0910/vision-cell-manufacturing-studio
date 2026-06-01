using FluentAssertions;
using VisionCell.Application.Interlocks;
using VisionCell.Application.Motion;
using VisionCell.Core.Commands;
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
            new MotionViewModel(new FakeMotionCommandHistoryReader()),
            new TeachingViewModel(),
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
        var motion = new MotionViewModel(new FakeMotionCommandHistoryReader(
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
        var motion = new MotionViewModel(new FakeMotionCommandHistoryReader());

        await motion.RefreshHistoryAsync(CancellationToken.None);

        motion.HasHistory.Should().BeFalse();
        motion.RecentCommands.Should().BeEmpty();
        motion.HistoryStatus.Should().Be("No motion command history");
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
}
