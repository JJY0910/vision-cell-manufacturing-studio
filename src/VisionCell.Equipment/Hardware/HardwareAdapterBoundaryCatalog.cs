namespace VisionCell.Equipment.Hardware;

public enum HardwareAdapterRole
{
    MotionController = 0,
    Camera = 1,
    PlcIo = 2
}

public sealed record HardwareAdapterBoundaryRequirement
{
    public HardwareAdapterBoundaryRequirement(
        HardwareAdapterRole role,
        string interfaceName,
        string currentProvider,
        string plannedAdapterName,
        string requiredEvidence,
        string boundaryNotes)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
        {
            throw new ArgumentException("Adapter interface name is required.", nameof(interfaceName));
        }

        if (string.IsNullOrWhiteSpace(currentProvider))
        {
            throw new ArgumentException("Current provider is required.", nameof(currentProvider));
        }

        if (string.IsNullOrWhiteSpace(plannedAdapterName))
        {
            throw new ArgumentException("Planned adapter name is required.", nameof(plannedAdapterName));
        }

        if (string.IsNullOrWhiteSpace(requiredEvidence))
        {
            throw new ArgumentException("Required evidence is required.", nameof(requiredEvidence));
        }

        if (string.IsNullOrWhiteSpace(boundaryNotes))
        {
            throw new ArgumentException("Boundary notes are required.", nameof(boundaryNotes));
        }

        Role = role;
        InterfaceName = interfaceName.Trim();
        CurrentProvider = currentProvider.Trim();
        PlannedAdapterName = plannedAdapterName.Trim();
        RequiredEvidence = requiredEvidence.Trim();
        BoundaryNotes = boundaryNotes.Trim();
    }

    public HardwareAdapterRole Role { get; init; }
    public string InterfaceName { get; init; }
    public string CurrentProvider { get; init; }
    public string PlannedAdapterName { get; init; }
    public string RequiredEvidence { get; init; }
    public string BoundaryNotes { get; init; }

    public string RoleName => Role switch
    {
        HardwareAdapterRole.MotionController => "Motion Controller",
        HardwareAdapterRole.Camera => "Camera",
        HardwareAdapterRole.PlcIo => "PLC I/O",
        _ => Role.ToString()
    };
}

public static class HardwareAdapterBoundaryCatalog
{
    public static IReadOnlyList<HardwareAdapterBoundaryRequirement> RequiredAdapters { get; } =
    [
        new HardwareAdapterBoundaryRequirement(
            HardwareAdapterRole.MotionController,
            nameof(IMotionControllerAdapter),
            "VirtualEquipmentController motion simulator",
            "MotionControllerAdapter",
            "motion adapter bench validation",
            "Read axes and route Servo/Home/Jog/Move/Stop through correlated MachineCommandResult values."),
        new HardwareAdapterBoundaryRequirement(
            HardwareAdapterRole.Camera,
            nameof(ICameraAdapter),
            "VirtualCameraDevice",
            "CameraAdapter",
            "camera adapter bench validation",
            "Report camera readiness and convert vendor frame buffers into CameraFrame acquisition results."),
        new HardwareAdapterBoundaryRequirement(
            HardwareAdapterRole.PlcIo,
            nameof(IPlcIoAdapter),
            "VirtualEquipmentController simulator I/O",
            "PlcIoAdapter",
            "PLC I/O adapter bench validation",
            "Read safety/input bits and write allowed outputs only through backend interlock-checked commands.")
    ];
}
