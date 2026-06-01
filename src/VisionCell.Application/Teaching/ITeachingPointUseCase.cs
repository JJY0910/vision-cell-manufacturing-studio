using VisionCell.Motion.Teaching;

namespace VisionCell.Application.Teaching;

public interface ITeachingPointUseCase
{
    Task<IReadOnlyList<TeachingPoint>> ListAsync(
        int limit,
        CancellationToken cancellationToken);

    Task<TeachingPointSaveResult> SaveCurrentPositionAsync(
        TeachingPointSaveRequest request,
        CancellationToken cancellationToken);

    Task<TeachingPointSaveResult> UpdateAsync(
        TeachingPointUpdateRequest request,
        CancellationToken cancellationToken);

    Task<TeachingPointDeleteResult> DeleteAsync(
        TeachingPointDeleteRequest request,
        CancellationToken cancellationToken);

    Task<TeachingPointGoToResult> GoToAsync(
        TeachingPointGoToRequest request,
        CancellationToken cancellationToken);
}
