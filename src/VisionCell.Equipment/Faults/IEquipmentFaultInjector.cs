using VisionCell.Core.Commands;

namespace VisionCell.Equipment.Faults;

public interface IEquipmentFaultInjector
{
    Task<MachineCommandResult> ApplyFaultAsync(
        EquipmentFaultInjectionRequest request,
        CancellationToken cancellationToken);
}
