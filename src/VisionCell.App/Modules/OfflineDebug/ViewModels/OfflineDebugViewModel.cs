using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.Application.Inspection;
using VisionCell.Vision.Inspection;

namespace VisionCell.App.Modules.OfflineDebug.ViewModels;

public sealed partial class OfflineDebugViewModel : ObservableObject
{
    private const int ResultLimit = 50;
    private readonly IInspectionResultReader _inspectionResultReader;
    private readonly IInspectionArtifactReader _inspectionArtifactReader;

    public OfflineDebugViewModel(
        IInspectionResultReader inspectionResultReader,
        IInspectionArtifactReader inspectionArtifactReader)
    {
        _inspectionResultReader = inspectionResultReader ?? throw new ArgumentNullException(nameof(inspectionResultReader));
        _inspectionArtifactReader = inspectionArtifactReader ?? throw new ArgumentNullException(nameof(inspectionArtifactReader));
        RefreshResultsCommand = new AsyncRelayCommand(RefreshResultsAsync, () => !IsBusy);
        LoadSelectedArtifactsCommand = new AsyncRelayCommand(LoadSelectedArtifactsAsync, CanInspectSelectedResult);
        PrepareReinspectCommand = new RelayCommand(PrepareReinspect, CanInspectSelectedResult);
    }

    public ObservableCollection<OfflineInspectionResultItemViewModel> Results { get; } = new();

    public IAsyncRelayCommand RefreshResultsCommand { get; }
    public IAsyncRelayCommand LoadSelectedArtifactsCommand { get; }
    public IRelayCommand PrepareReinspectCommand { get; }

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

    [ObservableProperty]
    private string _artifactPreviewStatusText = "Artifact preview not loaded";

    [ObservableProperty]
    private ImageSource? _overlayPreviewImageSource;

    [ObservableProperty]
    private ImageSource? _heightMapPreviewImageSource;

    [ObservableProperty]
    private string _reinspectStatusText = "Re-inspect not prepared";

    [ObservableProperty]
    private InspectionReinspectPreparation? _preparedReinspect;

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
                var overlayMetadata = await _inspectionArtifactReader
                    .ReadMetadataAsync(record.OverlayImagePath, cancellationToken)
                    .ConfigureAwait(true);
                var heightMapMetadata = await _inspectionArtifactReader
                    .ReadMetadataAsync(record.HeightMapPath, cancellationToken)
                    .ConfigureAwait(true);
                Results.Add(new OfflineInspectionResultItemViewModel(record, overlayMetadata, heightMapMetadata));
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
        LoadSelectedArtifactsCommand.NotifyCanExecuteChanged();
        PrepareReinspectCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedResultChanged(OfflineInspectionResultItemViewModel? value)
    {
        OverlayPreviewImageSource = null;
        HeightMapPreviewImageSource = null;
        PreparedReinspect = null;
        ArtifactPreviewStatusText = value is null
            ? "Select an inspection result to load artifacts"
            : "Artifact preview not loaded";
        ReinspectStatusText = value is null
            ? "Select an inspection result to prepare re-inspect"
            : "Re-inspect not prepared";
        LoadSelectedArtifactsCommand.NotifyCanExecuteChanged();
        PrepareReinspectCommand.NotifyCanExecuteChanged();
    }

