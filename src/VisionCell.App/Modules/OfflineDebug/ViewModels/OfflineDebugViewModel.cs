using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.Application.Inspection;
using VisionCell.Vision.Inspection;

namespace VisionCell.App.Modules.OfflineDebug.ViewModels;

public sealed partial class OfflineDebugViewModel : ObservableObject
{
    private const int ResultLimit = 50;
    private readonly IInspectionResultReader _inspectionResultReader;

    public OfflineDebugViewModel(IInspectionResultReader inspectionResultReader)
    {
        _inspectionResultReader = inspectionResultReader ?? throw new ArgumentNullException(nameof(inspectionResultReader));
        RefreshResultsCommand = new AsyncRelayCommand(RefreshResultsAsync, () => !IsBusy);
    }

    public ObservableCollection<OfflineInspectionResultItemViewModel> Results { get; } = new();

    public IAsyncRelayCommand RefreshResultsCommand { get; }

    [ObservableProperty]
    private string _statusText = "Inspection results not loaded";

    [ObservableProperty]
    private OfflineInspectionResultItemViewModel? _selectedResult;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private int _passCount;

    [ObservableProperty]
    private int _failCount;

    [ObservableProperty]
    private int _defectCount;

    [ObservableProperty]
    private string _lastRefreshText = "-";

    public async Task RefreshResultsAsync(CancellationToken cancellationToken)
    {
        var previousId = SelectedResult?.Id;
        IsBusy = true;
        try
        {
            var records = await _inspectionResultReader
                .ListRecentAsync(ResultLimit, cancellationToken)
                .ConfigureAwait(true);

            Results.Clear();
            foreach (var record in records)
            {
                Results.Add(new OfflineInspectionResultItemViewModel(record));
            }

            HasResults = Results.Count > 0;
            PassCount = Results.Count(result => result.Judgment == Judgment.Pass);
            FailCount = Results.Count(result => result.Judgment == Judgment.Fail);
            DefectCount = Results.Sum(result => result.DefectCount);
            SelectedResult = SelectResult(previousId);
            LastRefreshText = DateTimeOffset.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            StatusText = HasResults
                ? $"{Results.Count} inspection result records loaded"
                : "No inspection result records";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Inspection result refresh cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Inspection result refresh failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshResultsCommand.NotifyCanExecuteChanged();
    }

    private OfflineInspectionResultItemViewModel? SelectResult(Guid? preferredId)
    {
        if (preferredId is not null)
        {
            var match = Results.FirstOrDefault(result => result.Id == preferredId);
            if (match is not null)
            {
                return match;
            }
        }

        return Results.FirstOrDefault();
    }
}

public sealed class OfflineInspectionResultItemViewModel
{
    public OfflineInspectionResultItemViewModel(InspectionResultRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        Id = record.Id;
        CorrelationId = record.CorrelationId;
        LotId = record.LotId;
        RecipeId = record.RecipeId;
        RecipeVersion = record.RecipeVersion;
        Judgment = record.Judgment;
        JudgmentText = record.Judgment.ToString();
        DefectSummary = string.IsNullOrWhiteSpace(record.DefectSummary) ? "-" : record.DefectSummary;
        SourceImagePath = record.SourceImagePath;
        OverlayImagePath = string.IsNullOrWhiteSpace(record.OverlayImagePath) ? "-" : record.OverlayImagePath;
        HeightMapPath = string.IsNullOrWhiteSpace(record.HeightMapPath) ? "-" : record.HeightMapPath;
        CycleTimeText = $"{record.CycleTime.TotalMilliseconds:0} ms";
        CreatedAtText = record.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        DefectCount = record.Defects.Count;
        Defects = record.Defects
            .Select(defect => new OfflineInspectionDefectItemViewModel(defect))
            .ToArray();
    }

    public Guid Id { get; }
    public string CorrelationId { get; }
    public string LotId { get; }
    public string RecipeId { get; }
    public string RecipeVersion { get; }
    public Judgment Judgment { get; }
    public string JudgmentText { get; }
    public string DefectSummary { get; }
    public string SourceImagePath { get; }
    public string OverlayImagePath { get; }
    public string HeightMapPath { get; }
    public string CycleTimeText { get; }
    public string CreatedAtText { get; }
    public int DefectCount { get; }
    public IReadOnlyList<OfflineInspectionDefectItemViewModel> Defects { get; }
}

public sealed class OfflineInspectionDefectItemViewModel
{
    public OfflineInspectionDefectItemViewModel(InspectionDefectRecord defect)
    {
        ArgumentNullException.ThrowIfNull(defect);

        Type = defect.Type;
        ScoreText = defect.Score.ToString("0.000");
        RoiId = string.IsNullOrWhiteSpace(defect.RoiId) ? "-" : defect.RoiId;
        BoundingBoxText = $"{defect.X},{defect.Y} {defect.Width}x{defect.Height}";
        Message = string.IsNullOrWhiteSpace(defect.Message) ? "-" : defect.Message;
    }

    public string Type { get; }
    public string ScoreText { get; }
    public string RoiId { get; }
    public string BoundingBoxText { get; }
    public string Message { get; }
}
