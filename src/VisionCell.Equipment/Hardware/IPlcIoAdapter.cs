using VisionCell.Core.Commands;
using VisionCell.Equipment.Io;

namespace VisionCell.Equipment.Hardware;

public interface IPlcIoAdapter : IHardwareAdapter
{
    Task<IoSnapshot> ReadIoAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken);

    Task<MachineCommandResult> WriteOutputAsync(
        string bitName,
        bool value,
        MachineCommandRequest request,
        CancellationToken cancellationToken);
}
