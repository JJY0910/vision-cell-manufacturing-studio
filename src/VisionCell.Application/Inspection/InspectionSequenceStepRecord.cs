namespace VisionCell.Application.Inspection;

public sealed record InspectionSequenceStepRecord(
    string Name,
    InspectionSequenceStepStatus Status,
    string Message,
    TimeSpan? Elapsed);
