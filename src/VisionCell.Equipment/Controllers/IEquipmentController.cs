using VisionCell.Core.Commands;

namespace VisionCell.Equipment.Controllers;

public interface IEquipmentController
{
    Task<EquipmentSnapshot> GetSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken);
    Task<MachineCommandResult> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken);
    Task<MachineCommandResult> DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
