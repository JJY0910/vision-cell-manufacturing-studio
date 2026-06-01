namespace VisionCell.Application.Inspection;

public interface IInspectionRunUseCase
{
    Task<InspectionRunResult> RunAsync(
        InspectionRunRequest request,
        IProgress<InspectionSequenceStepRecord>? progress,
        CancellationToken cancellationToken);
}
