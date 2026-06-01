using VisionCell.Application.Recipes;
using VisionCell.Application.Motion;
using VisionCell.Core.Commands;
using VisionCell.Equipment.Cameras;
using VisionCell.Vision.Inspection;

namespace VisionCell.Application.Inspection;

public sealed record InspectionRunResult(
    InspectionRunStatus Status,
    string Message,
    RecipeIndexEntry? Recipe,
    MachineCommandRequest? Request,
    MachineCommandResult? CommandResult,
    MotionCommandExecutionResult? MoveToCameraResult,
    CameraGrabResult? CameraGrabResult,
    VisionInspectionResult? VisionResult,
    VisionInspectionResult? HeightMapResult,
    Guid? PersistedResultId,
    IReadOnlyList<InspectionSequenceStepRecord> Steps,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
{
    public bool IsAccepted => Status == InspectionRunStatus.Accepted;
}
