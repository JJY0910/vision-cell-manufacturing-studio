namespace VisionCell.Application.Teaching;

public interface ITeachingPointUseCase
{
    Task<TeachingPointSaveResult> SaveCurrentPositionAsync(
        TeachingPointSaveRequest request,
        CancellationToken cancellationToken);

    Task<TeachingPointGoToResult> GoToAsync(
        TeachingPointGoToRequest request,
        CancellationToken cancellationToken);
}
