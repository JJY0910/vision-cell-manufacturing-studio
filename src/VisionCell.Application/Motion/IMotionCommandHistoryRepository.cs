using VisionCell.Core.Commands;

namespace VisionCell.Application.Motion;

public interface IMotionCommandHistoryRepository
{
    Task SaveAsync(MotionCommandHistoryEntry entry, CancellationToken cancellationToken);
}

public sealed record MotionCommandHistoryEntry(
    Guid Id,
    MachineCommandRequest Request,
    MachineCommandResult CommandResult,
    DateTimeOffset CreatedAt);
