using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionCell.App.Interaction;
using VisionCell.App.Shared.ViewModels;
using VisionCell.Application.Inspection;
using VisionCell.Vision.Inspection;

namespace VisionCell.App.Modules.OfflineDebug.ViewModels;

public sealed partial class OfflineDebugViewModel : ObservableObject
{
    private const int ResultLimit = 50;
    private readonly IInspectionResultReader _inspectionResultReader;
    private readonly IInspectionArtifactReader _inspectionArtifactReader;
    private readonly IUserConfirmationService _confirmationService;
    private readonly IArtifactViewerService _artifactViewerService;

    public OfflineDebugViewModel(
        IInspectionResultReader inspectionResultReader,
        IInspectionArtifactReader inspectionArtifactReader,
        IUserConfirmationService confirmationService,
        IArtifactViewerService artifactViewerService)
    {
        _inspectionResultReader = inspectionResultReader ?? throw new ArgumentNullException(nameof(inspectionResultReader));
        _inspectionArtifactReader = inspectionArtifactReader ?? throw new ArgumentNullException(nameof(inspectionArtifactReader));
        _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));
        _artifactViewerService = artifactViewerService ?? throw new ArgumentNullException(nameof(artifactViewerService));
        RefreshResultsCommand = new AsyncRelayCommand(RefreshResultsAsync, () => !IsBusy);
        LoadSelectedArtifactsCommand = new AsyncRelayCommand(LoadSelectedArtifactsAsync, CanInspectSelectedResult);
        OpenOverlayArtifactCommand = new AsyncRelayCommand(OpenSelectedOverlayArtifactAsync, CanInspectSelectedResult);
        OpenHeightMapArtifactCommand = new AsyncRelayCommand(OpenSelectedHeightMapArtifactAsync, CanInspectSelectedResult);
        PrepareReinspectCommand = new RelayCommand(PrepareReinspect, CanInspectSelectedResult);
    }

    public ObservableCollection<OfflineInspectionResultItemViewModel> Results { get; } = new();

    public IAsyncRelayCommand RefreshResultsCommand { get; }
    public IAsyncRelayCommand LoadSelectedArtifactsCommand { get; }
    public IAsyncRelayCommand OpenOverlayArtifactCommand { get; }
    public IAsyncRelayCommand OpenHeightMapArtifactCommand { get; }
    public IRelayCommand PrepareReinspectCommand { get; }

    [ObservableProperty]
    private string _statusText = "Inspection results not loaded";

    [ObservableProperty]
    private string? _alertMessage;

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
    private string _artifactOpenStatusText = "Artifact open not requested";

    [ObservableProperty]
    private ImageSource? _overlayPreviewImageSource;

    [ObservableProperty]
    private double _overlayImagePixelWidth;

    [ObservableProperty]
    private double _overlayImagePixelHeight;

    [ObservableProperty]
    private ImageSource? _heightMapPreviewImageSource;

    [ObservableProperty]
    private string _reinspectStatusText = "Re-inspect not prepared";

    [ObservableProperty]
    private InspectionReinspectPreparation? _preparedReinspect;

    public bool HasAlert => !string.IsNullOrWhiteSpace(AlertMessage);

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
        OpenOverlayArtifactCommand.NotifyCanExecuteChanged();
        OpenHeightMapArtifactCommand.NotifyCanExecuteChanged();
        PrepareReinspectCommand.NotifyCanExecuteChanged();
    }

    partial void OnStatusTextChanged(string value)
    {
        UpdateAlertMessage();
    }

    partial void OnArtifactPreviewStatusTextChanged(string value)
    {
        UpdateAlertMessage();
    }

    partial void OnArtifactOpenStatusTextChanged(string value)
    {
        UpdateAlertMessage();
    }

    partial void OnReinspectStatusTextChanged(string value)
    {
        UpdateAlertMessage();
    }

    partial void OnAlertMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasAlert));
    }

    partial void OnSelectedResultChanged(OfflineInspectionResultItemViewModel? value)
    {
        OverlayPreviewImageSource = null;
        OverlayImagePixelWidth = 0;
        OverlayImagePixelHeight = 0;
        HeightMapPreviewImageSource = null;
        PreparedReinspect = null;
        ArtifactPreviewStatusText = value is null
            ? "Select an inspection result to load artifacts"
            : "Artifact preview not loaded";
        ArtifactOpenStatusText = value is null
            ? "Select an inspection result to open artifacts"
            : "Artifact open not requested";
        ReinspectStatusText = value is null
            ? "Select an inspection result to prepare re-inspect"
            : "Re-inspect not prepared";
        LoadSelectedArtifactsCommand.NotifyCanExecuteChanged();
        OpenOverlayArtifactCommand.NotifyCanExecuteChanged();
        OpenHeightMapArtifactCommand.NotifyCanExecuteChanged();
        PrepareReinspectCommand.NotifyCanExecuteChanged();
    }

    private void UpdateAlertMessage()
    {
        AlertMessage =
            OperatorAlertClassifier.GetAlertMessage(StatusText) ??
            OperatorAlertClassifier.GetAlertMessage(ArtifactPreviewStatusText) ??
            OperatorAlertClassifier.GetAlertMessage(ArtifactOpenStatusText) ??
            OperatorAlertClassifier.GetAlertMessage(ReinspectStatusText);
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
            OverlayImagePixelWidth = overlay.HasImage ? overlay.Width : 0;
            OverlayImagePixelHeight = overlay.HasImage ? overlay.Height : 0;
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
            OverlayImagePixelWidth = 0;
            OverlayImagePixelHeight = 0;
            HeightMapPreviewImageSource = null;
            ArtifactPreviewStatusText = $"Artifact preview load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task OpenSelectedOverlayArtifactAsync(CancellationToken cancellationToken)
    {
        return OpenSelectedArtifactAsync(InspectionArtifactKind.Overlay, cancellationToken);
    }

    public Task OpenSelectedHeightMapArtifactAsync(CancellationToken cancellationToken)
    {
        return OpenSelectedArtifactAsync(InspectionArtifactKind.HeightMap, cancellationToken);
    }

    private async Task OpenSelectedArtifactAsync(
        InspectionArtifactKind artifactKind,
        CancellationToken cancellationToken)
    {
        if (SelectedResult is null)
        {
            ArtifactOpenStatusText = "Select an inspection result to open artifacts";
            return;
        }

        IsBusy = true;
        var label = FormatArtifactLabel(artifactKind);
        try
        {
            var artifactPath = artifactKind == InspectionArtifactKind.HeightMap
                ? SelectedResult.HeightMapPath
                : SelectedResult.OverlayImagePath;
            var preparation = await _inspectionArtifactReader
                .PrepareOpenAsync(new InspectionArtifactOpenRequest(artifactKind, artifactPath), cancellationToken)
                .ConfigureAwait(true);

            if (!preparation.CanOpen)
            {
                ArtifactOpenStatusText = $"{label} open unavailable: {preparation.Message}";
                return;
            }

            var confirmed = await _confirmationService
                .ConfirmAsync(
                    $"Open {label} Artifact",
                    $"Open {preparation.DisplayPath} in an external viewer?",
                    cancellationToken)
                .ConfigureAwait(true);
            if (!confirmed)
            {
                ArtifactOpenStatusText = $"{label} artifact open cancelled by operator.";
                return;
            }

            await _artifactViewerService
                .OpenAsync(preparation.ResolvedPath!, cancellationToken)
                .ConfigureAwait(true);
            ArtifactOpenStatusText = $"{label} artifact open requested: {preparation.DisplayPath}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ArtifactOpenStatusText = $"{label} artifact open cancelled";
        }
        catch (Exception ex)
        {
            ArtifactOpenStatusText = $"{label} artifact open failed: {ex.Message}";
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

    private static string FormatArtifactLabel(InspectionArtifactKind artifactKind)
    {
        return artifactKind == InspectionArtifactKind.HeightMap ? "Height Map" : "Overlay";
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
        OverlayItems = Defects.Select(defect => defect.OverlayItem).ToArray();
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
    public IReadOnlyList<RoiOverlayItemViewModel> OverlayItems { get; }

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
        X = defect.X;
        Y = defect.Y;
        Width = defect.Width;
        Height = defect.Height;
        BoundingBoxText = $"{defect.X},{defect.Y} {defect.Width}x{defect.Height}";
        Message = string.IsNullOrWhiteSpace(defect.Message) ? "-" : defect.Message;
        OverlayItem = new RoiOverlayItemViewModel(
            defect.X,
            defect.Y,
            defect.Width,
            defect.Height,
            FormatOverlayLabel(Type, RoiId),
            ScoreText,
            "Defect");
    }

    public string Type { get; }
    public string ScoreText { get; }
    public string RoiId { get; }
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    public string BoundingBoxText { get; }
    public string Message { get; }
    public RoiOverlayItemViewModel OverlayItem { get; }

    private static string FormatOverlayLabel(string type, string roiId)
    {
        return roiId == "-"
            ? type
            : $"{roiId} {type}";
    }
}
