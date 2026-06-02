using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.Application.Alarms;
using VisionCell.Core.Alarms;

namespace VisionCell.App.Modules.Alarm.ViewModels;

public sealed partial class AlarmViewModel : ObservableObject
{
    private readonly IAlarmCenterUseCase _alarmCenterUseCase;

    public AlarmViewModel(IAlarmCenterUseCase alarmCenterUseCase)
    {
        _alarmCenterUseCase = alarmCenterUseCase ?? throw new ArgumentNullException(nameof(alarmCenterUseCase));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        AcknowledgeCommand = new AsyncRelayCommand(AcknowledgeSelectedAsync, CanAcknowledgeSelected);
    }

    public ObservableCollection<AlarmItemViewModel> Alarms { get; } = new();
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AcknowledgeCommand { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Alarm records not loaded";

    [ObservableProperty]
    private AlarmItemViewModel? _selectedAlarm;

    [ObservableProperty]
    private string _actionMemoText = string.Empty;

    [ObservableProperty]
    private string _lastRefreshText = "-";

    public int ActiveCount => Alarms.Count(alarm => !alarm.Alarm.IsAcknowledged);
    public int AcknowledgedCount => Alarms.Count(alarm => alarm.Alarm.IsAcknowledged);
    public int CriticalCount => Alarms.Count(alarm => alarm.Alarm.Severity == EquipmentAlarmSeverity.Critical);

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var previousId = SelectedAlarm?.Id;
        IsBusy = true;
        try
        {
            var alarms = await _alarmCenterUseCase.ListRecentAsync(100, cancellationToken).ConfigureAwait(true);
            Alarms.Clear();
            foreach (var alarm in alarms)
            {
                Alarms.Add(new AlarmItemViewModel(alarm));
            }

            SelectedAlarm = SelectAlarm(previousId);
            LastRefreshText = DateTimeOffset.Now.ToString("HH:mm:ss");
            StatusText = Alarms.Count == 0
                ? "No alarm records"
                : $"{Alarms.Count} alarm records loaded";
            NotifyCountsChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Alarm refresh cancelled";
        }
        catch (Exception ex)
        {
            Alarms.Clear();
            SelectedAlarm = null;
            StatusText = $"Alarm refresh failed: {ex.Message}";
            NotifyCountsChanged();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AcknowledgeSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedAlarm is null)
        {
            StatusText = "Select an alarm to acknowledge";
            return;
        }

        IsBusy = true;
        try
        {
            var selectedId = SelectedAlarm.Id;
            var selectedCode = SelectedAlarm.Code;
            await _alarmCenterUseCase
                .AcknowledgeAsync(selectedId, ActionMemoText, cancellationToken)
                .ConfigureAwait(true);
            ActionMemoText = string.Empty;
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            SelectedAlarm = Alarms.FirstOrDefault(alarm => alarm.Id == selectedId) ?? SelectedAlarm;
            StatusText = $"Alarm {selectedCode} acknowledged";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Alarm acknowledge cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Alarm acknowledge failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        AcknowledgeCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedAlarmChanged(AlarmItemViewModel? value)
    {
        ActionMemoText = value?.Alarm.ActionMemo ?? string.Empty;
        AcknowledgeCommand.NotifyCanExecuteChanged();
    }

    private bool CanAcknowledgeSelected()
    {
        return !IsBusy && SelectedAlarm is not null && !SelectedAlarm.Alarm.IsAcknowledged;
    }

    private AlarmItemViewModel? SelectAlarm(Guid? preferredId)
    {
        if (preferredId is not null)
        {
            var match = Alarms.FirstOrDefault(alarm => alarm.Id == preferredId);
            if (match is not null)
            {
                return match;
            }
        }

        return Alarms.FirstOrDefault();
    }

    private void NotifyCountsChanged()
    {
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(AcknowledgedCount));
        OnPropertyChanged(nameof(CriticalCount));
    }
}
