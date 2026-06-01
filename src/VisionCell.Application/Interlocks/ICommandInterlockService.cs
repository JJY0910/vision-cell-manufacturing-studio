using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;

namespace VisionCell.Application.Interlocks;

public interface ICommandInterlockService
{
    CommandAvailability Evaluate(CommandKind command, InterlockContext context);
}
