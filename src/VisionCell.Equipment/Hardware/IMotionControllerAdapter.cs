using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;
using VisionCell.Motion.Axes;

namespace VisionCell.Equipment.Hardware;

public interface IMotionControllerAdapter : IHardwareAdapter
{
    Task<IReadOnlyList<AxisSnapshot>> ReadAxesAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken);

    Task<MachineCommandResult> ExecuteMotionAsync(
        CommandKind command,
        InterlockContext context,
        MachineCommandRequest request,
        CancellationToken cancellationToken);
}
