namespace VisionCell.App.Configuration;

public sealed record RealHardwareReadinessEvidence(
    bool RealEquipmentControllerImplemented,
    bool MotionAdapterBenchValidated,
    bool CameraAdapterBenchValidated,
    bool PlcIoAdapterBenchValidated,
    bool SafetyResetValidated,
    bool HardwareIntegrationPlanReviewed)
{
    public static RealHardwareReadinessEvidence Unvalidated { get; } = new(
        RealEquipmentControllerImplemented: false,
        MotionAdapterBenchValidated: false,
        CameraAdapterBenchValidated: false,
        PlcIoAdapterBenchValidated: false,
        SafetyResetValidated: false,
        HardwareIntegrationPlanReviewed: false);
}

public sealed record RealHardwareReadinessReport(
    bool CanEnableRealHardware,
    IReadOnlyList<string> MissingEvidence)
{
    public string FormatMissingEvidence()
    {
        return MissingEvidence.Count == 0
            ? "none"
            : string.Join("; ", MissingEvidence);
    }
}

public static class RealHardwareReadinessGate
{
    public static RealHardwareReadinessReport Evaluate(RealHardwareReadinessEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var missing = new List<string>();
        AddIfMissing(missing, evidence.RealEquipmentControllerImplemented, "RealEquipmentController implementation");
        AddIfMissing(missing, evidence.MotionAdapterBenchValidated, "motion adapter bench validation");
        AddIfMissing(missing, evidence.CameraAdapterBenchValidated, "camera adapter bench validation");
        AddIfMissing(missing, evidence.PlcIoAdapterBenchValidated, "PLC I/O adapter bench validation");
        AddIfMissing(missing, evidence.SafetyResetValidated, "safety reset validation");
        AddIfMissing(missing, evidence.HardwareIntegrationPlanReviewed, "HARDWARE_INTEGRATION_PLAN review evidence");

        return new RealHardwareReadinessReport(
            missing.Count == 0,
            missing);
    }

    private static void AddIfMissing(List<string> missing, bool isPresent, string label)
    {
        if (!isPresent)
        {
            missing.Add(label);
        }
    }
}
