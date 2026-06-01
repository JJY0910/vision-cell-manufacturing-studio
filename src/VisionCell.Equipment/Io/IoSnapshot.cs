namespace VisionCell.Equipment.Io;

public sealed record IoSnapshot(IReadOnlyList<IoBitSnapshot> Bits, DateTimeOffset UpdatedAt);
