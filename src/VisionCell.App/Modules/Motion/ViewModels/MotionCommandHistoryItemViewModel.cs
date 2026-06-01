using VisionCell.Core.Commands;

namespace VisionCell.App.Modules.Motion.ViewModels;

public sealed record MotionCommandHistoryItemViewModel(
    string CommandName,
    string AxisId,
    CommandStatus Status,
    string ErrorCode,
    string Message,
    double ElapsedMs,
    DateTimeOffset CreatedAt,
    string CorrelationId)
{
    public string StatusText => Status.ToString();
    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string ElapsedText => $"{ElapsedMs:0} ms";
    public string CorrelationShort => CorrelationId.Length <= 12 ? CorrelationId : CorrelationId[..12];
}
