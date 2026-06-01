namespace VisionCell.Application.Motion;

public interface IMotionCommandHistoryReader
{
    Task<IReadOnlyList<MotionCommandHistoryRecord>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken);
}
