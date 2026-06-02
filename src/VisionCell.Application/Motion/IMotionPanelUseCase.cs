using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;
using VisionCell.Equipment.Controllers;

namespace VisionCell.Application.Motion;

public interface IMotionPanelUseCase
{
    CommandAvailability GetCommandAvailability(CommandKind command, InterlockContext context);

    Task<MotionSnapshotRefreshResult> RefreshSnapshotAsync(
        TimeSpan snapshotTimeout,
        CancellationToken cancellationToken);
}

public enum MotionSnapshotRefreshStatus
{
    Refreshed,
    Cancelled,
    Timeout,
    Failed
}

public sealed record MotionSnapshotRefreshResult(
    MotionSnapshotRefreshStatus Status,
    EquipmentSnapshot? Snapshot,
    string Message)
{
    public bool HasSnapshot => Snapshot is not null;
}
