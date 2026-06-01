using FluentAssertions;
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
        var dashboard = new DashboardViewModel(new VirtualEquipmentController());

        await dashboard.ConnectAsync(CancellationToken.None);

        dashboard.IsConnected.Should().BeTrue();
        dashboard.ConnectionStatus.Should().Be("Connected");
        dashboard.Axes.Should().HaveCount(4);
        dashboard.IoBits.Should().Contain(bit => bit.Name == "DI_ESTOP_ON");
        dashboard.Events.Should().Contain(systemEvent => systemEvent.EventType == "Connect");
    }

    [Fact]
    public void Shell_Navigation_Should_Change_Current_ViewModel_And_Selected_Item()
    {
        var shell = new ShellViewModel(
            new NavigationService(),
            new DashboardViewModel(new VirtualEquipmentController()),
            new EquipmentViewModel(),
            new MotionViewModel(),
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
}
