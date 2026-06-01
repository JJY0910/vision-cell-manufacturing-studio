namespace VisionCell.Equipment.Io;

public sealed record IoBitSnapshot(
    string Name,
    string Address,
    IoBitDirection Direction,
    bool Value,
    bool IsForced);
