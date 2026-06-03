using VisionCell.Equipment.Io;

namespace VisionCell.Application.Equipment;

public sealed class NoopEquipmentIoTransitionRepository : IEquipmentIoTransitionRepository
{
    public static NoopEquipmentIoTransitionRepository Instance { get; } = new();

    private NoopEquipmentIoTransitionRepository()
    {
    }

    public Task SaveAsync(IoTransitionRecord transition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transition);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IoTransitionRecord>> ListRecentAsync(int limit, CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be greater than zero.");
        }

        return Task.FromResult<IReadOnlyList<IoTransitionRecord>>(Array.Empty<IoTransitionRecord>());
    }
}
