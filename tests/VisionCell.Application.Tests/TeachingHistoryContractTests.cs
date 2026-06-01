using FluentAssertions;
using VisionCell.Application.Teaching;
using Xunit;

namespace VisionCell_Application_Tests;

public sealed class TeachingHistoryContractTests
{
    [Fact]
    public void Create_Should_Record_Traceable_Update_Payload()
    {
        var teachingPointId = Guid.Parse("5fb7be86-6212-4469-bdf3-d4d4f1b83449");
        var timestamp = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

        var entry = TeachingHistoryEntry.Create(
            teachingPointId,
            " recipe-a ",
            TeachingHistoryAction.Updated,
            """{"x":1}""",
            """{"x":2}""",
            () => timestamp);

        entry.Id.Should().NotBe(Guid.Empty);
        entry.TeachingPointId.Should().Be(teachingPointId);
        entry.RecipeId.Should().Be("recipe-a");
        entry.Action.Should().Be(TeachingHistoryAction.Updated);
        entry.BeforeJson.Should().Be("""{"x":1}""");
        entry.AfterJson.Should().Be("""{"x":2}""");
        entry.CreatedAt.Should().Be(timestamp);
    }

    [Fact]
    public void Create_Should_Reject_Update_Without_Before_And_After_Payloads()
    {
        var act = () => TeachingHistoryEntry.Create(
            Guid.NewGuid(),
            "recipe-a",
            TeachingHistoryAction.Updated,
            beforeJson: null,
            afterJson: """{"x":2}""");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*before and after JSON*");
    }

    [Fact]
    public async Task Repository_Port_Should_Save_And_List_By_Teaching_Point()
    {
        var repository = new CapturingTeachingHistoryRepository();
        var teachingPointId = Guid.NewGuid();
        var entry = TeachingHistoryEntry.Create(
            teachingPointId,
            "recipe-a",
            TeachingHistoryAction.Created,
            beforeJson: null,
            afterJson: """{"name":"Load"}""");

        await repository.SaveAsync(entry, CancellationToken.None);
        var entries = await repository.ListByPointAsync(teachingPointId, 10, CancellationToken.None);

        entries.Should().ContainSingle().Which.Should().Be(entry);
    }

    private sealed class CapturingTeachingHistoryRepository : ITeachingHistoryRepository
    {
        private readonly List<TeachingHistoryEntry> _entries = new();

        public Task SaveAsync(TeachingHistoryEntry entry, CancellationToken cancellationToken)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TeachingHistoryEntry>> ListByPointAsync(
            Guid teachingPointId,
            int limit,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TeachingHistoryEntry>>(
                _entries.Where(entry => entry.TeachingPointId == teachingPointId).Take(limit).ToArray());
        }
    }
}
