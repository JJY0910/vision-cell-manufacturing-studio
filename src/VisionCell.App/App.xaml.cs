using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VisionCell.Application.Interlocks;
using VisionCell.Application.Motion;
using VisionCell.Application.Teaching;
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
using VisionCell.Equipment.Controllers;
using VisionCell.Persistence.Motion;
using VisionCell.Persistence.Sqlite;
using VisionCell.Persistence.Teaching;
using VisionCell.Simulator;

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
        services.AddSingleton<IEquipmentController, VirtualEquipmentController>();
        services.AddSingleton<ICommandInterlockService, CommandInterlockService>();
        services.AddSingleton(_ => new SqliteConnectionFactory(GetDefaultDatabasePath()));
        services.AddSingleton<SqliteSchemaInitializer>();
        services.AddSingleton<SqliteMotionCommandHistoryRepository>();
        services.AddSingleton<SqliteTeachingPointRepository>();
        services.AddSingleton<SqliteTeachingHistoryRepository>();
        services.AddSingleton<IMotionCommandHistoryRepository>(provider => provider.GetRequiredService<SqliteMotionCommandHistoryRepository>());
        services.AddSingleton<IMotionCommandHistoryReader>(provider => provider.GetRequiredService<SqliteMotionCommandHistoryRepository>());
        services.AddSingleton<ITeachingPointRepository>(provider => provider.GetRequiredService<SqliteTeachingPointRepository>());
        services.AddSingleton<ITeachingHistoryRepository>(provider => provider.GetRequiredService<SqliteTeachingHistoryRepository>());
        services.AddSingleton<IMotionCommandUseCase, MotionCommandUseCase>();
        services.AddSingleton<ITeachingPointUseCase, TeachingPointUseCase>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<EquipmentViewModel>();
        services.AddSingleton<MotionViewModel>();
        services.AddSingleton<TeachingViewModel>();
        services.AddSingleton<RecipeViewModel>();
        services.AddSingleton<InspectionViewModel>();
        services.AddSingleton<OfflineDebugViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton(provider => new ShellWindow
        {
            DataContext = provider.GetRequiredService<ShellViewModel>()
        });
    }

    private static string GetDefaultDatabasePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "local-data", "visioncell.db");
    }
}
