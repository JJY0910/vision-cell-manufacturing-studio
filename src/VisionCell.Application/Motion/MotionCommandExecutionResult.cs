using VisionCell.Core.Commands;

namespace VisionCell.Application.Motion;

public sealed record MotionCommandExecutionResult(
    MachineCommandRequest Request,
    MachineCommandResult CommandResult);
