using VisionCell.Core.Commands;

namespace VisionCell.Core.Interlocks;

public sealed record CommandAvailability(
    CommandKind Command,
    bool IsEnabled,
    string DisabledReason,
    IReadOnlyList<InterlockViolation> Violations)
{
    public static CommandAvailability Available(CommandKind command)
    {
        return new CommandAvailability(command, true, string.Empty, Array.Empty<InterlockViolation>());
    }

    public static CommandAvailability Blocked(CommandKind command, IReadOnlyList<InterlockViolation> violations)
    {
        var reason = violations.Count == 0 ? "Command is not available." : string.Join(" ", violations.Select(violation => violation.Message));
        return new CommandAvailability(command, false, reason, violations);
    }
}
