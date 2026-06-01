using VisionCell.Core.Primitives;

namespace VisionCell.Core.Commands;

public sealed record MachineCommandRequest(
    string CommandName,
    CorrelationId CorrelationId,
    TimeSpan Timeout,
    DateTimeOffset RequestedAt,
    IReadOnlyDictionary<string, string> Parameters);
