using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;

namespace VisionCell.Application.Interlocks;

public sealed class CommandInterlockService : ICommandInterlockService
{
    public CommandAvailability Evaluate(CommandKind command, InterlockContext context)
    {
        return CommandInterlockRules.Evaluate(command, context);
    }
}
