using VisionCell.Motion.Teaching;

namespace VisionCell.Application.Teaching;

public interface ITeachingPointRepository
{
    Task<IReadOnlyList<TeachingPoint>> ListAsync(int limit, CancellationToken cancellationToken);

    Task<TeachingPoint?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<TeachingPoint?> FindByNameAsync(string name, CancellationToken cancellationToken);

    Task SaveAsync(TeachingPoint point, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