    public async Task LoadSelectedArtifactsAsync(CancellationToken cancellationToken)
    {
        if (SelectedResult is null)
        {
            ArtifactPreviewStatusText = "Select an inspection result to load artifacts";
            return;
        }

        IsBusy = true;
        try
        {
            var overlay = await _inspectionArtifactReader
                .ReadPreviewAsync(SelectedResult.OverlayImagePath, cancellationToken)
                .ConfigureAwait(true);
            var heightMap = await _inspectionArtifactReader
                .ReadPreviewAsync(SelectedResult.HeightMapPath, cancellationToken)
                .ConfigureAwait(true);

            OverlayPreviewImageSource = CreateImageSource(overlay);
            HeightMapPreviewImageSource = CreateImageSource(heightMap);
            ArtifactPreviewStatusText = $"Overlay: {overlay.Message} Height Map: {heightMap.Message}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ArtifactPreviewStatusText = "Artifact preview load cancelled";
        }
        catch (Exception ex)
        {
            OverlayPreviewImageSource = null;
            HeightMapPreviewImageSource = null;
            ArtifactPreviewStatusText = $"Artifact preview load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void PrepareReinspect()
    {
        if (SelectedResult is null)
        {
            ReinspectStatusText = "Select an inspection result to prepare re-inspect";
            PreparedReinspect = null;
            return;
        }

        PreparedReinspect = new InspectionReinspectPreparation(
            SelectedResult.Id,
            SelectedResult.LotId,
            SelectedResult.RecipeId,
            SelectedResult.RecipeVersion,
            SelectedResult.CorrelationId,
            DateTimeOffset.UtcNow);
        ReinspectStatusText = $"Re-inspect prepared for {SelectedResult.LotId} / {SelectedResult.RecipeId} v{SelectedResult.RecipeVersion}";
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

    private bool CanInspectSelectedResult()
    {
        return !IsBusy && SelectedResult is not null;
    }

    private static ImageSource? CreateImageSource(InspectionArtifactPreviewResult preview)
    {
        if (!preview.HasImage)
        {
            return null;
        }

        if (preview.PixelFormat != InspectionArtifactPreviewPixelFormat.Bgra32)
        {
            throw new NotSupportedException($"Unsupported artifact preview pixel format: {preview.PixelFormat}.");
        }

        var bitmap = BitmapSource.Create(
            preview.Width,
            preview.Height,
            96,
            96,
            PixelFormats.Bgra32,
            palette: null,
            preview.Pixels,
            preview.Stride);
        bitmap.Freeze();
        return bitmap;
    }
}

public sealed class OfflineInspectionResultItemViewModel
{
    public OfflineInspectionResultItemViewModel(
        InspectionResultRecord record,
        InspectionArtifactMetadata overlayMetadata,
        InspectionArtifactMetadata heightMapMetadata)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(overlayMetadata);
        ArgumentNullException.ThrowIfNull(heightMapMetadata);

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
        OverlayArtifactStatus = FormatArtifactStatus("Overlay", overlayMetadata);
        HeightMapArtifactStatus = FormatArtifactStatus("Height Map", heightMapMetadata);
        ArtifactStatusSummary = $"{OverlayArtifactStatus} | {HeightMapArtifactStatus}";
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
    public string OverlayArtifactStatus { get; }
    public string HeightMapArtifactStatus { get; }
    public string ArtifactStatusSummary { get; }
    public string CycleTimeText { get; }
    public string CreatedAtText { get; }
    public int DefectCount { get; }
    public IReadOnlyList<OfflineInspectionDefectItemViewModel> Defects { get; }

    private static string FormatArtifactStatus(string label, InspectionArtifactMetadata metadata)
    {
        return metadata.Status switch
        {
            InspectionArtifactMetadataStatus.Available =>
                $"{label}: Available ({FormatSize(metadata.SizeBytes.GetValueOrDefault())}, {metadata.LastModifiedAt!.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss})",
            InspectionArtifactMetadataStatus.Missing => $"{label}: Missing",
            InspectionArtifactMetadataStatus.NotRecorded => $"{label}: Not recorded",
            InspectionArtifactMetadataStatus.UnsafePath => $"{label}: Unsafe path",
            _ => $"{label}: Unavailable - {metadata.Message}"
        };
    }

    private static string FormatSize(long sizeBytes)
    {
        if (sizeBytes < 1024)
        {
            return $"{sizeBytes} B";
        }

        var kib = sizeBytes / 1024.0;
        return kib < 1024
            ? $"{kib:0.0} KiB"
            : $"{kib / 1024.0:0.0} MiB";
    }
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
