namespace VisionCell.Application.Motion;

public interface IMotionCommandUseCase
{
    Task<MotionCommandExecutionResult> ExecuteAsync(
        MotionCommandExecutionRequest request,
        CancellationToken cancellationToken);
}
