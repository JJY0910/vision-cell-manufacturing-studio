namespace VisionCell.Application.Teaching;

public interface ITeachingHistoryRepository
{
    Task SaveAsync(TeachingHistoryEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyList<TeachingHistoryEntry>> ListByPointAsync(
        Guid teachingPointId,
        int limit,
        CancellationToken cancellationToken);
}
