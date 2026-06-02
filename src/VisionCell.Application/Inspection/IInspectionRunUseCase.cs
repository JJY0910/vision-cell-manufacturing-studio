using VisionCell.Application.Recipes;

namespace VisionCell.Application.Inspection;

public interface IInspectionRunUseCase
{
    Task<ActiveRecipeContextResult> PrecheckActiveRecipeAsync(
        CancellationToken cancellationToken);

    Task<InspectionRunResult> RunAsync(
        InspectionRunRequest request,
        IProgress<InspectionSequenceStepRecord>? progress,
        CancellationToken cancellationToken);
}
