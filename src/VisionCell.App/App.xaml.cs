using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VisionCell.App.Shell;

namespace VisionCell.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var shellWindow = _serviceProvider.GetRequiredService<ShellWindow>();
        shellWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddVisionCellAppServices(GetDefaultDatabasePath(), GetDefaultRecipeRootPath());
    }

    private static string GetDefaultDatabasePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "local-data", "visioncell.db");
    }

    private static string GetDefaultRecipeRootPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "local-data", "recipes");
    }
}
