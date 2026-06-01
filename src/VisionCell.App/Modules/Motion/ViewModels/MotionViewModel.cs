using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.Application.Motion;

namespace VisionCell.App.Modules.Motion.ViewModels;

public sealed partial class MotionViewModel : ObservableObject
{
    private const int RecentHistoryLimit = 25;
    private readonly IMotionCommandHistoryReader _historyReader;

    public MotionViewModel(IMotionCommandHistoryReader historyReader)
    {
        _historyReader = historyReader ?? throw new ArgumentNullException(nameof(historyReader));
        RefreshHistoryCommand = new AsyncRelayCommand(RefreshHistoryAsync);
    }

    public ObservableCollection<MotionCommandHistoryItemViewModel> RecentCommands { get; } = new();

    public IAsyncRelayCommand RefreshHistoryCommand { get; }

    [ObservableProperty]
    private string _historyStatus = "History not loaded";

    [ObservableProperty]
    private DateTimeOffset? _lastHistoryRefreshAt;

    [ObservableProperty]
    private bool _hasHistory;

    public async Task RefreshHistoryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var records = await _historyReader.ListRecentAsync(RecentHistoryLimit, cancellationToken).ConfigureAwait(true);
            RecentCommands.Clear();
            foreach (var record in records)
            {
                RecentCommands.Add(CreateItem(record));
            }

            HasHistory = RecentCommands.Count > 0;
            LastHistoryRefreshAt = DateTimeOffset.UtcNow;
            HistoryStatus = HasHistory
                ? $"{RecentCommands.Count} command records loaded"
                : "No motion command history";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            HistoryStatus = "History refresh cancelled";
        }
        catch (Exception ex)
        {
            HistoryStatus = $"History refresh failed: {ex.Message}";
        }
    }

    private static MotionCommandHistoryItemViewModel CreateItem(MotionCommandHistoryRecord record)
    {
        return new MotionCommandHistoryItemViewModel(
            record.CommandName,
            string.IsNullOrWhiteSpace(record.AxisId) ? "-" : record.AxisId,
            record.Status,
            string.IsNullOrWhiteSpace(record.ErrorCode) ? "-" : record.ErrorCode,
            record.Message,
            record.Elapsed.TotalMilliseconds,
            record.CreatedAt,
            record.CorrelationId);
    }
}
