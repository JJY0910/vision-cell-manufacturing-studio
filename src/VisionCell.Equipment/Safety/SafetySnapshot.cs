namespace VisionCell.Equipment.Safety;

public sealed record SafetySnapshot(
    bool DoorClosed,
    bool EmergencyStopActive,
    bool AirPressureOk,
    bool VacuumOn,
    bool ServoEnabled);
