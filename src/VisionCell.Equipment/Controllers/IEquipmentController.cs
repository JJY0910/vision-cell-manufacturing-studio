using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;

namespace VisionCell.Equipment.Controllers;

public interface IEquipmentController
{
    Task<EquipmentSnapshot> GetSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken);
    Task<MachineCommandResult> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken);
    Task<MachineCommandResult> DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken);
    CommandAvailability GetCommandAvailability(CommandKind command, InterlockContext context);
    Task<MachineCommandResult> ExecuteCommandAsync(CommandKind command, InterlockContext context, TimeSpan timeout, CancellationToken cancellationToken);
}
