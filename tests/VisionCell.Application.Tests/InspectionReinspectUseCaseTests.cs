using FluentAssertions;
using VisionCell.Application.Inspection;
using Xunit;

namespace VisionCell.Application.Tests;

public sealed class InspectionReinspectUseCaseTests
{
    [Fact]
    public async Task RunAsync_Should_Return_Metadata_Comparison_Result()
    {
        var comparedAt = new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);
        var replayId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var preparation = CreatePreparation(canRunInspection: true);
        var useCase = new InspectionReinspectUseCase(() => comparedAt, () => replayId);

        var result = await useCase.RunAsync(preparation, CancellationToken.None);

        result.SourceResultId.Should().Be(preparation.SourceResultId);
        result.ReplayCorrelationId.Should().Be($"offline-reinspect-{replayId:N}");
        result.LotId.Should().Be(preparation.LotId);
        result.RecipeId.Should().Be(preparation.RecipeId);
        result.PreviousJudgment.Should().Be(preparation.PreviousJudgment);
        result.ReplayedJudgment.Should().Be(preparation.PreviousJudgment);
        result.PreviousDefectCount.Should().Be(preparation.PreviousDefectCount);
        result.ReplayedDefectCount.Should().Be(preparation.PreviousDefectCount);
        result.PreviousCycleTime.Should().Be(preparation.PreviousCycleTime);
        result.ReplayedCycleTime.Should().Be(preparation.PreviousCycleTime);
        result.Status.Should().Be(InspectionReinspectComparisonStatus.Matched);
        result.JudgmentChanged.Should().BeFalse();
        result.ComparedAt.Should().Be(comparedAt);
        result.PersistenceStatus.Should().Be("Not persisted");
        result.Message.Should().Contain("Live camera");
    }

    [Fact]
    public async Task RunAsync_Should_Persist_Metadata_Comparison_When_Repository_Is_Configured()
    {
        var repository = new FakeReinspectComparisonRepository();
        var preparation = CreatePreparation(canRunInspection: true);
        var useCase = new InspectionReinspectUseCase(
            repository,
            () => new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero),
            () => Guid.Parse("11111111-2222-3333-4444-555555555555"));

        var result = await useCase.RunAsync(preparation, CancellationToken.None);

        result.PersistenceStatus.Should().Contain("Persisted");
        repository.Saved.Should().ContainSingle();
        repository.Saved[0].ReplayCorrelationId.Should().Be(result.ReplayCorrelationId);
        repository.Saved[0].SourceResultId.Should().Be(preparation.SourceResultId);
    }

    [Fact]
    public async Task RunAsync_Should_Return_Blocked_Result_When_Preparation_Is_Not_Runnable()
    {
        var preparation = CreatePreparation(canRunInspection: false);
        var useCase = new InspectionReinspectUseCase(
            () => new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero),
            () => Guid.Parse("11111111-2222-3333-4444-555555555555"));

        var result = await useCase.RunAsync(preparation, CancellationToken.None);

        result.Status.Should().Be(InspectionReinspectComparisonStatus.Blocked);
        result.ReplayedJudgment.Should().Be("-");
        result.PersistenceStatus.Should().Be("Not persisted");
        result.Message.Should().Be(preparation.DisabledReason);
    }

    private static InspectionReinspectPreparation CreatePreparation(bool canRunInspection)
    {
        return new InspectionReinspectPreparation(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "LOT-20260603120000",
            "RCP-OFFLINE",
            "1.0.0",
            "Pass",
            TimeSpan.FromMilliseconds(123),
            0,
            "corr-001",
            "camera-frame://VirtualCamera/source",
            "inspection-artifacts/result.overlay.bmp",
            "inspection-artifacts/result.height.bmp",
            new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero),
            canRunInspection,
            canRunInspection
                ? "Ready for metadata comparison; live camera, motion, and vision sequence replay are not executed."
                : "Historical replay runner is unavailable.");
    }

    private sealed class FakeReinspectComparisonRepository : IInspectionReinspectComparisonRepository
    {
        public List<InspectionReinspectComparisonResult> Saved { get; } = new();

        public Task SaveAsync(
            InspectionReinspectComparisonResult result,
            CancellationToken cancellationToken)
        {
            Saved.Add(result);
            return Task.CompletedTask;
        }
    }
}
