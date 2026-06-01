using VisionCell.Application.Recipes;
using VisionCell.Core.Commands;
using VisionCell.Equipment.Cameras;

namespace VisionCell.Application.Inspection;

public sealed record InspectionRunResult(
    InspectionRunStatus Status,
    string Message,
    RecipeIndexEntry? Recipe,
    MachineCommandRequest? Request,
    MachineCommandResult? CommandResult,
    CameraGrabResult? CameraGrabResult,
    IReadOnlyList<InspectionSequenceStepRecord> Steps,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
{
    public bool IsAccepted => Status == InspectionRunStatus.Accepted;
}
