using FluentAssertions;
using VisionCell.App.Interaction;
using VisionCell.App.Modules.OfflineDebug.ViewModels;
using VisionCell.Application.Inspection;
using VisionCell.Vision.Inspection;
using Xunit;

namespace VisionCell_App_Tests;

public sealed class OfflineDebugArtifactOpenViewModelTests
{
    [Fact]
    public async Task OpenSelectedOverlayArtifactAsync_Should_Confirm_And_Open_Prepared_Path()
    {
        var result = CreateResultRecord();
        var artifactReader = new FakeInspectionArtifactReader
        {
            OpenHandler = (request, _) => Task.FromResult(InspectionArtifactOpenResult.Ready(
                request.ArtifactKind,
                request.ArtifactPath!,
                Path.Combine(
                    Path.GetTempPath(),
                    "VisionCellArtifactOpenTests",
                    "result.overlay.bmp")))
        };
        var confirmation = new FakeUserConfirmationService(result: true);
        var viewer = new FakeArtifactViewerService();
        var viewModel = CreateViewModel(result, artifactReader, confirmation, viewer);

        await viewModel.RefreshResultsAsync(CancellationToken.None);
        await viewModel.OpenSelectedOverlayArtifactAsync(CancellationToken.None);

        artifactReader.OpenRequests.Should().ContainSingle();
        artifactReader.OpenRequests[0].ArtifactKind.Should().Be(InspectionArtifactKind.Overlay);
        confirmation.Prompts.Should().ContainSingle();
        confirmation.Prompts[0].Message.Should().Contain(result.OverlayImagePath);
        viewer.OpenedPaths.Should().ContainSingle(path => path.EndsWith("result.overlay.bmp", StringComparison.Ordinal));
        viewModel.ArtifactOpenStatusText.Should().Contain("open requested");
    }

    [Fact]
    public async Task OpenSelectedHeightMapArtifactAsync_Should_Not_Open_When_Operator_Declines()
    {
        var result = CreateResultRecord();
        var artifactReader = new FakeInspectionArtifactReader
        {
            OpenHandler = (request, _) => Task.FromResult(InspectionArtifactOpenResult.Ready(
                request.ArtifactKind,
                request.ArtifactPath!,
                Path.Combine(
                    Path.GetTempPath(),
                    "VisionCellArtifactOpenTests",
                    "result.height.bmp")))
        };
        var confirmation = new FakeUserConfirmationService(result: false);
        var viewer = new FakeArtifactViewerService();
        var viewModel = CreateViewModel(result, artifactReader, confirmation, viewer);

        await viewModel.RefreshResultsAsync(CancellationToken.None);
        await viewModel.OpenSelectedHeightMapArtifactAsync(CancellationToken.None);

        artifactReader.OpenRequests.Should().ContainSingle();
        artifactReader.OpenRequests[0].ArtifactKind.Should().Be(InspectionArtifactKind.HeightMap);
        confirmation.Prompts.Should().ContainSingle();
        viewer.OpenedPaths.Should().BeEmpty();
        viewModel.ArtifactOpenStatusText.Should().Contain("cancelled by operator");
    }

    [Theory]
    [InlineData(InspectionArtifactOpenStatus.Missing)]
    [InlineData(InspectionArtifactOpenStatus.UnsafePath)]
    public async Task OpenSelectedOverlayArtifactAsync_Should_Surface_Not_Openable_Preparation(
        InspectionArtifactOpenStatus status)
    {
        var result = CreateResultRecord();
        var artifactReader = new FakeInspectionArtifactReader
        {
            OpenHandler = (request, _) => Task.FromResult(CreateNotOpenableResult(status, request))
        };
        var confirmation = new FakeUserConfirmationService(result: true);
        var viewer = new FakeArtifactViewerService();
        var viewModel = CreateViewModel(result, artifactReader, confirmation, viewer);

        await viewModel.RefreshResultsAsync(CancellationToken.None);
        await viewModel.OpenSelectedOverlayArtifactAsync(CancellationToken.None);

        artifactReader.OpenRequests.Should().ContainSingle();
        confirmation.Prompts.Should().BeEmpty();
        viewer.OpenedPaths.Should().BeEmpty();
        viewModel.ArtifactOpenStatusText.Should().Contain("open unavailable");
    }

    [Fact]
    public async Task RunReinspectAsync_Should_Surface_Metadata_Comparison()
    {
        var result = CreateResultRecord();
        var viewModel = CreateViewModel(
            result,
            new FakeInspectionArtifactReader(),
            new FakeUserConfirmationService(result: true),
            new FakeArtifactViewerService());

        await viewModel.RefreshResultsAsync(CancellationToken.None);
        viewModel.PrepareReinspect();
        await viewModel.RunReinspectAsync(CancellationToken.None);

        viewModel.PreparedReinspect.Should().NotBeNull();
        viewModel.PreparedReinspect!.CanRunInspection.Should().BeTrue();
        viewModel.PreparedReinspect.DisabledReason.Should().Contain("metadata comparison");
        viewModel.RunReinspectCommand.CanExecute(null).Should().BeTrue();
        viewModel.ReinspectStatusText.Should().Contain("comparison completed");
        viewModel.ReinspectComparison.Should().NotBeNull();
        viewModel.ReinspectComparison!.PreviousJudgment.Should().Be(result.Judgment.ToString());
        viewModel.ReinspectComparison.ReplayedJudgment.Should().Be(result.Judgment.ToString());
        viewModel.ReinspectComparison.Status.Should().Be(InspectionReinspectComparisonStatus.Matched);
        viewModel.ReinspectComparisonSummary.Should().Contain("Matched");
        viewModel.ReinspectComparisonDetail.Should().Contain("Not persisted");
        viewModel.PreparedReinspectSummary.Should().Contain(result.RecipeId);
        viewModel.PreparedReinspectArtifactSummary.Should().Contain(result.HeightMapPath);
    }

