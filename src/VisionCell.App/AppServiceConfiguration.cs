using System.IO;
using Microsoft.Extensions.DependencyInjection;
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
using VisionCell.Persistence.Alarms;
using VisionCell.Persistence.Equipment;
using VisionCell.Persistence.Inspection;
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
        string recipeRootPath,
        string? artifactRootPath = null,
        EquipmentRuntimeProfile? equipmentRuntimeProfile = null)
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

        var resolvedArtifactRootPath = string.IsNullOrWhiteSpace(artifactRootPath)
            ? Path.Combine(Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory, "inspection-artifacts")
            : artifactRootPath;
        var resolvedEquipmentRuntimeProfile = equipmentRuntimeProfile ?? EquipmentRuntimeProfile.Virtual;
        if (resolvedEquipmentRuntimeProfile.IsRealHardware)
        {
            var readiness = RealHardwareReadinessGate.Evaluate(RealHardwareReadinessEvidence.Unvalidated);
            throw new NotSupportedException(
                $"Real hardware runtime profile is not implemented or validated. Missing evidence: {readiness.FormatMissingEvidence()}. Use the virtual profile and follow docs/HARDWARE_INTEGRATION_PLAN.md before enabling real equipment.");
        }

        services.AddSingleton(resolvedEquipmentRuntimeProfile);
        services.AddSingleton<VirtualEquipmentController>();
        services.AddSingleton<IEquipmentController>(provider => provider.GetRequiredService<VirtualEquipmentController>());
        services.AddSingleton<IEquipmentFaultInjector>(provider => provider.GetRequiredService<VirtualEquipmentController>());
        services.AddSingleton<ICameraDevice, VirtualCameraDevice>();
        services.AddSingleton<IVisionInspectionEngine, Deterministic2DInspectionEngine>();
        services.AddSingleton<IHeightMapInspectionEngine, DeterministicHeightMapInspectionEngine>();
        services.AddSingleton<SyntheticHeightMapFactory>();
        services.AddSingleton<ICommandInterlockService, CommandInterlockService>();
        services.AddSingleton<RecipeValidator>();
        services.AddSingleton(_ => new SqliteConnectionFactory(databasePath));
        services.AddSingleton<SqliteSchemaInitializer>();
        services.AddSingleton<SqliteEquipmentAlarmRepository>();
        services.AddSingleton<SqliteEquipmentIoTransitionRepository>();
        services.AddSingleton<SqliteMotionCommandHistoryRepository>();
        services.AddSingleton<SqliteInspectionResultRepository>();
        services.AddSingleton<SqliteInspectionReinspectComparisonRepository>();
        services.AddSingleton(_ => new FileSystemInspectionArtifactWriter(resolvedArtifactRootPath));
        services.AddSingleton<SqliteRecipeIndexRepository>();
        services.AddSingleton<SqliteTeachingPointRepository>();
        services.AddSingleton<SqliteTeachingHistoryRepository>();
        services.AddSingleton<IEquipmentAlarmRepository>(provider => provider.GetRequiredService<SqliteEquipmentAlarmRepository>());
        services.AddSingleton<IEquipmentAlarmRecorder, EquipmentAlarmRecorder>();
        services.AddSingleton<IEquipmentIoTransitionRepository>(provider => provider.GetRequiredService<SqliteEquipmentIoTransitionRepository>());
        services.AddSingleton<IAlarmCenterUseCase, AlarmCenterUseCase>();
        services.AddSingleton<IMotionCommandHistoryRepository>(provider => provider.GetRequiredService<SqliteMotionCommandHistoryRepository>());
        services.AddSingleton<IMotionCommandHistoryReader>(provider => provider.GetRequiredService<SqliteMotionCommandHistoryRepository>());
        services.AddSingleton<IInspectionArtifactWriter>(provider => provider.GetRequiredService<FileSystemInspectionArtifactWriter>());
        services.AddSingleton<IInspectionArtifactReader>(provider => provider.GetRequiredService<FileSystemInspectionArtifactWriter>());
        services.AddSingleton<IInspectionResultRepository>(provider => provider.GetRequiredService<SqliteInspectionResultRepository>());
        services.AddSingleton<IInspectionResultReader>(provider => provider.GetRequiredService<SqliteInspectionResultRepository>());
        services.AddSingleton<IInspectionReinspectComparisonRepository>(provider => provider.GetRequiredService<SqliteInspectionReinspectComparisonRepository>());
        services.AddSingleton<IInspectionReinspectComparisonReader>(provider => provider.GetRequiredService<SqliteInspectionReinspectComparisonRepository>());
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
        services.AddSingleton<IMotionPanelUseCase, MotionPanelUseCase>();
        services.AddSingleton<IEquipmentDashboardUseCase, EquipmentDashboardUseCase>();
        services.AddSingleton<IEquipmentFaultInjectionUseCase, EquipmentFaultInjectionUseCase>();
        services.AddSingleton<IInspectionRunUseCase, InspectionRunUseCase>();
        services.AddSingleton<IInspectionReinspectUseCase>(provider => new InspectionReinspectUseCase(
            provider.GetRequiredService<IInspectionReinspectComparisonRepository>()));
        services.AddSingleton<ITeachingPointUseCase, TeachingPointUseCase>();
        services.AddSingleton<IUserConfirmationService, MessageBoxConfirmationService>();
        services.AddSingleton<IArtifactViewerService, ShellArtifactViewerService>();
        services.AddSingleton<AlarmViewModel>();
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
