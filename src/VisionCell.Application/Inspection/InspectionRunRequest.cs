namespace VisionCell.Application.Inspection;

public sealed record InspectionRunRequest(
    TimeSpan SnapshotTimeout,
    TimeSpan CommandTimeout)
{
    public TimeSpan GrabTimeout { get; init; } = CommandTimeout;
}
