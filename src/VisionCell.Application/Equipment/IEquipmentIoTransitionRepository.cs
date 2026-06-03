using VisionCell.Equipment.Io;

namespace VisionCell.Application.Equipment;

public interface IEquipmentIoTransitionRepository
{
    Task SaveAsync(IoTransitionRecord transition, CancellationToken cancellationToken);

    Task<IReadOnlyList<IoTransitionRecord>> ListRecentAsync(int limit, CancellationToken cancellationToken);
}
