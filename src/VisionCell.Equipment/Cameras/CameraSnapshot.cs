namespace VisionCell.Equipment.Cameras;

public sealed record CameraSnapshot(bool IsReady, string Name, DateTimeOffset UpdatedAt);