    private static OfflineDebugViewModel CreateViewModel(
        InspectionResultRecord result,
        FakeInspectionArtifactReader artifactReader,
        FakeUserConfirmationService confirmation,
        FakeArtifactViewerService viewer)
    {
        return new OfflineDebugViewModel(
            new FakeInspectionResultReader(result),
            artifactReader,
            new InspectionReinspectUseCase(
                () => new DateTimeOffset(2026, 6, 1, 12, 50, 0, TimeSpan.Zero),
                () => Guid.Parse("11111111-2222-3333-4444-555555555555")),
            new FakeInspectionReinspectComparisonReader(),
            confirmation,
            viewer);
    }

    private static InspectionResultRecord CreateResultRecord()
    {
        var resultId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        return new InspectionResultRecord(
            resultId,
            "corr-001",
            "LOT-20260601123000",
            "RCP-OFFLINE",
            "1.0.0",
            Judgment.Pass,
            "No defects",
            $"camera-frame://VirtualCamera/{resultId:N}",
            $"inspection-artifacts/20260601/{resultId:N}.overlay.bmp",
            $"inspection-artifacts/20260601/{resultId:N}.height.bmp",
            TimeSpan.FromMilliseconds(123),
            new DateTimeOffset(2026, 6, 1, 12, 30, 0, TimeSpan.Zero),
            Array.Empty<InspectionDefectRecord>());
    }

    private static InspectionArtifactOpenResult CreateNotOpenableResult(
        InspectionArtifactOpenStatus status,
        InspectionArtifactOpenRequest request)
    {
        return status switch
        {
            InspectionArtifactOpenStatus.Missing => InspectionArtifactOpenResult.Missing(
                request.ArtifactKind,
                request.ArtifactPath!),
            InspectionArtifactOpenStatus.UnsafePath => InspectionArtifactOpenResult.UnsafePath(
                request.ArtifactKind,
                request.ArtifactPath!),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported test status.")
        };
    }

    private sealed class FakeInspectionResultReader : IInspectionResultReader
    {
        private readonly IReadOnlyList<InspectionResultRecord> _records;

        public FakeInspectionResultReader(params InspectionResultRecord[] records)
        {
            _records = records;
        }

        public Task<IReadOnlyList<InspectionResultRecord>> ListRecentAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<InspectionResultRecord>>(_records.Take(limit).ToArray());
        }
    }

    private sealed class FakeInspectionArtifactReader : IInspectionArtifactReader
    {
        private static readonly DateTimeOffset Timestamp = new(2026, 6, 1, 12, 45, 0, TimeSpan.Zero);

        public List<InspectionArtifactOpenRequest> OpenRequests { get; } = new();

        public Func<InspectionArtifactOpenRequest, CancellationToken, Task<InspectionArtifactOpenResult>>? OpenHandler { get; init; }

        public Task<InspectionArtifactMetadata> ReadMetadataAsync(
            string? artifactPath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(string.IsNullOrWhiteSpace(artifactPath)
                ? InspectionArtifactMetadata.NotRecorded()
                : InspectionArtifactMetadata.Available(artifactPath, 2048, Timestamp));
        }

        public Task<InspectionArtifactPreviewResult> ReadPreviewAsync(
            string? artifactPath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(InspectionArtifactPreviewResult.FromMetadata(
                InspectionArtifactMetadata.NotRecorded()));
        }

        public Task<InspectionArtifactOpenResult> PrepareOpenAsync(
            InspectionArtifactOpenRequest request,
            CancellationToken cancellationToken)
        {
            OpenRequests.Add(request);
            return OpenHandler is not null
                ? OpenHandler(request, cancellationToken)
                : Task.FromResult(InspectionArtifactOpenResult.Ready(
                    request.ArtifactKind,
                    request.ArtifactPath!,
                    Path.Combine(
                        Path.GetTempPath(),
                        "VisionCellArtifacts",
                        $"{request.ArtifactKind}.bmp")));
        }
    }

    private sealed class FakeInspectionReinspectComparisonReader : IInspectionReinspectComparisonReader
    {
        public Task<IReadOnlyList<InspectionReinspectComparisonResult>> ListRecentAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<InspectionReinspectComparisonResult>>(Array.Empty<InspectionReinspectComparisonResult>());
        }
    }

    private sealed class FakeUserConfirmationService : IUserConfirmationService
    {
        private readonly bool _result;

        public FakeUserConfirmationService(bool result)
        {
            _result = result;
        }

        public List<(string Title, string Message)> Prompts { get; } = new();

        public Task<bool> ConfirmAsync(
            string title,
            string message,
            CancellationToken cancellationToken)
        {
            Prompts.Add((title, message));
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeArtifactViewerService : IArtifactViewerService
    {
        public List<string> OpenedPaths { get; } = new();

        public Task OpenAsync(
            string artifactPath,
            CancellationToken cancellationToken)
        {
            OpenedPaths.Add(artifactPath);
            return Task.CompletedTask;
        }
    }
}
