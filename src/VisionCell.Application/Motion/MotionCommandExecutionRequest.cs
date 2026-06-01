using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;

namespace VisionCell.Application.Motion;

public sealed record MotionCommandExecutionRequest(
    CommandKind Command,
    InterlockContext InterlockContext,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string>? Parameters = null)
{
    public IReadOnlyDictionary<string, string> GetParameters()
    {
        return Parameters ?? EmptyParameters.Value;
    }

    private static class EmptyParameters
    {
        internal static readonly IReadOnlyDictionary<string, string> Value = new Dictionary<string, string>();
    }
}
