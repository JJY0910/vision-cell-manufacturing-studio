using VisionCell.Application.Recipes;
using VisionCell.Core.Commands;

namespace VisionCell.Application.Inspection;

public sealed record InspectionRunResult(
    InspectionRunStatus Status,
    string Message,
    RecipeIndexEntry? Recipe,
    MachineCommandRequest? Request,
    MachineCommandResult? CommandResult,
    IReadOnlyList<InspectionSequenceStepRecord> Steps,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
{
    public bool IsAccepted => Status == InspectionRunStatus.Accepted;
}
