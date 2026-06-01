using Microsoft.Extensions.DependencyInjection;
using VisionCell.Application.Interlocks;
using VisionCell.Application.Inspection;
using VisionCell.Application.Motion;
using VisionCell.Application.Recipes;
using VisionCell.Application.Teaching;
using VisionCell.App.Interaction;
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
using VisionCell.Persistence.Motion;
using VisionCell.Persistence.Recipes;
using VisionCell.Persistence.Sqlite;
using VisionCell.Persistence.Teaching;
using VisionCell.Simulator;
using VisionCell.Vision.Inspection;

namespace VisionCell.App;

public static class AppServiceConfiguration
{
    public static IServiceCollection AddVisionCellAppServices(
        this IServiceCollection services,
        string databasePath,
        string recipeRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        if (string.IsNullOrWhiteSpace(recipeRootPath))
        {
            throw new ArgumentException("Recipe root path is required.", nameof(recipeRootPath));
        }

        services.AddSingleton<IEquipmentController, VirtualEquipmentController>();
        services.AddSingleton<ICameraDevice, VirtualCameraDevice>();
        services.AddSingleton<IVisionInspectionEngine, Deterministic2DInspectionEngine>();
        services.AddSingleton<IHeightMapInspectionEngine, DeterministicHeightMapInspectionEngine>();
        services.AddSingleton<SyntheticHeightMapFactory>();
        services.AddSingleton<ICommandInterlockService, CommandInterlockService>();
        services.AddSingleton<RecipeValidator>();
        services.AddSingleton(_ => new SqliteConnectionFactory(databasePath));
        services.AddSingleton<SqliteSchemaInitializer>();
        services.AddSingleton<SqliteMotionCommandHistoryRepository>();
        services.AddSingleton<SqliteRecipeIndexRepository>();
        services.AddSingleton<SqliteTeachingPointRepository>();
        services.AddSingleton<SqliteTeachingHistoryRepository>();
        services.AddSingleton<IMotionCommandHistoryRepository>(provider => provider.GetRequiredService<SqliteMotionCommandHistoryRepository>());
        services.AddSingleton<IMotionCommandHistoryReader>(provider => provider.GetRequiredService<SqliteMotionCommandHistoryRepository>());
        services.AddSingleton<IRecipeIndexRepository>(provider => provider.GetRequiredService<SqliteRecipeIndexRepository>());
        services.AddSingleton<IRecipeDocumentStore>(provider => new JsonRecipeDocumentStore(
            recipeRootPath,
            provider.GetRequiredService<RecipeValidator>()));
        services.AddSingleton<IRecipeLibraryUseCase>(provider => new RecipeLibraryUseCase(
            provider.GetRequiredService<IRecipeDocumentStore>(),
            provider.GetRequiredService<IRecipeIndexRepository>(),
            provider.GetRequiredService<RecipeValidator>()));
        services.AddSingleton<IActiveRecipeContext, ActiveRecipeContext>();
        services.AddSingleton<ITeachingPointRepository>(provider => provider.GetRequiredService<SqliteTeachingPointRepository>());
        services.AddSingleton<ITeachingHistoryRepository>(provider => provider.GetRequiredService<SqliteTeachingHistoryRepository>());
        services.AddSingleton<IMotionCommandUseCase, MotionCommandUseCase>();
        services.AddSingleton<IInspectionRunUseCase, InspectionRunUseCase>();
        services.AddSingleton<ITeachingPointUseCase, TeachingPointUseCase>();
        services.AddSingleton<IUserConfirmationService, MessageBoxConfirmationService>();
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

        return services;
    }
}
