using VisionCell.Core.Commands;
using VisionCell.Core.Events;
using VisionCell.Core.Interlocks;
using VisionCell.Equipment.Controllers;

namespace VisionCell.Application.Equipment;

public interface IEquipmentDashboardUseCase
{
    CommandAvailability GetCommandAvailability(CommandKind command, InterlockContext context);

    Task<EquipmentDashboardSnapshotResult> RefreshAsync(
        TimeSpan snapshotTimeout,
        CancellationToken cancellationToken);

    Task<EquipmentDashboardCommandResult> ConnectAsync(
        TimeSpan commandTimeout,
        TimeSpan snapshotTimeout,
        CancellationToken cancellationToken);

    Task<EquipmentDashboardCommandResult> DisconnectAsync(
        TimeSpan commandTimeout,
        TimeSpan snapshotTimeout,
        CancellationToken cancellationToken);

    Task<EquipmentDashboardCommandResult> ExecuteCommandAsync(
        CommandKind command,
        InterlockContext context,
        TimeSpan commandTimeout,
        TimeSpan snapshotTimeout,
        CancellationToken cancellationToken);
}

public sealed record EquipmentDashboardSnapshotResult(
    EquipmentSnapshot? Snapshot,
    SystemEvent Event)
{
    public bool HasSnapshot => Snapshot is not null;
}

public sealed record EquipmentDashboardCommandResult(
    MachineCommandResult CommandResult,
    SystemEvent CommandEvent,
    EquipmentDashboardSnapshotResult SnapshotResult);
