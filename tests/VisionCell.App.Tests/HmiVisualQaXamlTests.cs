using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace VisionCell_App_Tests;

public sealed class HmiVisualQaXamlTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void Controls_Should_Keep_Dark_Hmi_Grid_Headers_And_Disabled_Tooltips()
    {
        var controls = XDocument.Load(GetRepoPath("src", "VisionCell.App", "Themes", "Controls.xaml"));

        controls.ToString().Should().Contain("Brush.PanelAlt");
        controls.ToString().Should().NotContain("Background\" Value=\"White");
        controls
            .Descendants(Wpf + "Style")
            .Where(style => style.Attribute("TargetType")?.Value is "Button")
            .SelectMany(style => style.Elements(Wpf + "Setter"))
            .Where(setter => setter.Attribute("Property")?.Value == "ToolTipService.ShowOnDisabled")
            .Select(setter => setter.Attribute("Value")?.Value)
            .Should()
            .OnlyContain(value => value == "True");
        controls.ToString().Should().Contain("GridViewColumnHeader");
        controls.ToString().Should().Contain("Brush.NavHover");
        controls.ToString().Should().Contain("Brush.NavFocus");
    }

    [Fact]
    public void EmptyState_Should_Collapse_When_Bound_List_Has_Items()
    {
        var emptyState = XDocument.Load(GetRepoPath("src", "VisionCell.App", "Shared", "Controls", "EmptyState.xaml"));

        emptyState.ToString().Should().Contain("HasItems");
        emptyState.ToString().Should().Contain("Visibility");
        emptyState.ToString().Should().Contain("Collapsed");
        emptyState.ToString().Should().Contain("Text.EmptyStateTitle");
        emptyState.ToString().Should().Contain("Text.EmptyStateDetail");
    }

    [Fact]
    public void Priority_Hmi_Screens_Should_Surface_Empty_States()
    {
        var expected = new Dictionary<string, string[]>
        {
            ["Modules/Teaching/Views/TeachingView.xaml"] = new[] { "No Teaching Points", "No Selected Point History" },
            ["Modules/Recipe/Views/RecipeView.xaml"] = new[] { "No Recipe Index Records" },
            ["Modules/OfflineDebug/Views/OfflineDebugView.xaml"] = new[] { "No Inspection Results", "No Defect Rows", "Run Re-inspect", "Re-inspect Readiness" },
            ["Modules/Alarm/Views/AlarmView.xaml"] = new[] { "No Alarm Records", "Recovery Boundary" },
            ["Modules/Motion/Views/MotionView.xaml"] = new[] { "No Axis Snapshot", "No Motion Command History" },
            ["Modules/Equipment/Views/EquipmentView.xaml"] = new[] { "No I/O Snapshot", "No Fault Events", "No I/O Transitions" },
            ["Modules/Reports/Views/ReportsView.xaml"] = new[] { "Reports Export Not Configured", "FR-203", "FR-204" },
            ["Modules/Settings/Views/SettingsView.xaml"] = new[] { "Runtime Scope", "Real Hardware Readiness Gate", "ReadinessGateItems" }
        };

        foreach (var (relativePath, markers) in expected)
        {
            var xaml = File.ReadAllText(GetRepoPath(new[] { "src", "VisionCell.App" }
                .Concat(relativePath.Split('/')).ToArray()));
            foreach (var marker in markers)
            {
                xaml.Should().Contain(marker, $"'{relativePath}' must show an operator-readable empty or scope state");
            }
        }
    }

    [Fact]
    public void Priority_Hmi_Screens_Should_Use_Shared_CommandBar()
    {
        var expectedTitles = new Dictionary<string, string>
        {
            ["Modules/Dashboard/Views/DashboardView.xaml"] = "Dashboard",
            ["Modules/Equipment/Views/EquipmentView.xaml"] = "Equipment",
            ["Modules/Motion/Views/MotionView.xaml"] = "Motion",
            ["Modules/Teaching/Views/TeachingView.xaml"] = "Teaching",
            ["Modules/Recipe/Views/RecipeView.xaml"] = "Recipe",
            ["Modules/Inspection/Views/InspectionView.xaml"] = "Inspection",
            ["Modules/Alarm/Views/AlarmView.xaml"] = "Alarm / Fault / Recovery"
        };

        foreach (var (relativePath, title) in expectedTitles)
        {
            var xaml = File.ReadAllText(GetRepoPath(new[] { "src", "VisionCell.App" }
                .Concat(relativePath.Split('/')).ToArray()));

            xaml.Should().Contain("<controls:CommandBar", $"'{relativePath}' should use the shared HMI command surface");
            xaml.Should().Contain($"Title=\"{title}\"", $"'{relativePath}' should expose the expected operator title");
        }
    }

    [Fact]
    public void Reports_Scope_State_Should_Not_Regress_To_Settings_Requirement()
    {
        var reports = File.ReadAllText(GetRepoPath("src", "VisionCell.App", "Modules", "Reports", "Views", "ReportsView.xaml"));

        reports.Should().Contain("FR-203");
        reports.Should().Contain("FR-204");
        reports.Should().NotContain("FR-240");
    }

    [Fact]
    public void CommandBar_Action_Buttons_Should_Use_Shared_Hmi_Command_Style()
    {
        var expectedCounts = new Dictionary<string, int>
        {
            ["Modules/Dashboard/Views/DashboardView.xaml"] = 5,
            ["Modules/Equipment/Views/EquipmentView.xaml"] = 1,
            ["Modules/Motion/Views/MotionView.xaml"] = 2,
            ["Modules/Teaching/Views/TeachingView.xaml"] = 2,
            ["Modules/Recipe/Views/RecipeView.xaml"] = 2,
            ["Modules/Inspection/Views/InspectionView.xaml"] = 3,
            ["Modules/OfflineDebug/Views/OfflineDebugView.xaml"] = 6,
            ["Modules/Alarm/Views/AlarmView.xaml"] = 1
        };

        foreach (var (relativePath, expectedCount) in expectedCounts)
        {
            var xaml = XDocument.Load(GetRepoPath(new[] { "src", "VisionCell.App" }
                .Concat(relativePath.Split('/')).ToArray()));
            var commandButtons = xaml
                .Descendants()
                .Where(element => element.Name.LocalName == "CommandBar.CommandContent")
                .SelectMany(element => element.Descendants(Wpf + "Button"))
                .ToArray();

            commandButtons.Should().HaveCount(expectedCount, $"'{relativePath}' should keep a known operator command set");
            commandButtons
                .Select(button => button.Attribute("Style")?.Value)
                .Should()
                .OnlyContain(value => value == "{DynamicResource Button.HmiCommand}");
            commandButtons.Should().OnlyContain(button => button.Attribute("Height") == null);
            commandButtons.Should().OnlyContain(button => button.Attribute("Margin") == null);
        }
    }

    [Fact]
    public void Module_Action_Buttons_Should_Use_Shared_Hmi_Button_Styles()
    {
        var knownModuleView = GetRepoPath("src", "VisionCell.App", "Modules", "Dashboard", "Views", "DashboardView.xaml");
        var modulesRoot = new DirectoryInfo(Path.GetDirectoryName(knownModuleView)!).Parent!.Parent!.FullName;
        var allowedStyles = new[]
        {
            "{DynamicResource Button.HmiCommand}",
            "{DynamicResource Button.HmiCommand.Compact}"
        };
        var invalidButtons = Directory
            .EnumerateFiles(modulesRoot, "*.xaml", SearchOption.AllDirectories)
            .SelectMany(path =>
            {
                var xaml = XDocument.Load(path);
                return xaml
                    .Descendants(Wpf + "Button")
                    .Where(button => !allowedStyles.Contains(button.Attribute("Style")?.Value, StringComparer.Ordinal))
                    .Select(button => $"{Path.GetRelativePath(modulesRoot, path)}:{button.Attribute("Content")?.Value ?? "(no content)"}");
            })
            .ToArray();

        invalidButtons.Should().BeEmpty("module operator buttons should use the shared HMI command styles");
    }

    [Fact]
    public void Disabled_Operator_Commands_Should_Expose_Tooltips()
    {
        var offlineDebug = XDocument.Load(GetRepoPath("src", "VisionCell.App", "Modules", "OfflineDebug", "Views", "OfflineDebugView.xaml"));
        var commandButtons = offlineDebug
            .Descendants(Wpf + "Button")
            .Where(button => button.Attribute("Content")?.Value is "Load Artifacts" or "Open Overlay" or "Open Height Map" or "Prepare Re-inspect");

        commandButtons.Should().NotBeEmpty();
        commandButtons
            .Select(button => button.Attribute("ToolTip")?.Value)
            .Should()
            .OnlyContain(value => !string.IsNullOrWhiteSpace(value) && value.Contains("Select an inspection result", StringComparison.Ordinal));

        var equipment = File.ReadAllText(GetRepoPath("src", "VisionCell.App", "Modules", "Equipment", "Views", "EquipmentView.xaml"));
        equipment.Should().Contain("ToolTip=\"{Binding FaultInjectionDisabledReason}\"");
        equipment.Should().Contain("FaultSummaryText");
        equipment.Should().Contain("IoSummaryText");
        equipment.Should().Contain("IoTransitions");
        equipment.Should().Contain("IoTransitionStatus");
        equipment.Should().Contain("RefreshIoTransitionHistoryCommand");

        var alarm = File.ReadAllText(GetRepoPath("src", "VisionCell.App", "Modules", "Alarm", "Views", "AlarmView.xaml"));
        alarm.Should().Contain("ToolTip=\"{Binding AcknowledgeDisabledReason}\"");
        alarm.Should().Contain("IsEnabled=\"{Binding IsActionMemoEditable}\"");
        alarm.Should().Contain("SelectedAlarm.RecoveryHint");
        alarm.Should().Contain("RecoveryBoundaryItems");
    }

    [Fact]
    public void Equipment_Fault_Injection_Buttons_Should_Use_Compact_Hmi_Command_Style()
    {
        var equipment = XDocument.Load(GetRepoPath("src", "VisionCell.App", "Modules", "Equipment", "Views", "EquipmentView.xaml"));
        var faultButtons = equipment
            .Descendants(Wpf + "Button")
            .Where(button => button.Attribute("ToolTip")?.Value == "{Binding FaultInjectionDisabledReason}")
            .ToArray();

        faultButtons.Should().HaveCount(13);
        faultButtons
            .Select(button => button.Attribute("Style")?.Value)
            .Should()
            .OnlyContain(value => value == "{DynamicResource Button.HmiCommand.Compact}");
        faultButtons.Should().OnlyContain(button => button.Attribute("Height") == null);
        faultButtons.Should().OnlyContain(button => button.Attribute("Margin") == null);
    }

    [Fact]
    public void OfflineDebug_Should_Separate_Prepare_And_Run_Reinspect_Commands()
    {
        var offlineDebug = XDocument.Load(GetRepoPath("src", "VisionCell.App", "Modules", "OfflineDebug", "Views", "OfflineDebugView.xaml"));

        var prepareButton = offlineDebug
            .Descendants(Wpf + "Button")
            .Single(button => button.Attribute("Content")?.Value == "Prepare Re-inspect");
        var runButton = offlineDebug
            .Descendants(Wpf + "Button")
            .Single(button => button.Attribute("Content")?.Value == "Run Re-inspect");

        prepareButton.Attribute("Command")?.Value.Should().Be("{Binding PrepareReinspectCommand}");
        runButton.Attribute("Command")?.Value.Should().Be("{Binding RunReinspectCommand}");
        runButton.Attribute("ToolTip")?.Value.Should().Be("{Binding ReinspectRunDisabledReason}");
        offlineDebug.ToString().Should().Contain("PreparedReinspectSummary");
        offlineDebug.ToString().Should().Contain("PreparedReinspectArtifactSummary");
        offlineDebug.ToString().Should().Contain("ReinspectReadinessItems");
        offlineDebug.ToString().Should().Contain("ReinspectComparisonSummary");
        offlineDebug.ToString().Should().Contain("ReinspectComparisonDetail");
    }

    private static string GetRepoPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate VisionCell repository root.");
    }
}
