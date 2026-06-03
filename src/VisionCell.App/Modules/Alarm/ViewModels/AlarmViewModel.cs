using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.App.Shared.ViewModels;
using VisionCell.Application.Alarms;
using VisionCell.Core.Alarms;

namespace VisionCell.App.Modules.Alarm.ViewModels;

public sealed partial class AlarmViewModel : ObservableObject
{
    private const string AllFilter = "All";
    private readonly IAlarmCenterUseCase _alarmCenterUseCase;
    private readonly List<EquipmentAlarm> _allAlarmRecords = new();

    public AlarmViewModel(IAlarmCenterUseCase alarmCenterUseCase)
    {
        _alarmCenterUseCase = alarmCenterUseCase ?? throw new ArgumentNullException(nameof(alarmCenterUseCase));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        AcknowledgeCommand = new AsyncRelayCommand(AcknowledgeSelectedAsync, CanAcknowledgeSelected);
    }

    public ObservableCollection<AlarmItemViewModel> Alarms { get; } = new();
    public IReadOnlyList<string> SeverityFilterOptions { get; } =
        [AllFilter, nameof(EquipmentAlarmSeverity.Warning), nameof(EquipmentAlarmSeverity.Error), nameof(EquipmentAlarmSeverity.Critical)];

    public IReadOnlyList<string> AreaFilterOptions { get; } =
        [AllFilter, nameof(EquipmentArea.Equipment), nameof(EquipmentArea.Safety), nameof(EquipmentArea.Motion), nameof(EquipmentArea.Camera), nameof(EquipmentArea.Inspection), nameof(EquipmentArea.Database)];

    public IReadOnlyList<AlarmRecoveryBoundaryItemViewModel> RecoveryBoundaryItems { get; } =
    [
        new(
            "Operator acknowledgement",
            "Available",
            "Acknowledge stores acknowledgedAt and actionMemo as operator recovery history."),
        new(
            "Recovery memo",
            "Available",
            "Memo text is persisted only through the Alarm Center acknowledgement path."),
        new(
            "Hardware reset",
            "Not connected",
            "AlarmView acknowledgement does not send controller, PLC, or safety reset commands."),
        new(
            "PLC/vendor alarm source",
            "Not validated",
            "Alarm rows currently come from simulator/Application failure paths and the error-code catalog."),
        new(
            "Safety relay confirmation",
            "Not validated",
            "No physical safety relay reset or field acknowledgement evidence exists in this environment.")
    ];

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AcknowledgeCommand { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Alarm records not loaded";

    [ObservableProperty]
    private string? _alertMessage;

    [ObservableProperty]
    private AlarmItemViewModel? _selectedAlarm;

    [ObservableProperty]
    private string _actionMemoText = string.Empty;

    [ObservableProperty]
    private string _lastRefreshText = "-";

    [ObservableProperty]
    private bool _showActiveOnly;

    [ObservableProperty]
    private string _selectedSeverityFilter = AllFilter;

    [ObservableProperty]
    private string _selectedAreaFilter = AllFilter;

    public int TotalAlarmCount => _allAlarmRecords.Count;
    public int ActiveCount => Alarms.Count(alarm => !alarm.Alarm.IsAcknowledged);
    public int AcknowledgedCount => Alarms.Count(alarm => alarm.Alarm.IsAcknowledged);
    public int CriticalCount => Alarms.Count(alarm => alarm.Alarm.Severity == EquipmentAlarmSeverity.Critical);
    public bool HasAlarms => Alarms.Count > 0;
    public bool HasAlert => !string.IsNullOrWhiteSpace(AlertMessage);
    public bool IsActionMemoEditable => !IsBusy && SelectedAlarm is not null && !SelectedAlarm.Alarm.IsAcknowledged;
    public string TotalAlarmDetail => $"{TotalAlarmCount} total records";
    public string FilterSummaryText
    {
        get
        {
            var filters = new List<string>();
            if (ShowActiveOnly)
            {
                filters.Add("Active only");
            }

            if (!string.Equals(SelectedSeverityFilter, AllFilter, StringComparison.Ordinal))
            {
                filters.Add($"Severity {SelectedSeverityFilter}");
            }

            if (!string.Equals(SelectedAreaFilter, AllFilter, StringComparison.Ordinal))
            {
                filters.Add($"Area {SelectedAreaFilter}");
            }

            var filterText = filters.Count == 0 ? "All alarm records" : string.Join(", ", filters);
            return $"{filterText}: {Alarms.Count} of {TotalAlarmCount} visible.";
        }
    }
    public string AcknowledgeDisabledReason
    {
        get
        {
            if (IsBusy)
            {
                return "Alarm command is busy.";
            }

            if (SelectedAlarm is null)
            {
                return "Select an alarm to acknowledge.";
            }

            return SelectedAlarm.Alarm.IsAcknowledged
                ? "Selected alarm is already acknowledged."
                : "Store the recovery memo and mark the selected alarm acknowledged.";
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var previousId = SelectedAlarm?.Id;
        IsBusy = true;
        try
        {
            var alarms = await _alarmCenterUseCase.ListRecentAsync(100, cancellationToken).ConfigureAwait(true);
            _allAlarmRecords.Clear();
            _allAlarmRecords.AddRange(alarms);
            ApplyFilters(previousId);
            LastRefreshText = DateTimeOffset.Now.ToString("HH:mm:ss");
            StatusText = Alarms.Count == 0
                ? "No alarm records"
                : $"{Alarms.Count} of {TotalAlarmCount} alarm records visible";
            NotifyCountsChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Alarm refresh cancelled";
        }
        catch (Exception ex)
        {
            Alarms.Clear();
            _allAlarmRecords.Clear();
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
        NotifyAcknowledgeStateChanged();
    }

    partial void OnStatusTextChanged(string value)
    {
        AlertMessage = OperatorAlertClassifier.GetAlertMessage(value);
    }

    partial void OnAlertMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasAlert));
    }

