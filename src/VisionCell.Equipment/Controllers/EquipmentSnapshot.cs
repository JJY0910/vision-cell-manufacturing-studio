using VisionCell.Core.Primitives;
using VisionCell.Equipment.Alarms;
using VisionCell.Equipment.Cameras;
using VisionCell.Equipment.Io;
using VisionCell.Equipment.Safety;
using VisionCell.Motion.Axes;

namespace VisionCell.Equipment.Controllers;

public sealed record EquipmentSnapshot(
    bool IsConnected,
    MachineMode Mode,
    SafetySnapshot Safety,
    IReadOnlyList<AxisSnapshot> Axes,
    IoSnapshot Io,
    CameraSnapshot Camera,
    AlarmSnapshot? Alarm,
    DateTimeOffset Timestamp);
