using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace VisionCell_App_Tests;

public sealed class ShellWindowLayoutXamlTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void ShellWindow_Should_Start_Maximized_With_1366x768_Minimum()
    {
        var shell = XDocument.Load(GetRepoPath("src", "VisionCell.App", "Shell", "ShellWindow.xaml")).Root!;

        shell.Attribute("WindowStartupLocation")!.Value.Should().Be("CenterScreen");
        shell.Attribute("WindowState")!.Value.Should().Be("Maximized");
        shell.Attribute("MinWidth")!.Value.Should().Be("1366");
        shell.Attribute("MinHeight")!.Value.Should().Be("768");
    }

    [Fact]
    public void ShellWindow_Should_Keep_Workspace_And_Navigation_Constrained()
    {
        var shell = XDocument.Load(GetRepoPath("src", "VisionCell.App", "Shell", "ShellWindow.xaml"));
        var workspace = shell
            .Descendants(Wpf + "Border")
            .Single(element => element.Attribute("Grid.Column")?.Value == "1" &&
                element.Attribute("Grid.Row")?.Value == "1");
        var navScroll = shell
            .Descendants(Wpf + "ScrollViewer")
            .Single(element => element.Descendants(Wpf + "ItemsControl").Any());

        workspace.Attribute("ClipToBounds")!.Value.Should().Be("True");
        workspace.Attribute("MinWidth")!.Value.Should().Be("0");
        navScroll.Attribute("VerticalScrollBarVisibility")!.Value.Should().Be("Auto");
        navScroll.Attribute("HorizontalScrollBarVisibility")!.Value.Should().Be("Disabled");
    }

    [Fact]
    public void DashboardView_Should_Wrap_Command_Buttons_Below_Title()
    {
        var dashboard = XDocument.Load(GetRepoPath("src", "VisionCell.App", "Modules", "Dashboard", "Views", "DashboardView.xaml"));
        var commandWrapPanel = dashboard
            .Descendants(Wpf + "WrapPanel")
            .Single(element => element.Attribute("Grid.Row")?.Value == "1" &&
                element.Elements(Wpf + "Button").Any(button => button.Attribute("Content")?.Value == "Connect"));

        commandWrapPanel.Elements(Wpf + "Button").Should().HaveCount(5);
        commandWrapPanel.Elements(Wpf + "Button").Select(button => button.Attribute("Margin")?.Value)
            .Should()
            .OnlyContain(margin => margin == "0,0,8,8");
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