    partial void OnSelectedAlarmChanged(AlarmItemViewModel? value)
    {
        ActionMemoText = value?.Alarm.ActionMemo ?? string.Empty;
        NotifyAcknowledgeStateChanged();
    }

    partial void OnShowActiveOnlyChanged(bool value)
    {
        ApplyFilters(SelectedAlarm?.Id);
    }

    partial void OnSelectedSeverityFilterChanged(string value)
    {
        ApplyFilters(SelectedAlarm?.Id);
    }

    partial void OnSelectedAreaFilterChanged(string value)
    {
        ApplyFilters(SelectedAlarm?.Id);
    }

    private void NotifyAcknowledgeStateChanged()
    {
        AcknowledgeCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsActionMemoEditable));
        OnPropertyChanged(nameof(AcknowledgeDisabledReason));
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

    private void ApplyFilters(Guid? preferredId)
    {
        var filtered = _allAlarmRecords.AsEnumerable();
        if (ShowActiveOnly)
        {
            filtered = filtered.Where(alarm => !alarm.IsAcknowledged);
        }

        if (Enum.TryParse<EquipmentAlarmSeverity>(SelectedSeverityFilter, ignoreCase: false, out var severity))
        {
            filtered = filtered.Where(alarm => alarm.Severity == severity);
        }

        if (Enum.TryParse<EquipmentArea>(SelectedAreaFilter, ignoreCase: false, out var area))
        {
            filtered = filtered.Where(alarm => alarm.Area == area);
        }

        Alarms.Clear();
        foreach (var alarm in filtered)
        {
            Alarms.Add(new AlarmItemViewModel(alarm));
        }

        SelectedAlarm = SelectAlarm(preferredId);
        StatusText = TotalAlarmCount == 0
            ? "No alarm records"
            : $"{Alarms.Count} of {TotalAlarmCount} alarm records visible";
        NotifyCountsChanged();
    }

    private void NotifyCountsChanged()
    {
        OnPropertyChanged(nameof(TotalAlarmCount));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(AcknowledgedCount));
        OnPropertyChanged(nameof(CriticalCount));
        OnPropertyChanged(nameof(HasAlarms));
        OnPropertyChanged(nameof(TotalAlarmDetail));
        OnPropertyChanged(nameof(FilterSummaryText));
    }
}

public sealed record AlarmRecoveryBoundaryItemViewModel(
    string Boundary,
    string State,
    string Detail);
