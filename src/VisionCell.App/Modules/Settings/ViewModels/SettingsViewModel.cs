using VisionCell.App.Configuration;
using VisionCell.Equipment.Hardware;

namespace VisionCell.App.Modules.Settings.ViewModels;

public sealed class SettingsViewModel
{
    public SettingsViewModel()
    {
        var readiness = RealHardwareReadinessGate.Evaluate(RealHardwareReadinessEvidence.Unvalidated);

        RuntimeScopeItems =
        [
            new SettingsScopeItemViewModel(
                "Equipment Mode",
                "VirtualEquipmentController",
                "RealHardware profile selection remains blocked until adapter implementation and bench evidence are complete."),
            new SettingsScopeItemViewModel(
                "Camera Mode",
                "Virtual camera only",
                "No real camera trigger, acquisition, or calibration path is implemented or validated."),
            new SettingsScopeItemViewModel(
                "I/O Mode",
                "Simulator I/O only",
                "Fault injection is simulator-only; no PLC scan polling or output-write path has been validated."),
            new SettingsScopeItemViewModel(
                "Validation Scope",
                "docs/VALIDATION_SCOPE.md",
                "Verified and unverified boundaries must stay visible before any real equipment claim.")
        ];

        ReadinessGateItems = readiness.MissingEvidence
            .Select(evidence => new SettingsScopeItemViewModel(
                evidence,
                "Missing",
                "Required before the RealHardware runtime profile can be enabled."))
            .ToArray();

        AdapterBoundaryItems = HardwareAdapterBoundaryCatalog.RequiredAdapters
            .Select(adapter => new SettingsAdapterBoundaryItemViewModel(
                adapter.RoleName,
                $"{adapter.InterfaceName} -> {adapter.PlannedAdapterName}",
                adapter.CurrentProvider,
                $"Missing: {adapter.RequiredEvidence}",
                adapter.BoundaryNotes))
            .ToArray();

        ReadinessSummary = readiness.CanEnableRealHardware
            ? "RealHardware profile can be enabled after reviewed evidence."
            : $"RealHardware profile remains blocked: {readiness.MissingEvidence.Count} missing evidence items.";
    }

    public string RuntimeStatusText { get; } =
        "Runtime settings are read-only; RealHardware remains blocked until validation evidence is complete.";

    public IReadOnlyList<SettingsScopeItemViewModel> RuntimeScopeItems { get; }

    public string ReadinessSummary { get; }

    public IReadOnlyList<SettingsScopeItemViewModel> ReadinessGateItems { get; }

    public IReadOnlyList<SettingsAdapterBoundaryItemViewModel> AdapterBoundaryItems { get; }
}

public sealed record SettingsScopeItemViewModel(
    string Name,
    string Value,
    string Detail);

public sealed record SettingsAdapterBoundaryItemViewModel(
    string Name,
    string Contract,
    string CurrentScope,
    string Readiness,
    string Detail);
