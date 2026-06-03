namespace VisionCell.App.Configuration;

public enum EquipmentRuntimeMode
{
    Virtual = 0,
    RealHardware = 1
}

public sealed record EquipmentRuntimeProfile
{
    public static EquipmentRuntimeProfile Virtual { get; } = new(
        EquipmentRuntimeMode.Virtual,
        "VirtualEquipmentController",
        "Simulator-only runtime using virtual motion, camera, I/O, and alarm paths.");

    public EquipmentRuntimeProfile(
        EquipmentRuntimeMode mode,
        string profileName,
        string validationScope)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Equipment profile name is required.", nameof(profileName));
        }

        if (string.IsNullOrWhiteSpace(validationScope))
        {
            throw new ArgumentException("Equipment profile validation scope is required.", nameof(validationScope));
        }

        Mode = mode;
        ProfileName = profileName.Trim();
        ValidationScope = validationScope.Trim();
    }

    public EquipmentRuntimeMode Mode { get; init; }
    public string ProfileName { get; init; }
    public string ValidationScope { get; init; }
    public bool IsRealHardware => Mode == EquipmentRuntimeMode.RealHardware;
}
