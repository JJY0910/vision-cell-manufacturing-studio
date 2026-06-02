using FluentAssertions;
using VisionCell.Application.Teaching;
using VisionCell.Persistence.Sqlite;
using VisionCell.Persistence.Teaching;
using Xunit;

namespace VisionCell_Persistence_Tests;

public sealed class SqliteTeachingHistoryRepositoryTests
{
    [Fact]
    public async Task SaveAsync_Should_Create_Schema_And_Insert_Teaching_History()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateTeachingHistoryRepository();
        var teachingPointId = Guid.Parse("82c9f802-b58a-47de-bd2f-f87ebbaad251");
        var entry = TeachingHistoryEntry.Create(
            teachingPointId,
            "RCP-001",
            TeachingHistoryAction.Created,
            null,
            """{"name":"Load"}""",
            () => DateTimeOffset.Parse("2026-06-01T08:00:00Z"));

        await repository.SaveAsync(entry, CancellationToken.None);
        var entries = await repository.ListByPointAsync(teachingPointId, 10, CancellationToken.None);
        var migrationCount = await database.CountRowsAsync("schema_version", CancellationToken.None);

        entries.Should().ContainSingle();
        entries[0].Id.Should().Be(entry.Id);
        entries[0].TeachingPointId.Should().Be(teachingPointId);
        entries[0].RecipeId.Should().Be("RCP-001");
        entries[0].Action.Should().Be(TeachingHistoryAction.Created);
        entries[0].BeforeJson.Should().BeNull();
        entries[0].AfterJson.Should().Be("""{"name":"Load"}""");
        entries[0].CreatedAt.Should().Be(entry.CreatedAt);
        migrationCount.Should().Be(6);
    }

    [Fact]
    public async Task ListByPointAsync_Should_Return_Newest_Point_History_First_And_Respect_Limit()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateTeachingHistoryRepository();
        var teachingPointId = Guid.Parse("82c9f802-b58a-47de-bd2f-f87ebbaad251");
        var otherPointId = Guid.Parse("f8d29931-62d6-4f65-8c78-5995f9ebf919");
        var older = CreateUpdatedEntry(teachingPointId, "Park", "Safe", "2026-06-01T08:00:00Z");
        var newer = CreateUpdatedEntry(teachingPointId, "Safe", "Load", "2026-06-01T08:05:00Z");
        var other = TeachingHistoryEntry.Create(
            otherPointId,
            null,
            TeachingHistoryAction.Created,
            null,
            """{"name":"Other"}""",
            () => DateTimeOffset.Parse("2026-06-01T08:10:00Z"));

        await repository.SaveAsync(older, CancellationToken.None);
        await repository.SaveAsync(newer, CancellationToken.None);
        await repository.SaveAsync(other, CancellationToken.None);
        var entries = await repository.ListByPointAsync(teachingPointId, 1, CancellationToken.None);

        entries.Should().ContainSingle();
        entries[0].Id.Should().Be(newer.Id);
        entries[0].BeforeJson.Should().Contain("Safe");
        entries[0].AfterJson.Should().Contain("Load");
    }

    [Fact]
    public async Task ListByPointAsync_Should_Reject_Invalid_Arguments()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateTeachingHistoryRepository();

        var emptyPointAct = async () => await repository.ListByPointAsync(Guid.Empty, 10, CancellationToken.None);
        var limitAct = async () => await repository.ListByPointAsync(Guid.NewGuid(), 0, CancellationToken.None);

        await emptyPointAct.Should().ThrowAsync<ArgumentException>();
        await limitAct.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private static TeachingHistoryEntry CreateUpdatedEntry(
        Guid teachingPointId,
        string beforeName,
        string afterName,
        string timestamp)
    {
        return TeachingHistoryEntry.Create(
            teachingPointId,
            null,
            TeachingHistoryAction.Updated,
            $$"""{"name":"{{beforeName}}"}""",
            $$"""{"name":"{{afterName}}"}""",
            () => DateTimeOffset.Parse(timestamp));
    }

    private sealed class TemporaryDatabase : IDisposable
    {
        private readonly string _directory;

        private TemporaryDatabase(string directory)
        {
            _directory = directory;
            var databasePath = Path.Combine(directory, "visioncell-test.db");
            ConnectionFactory = new SqliteConnectionFactory(databasePath);
            SchemaInitializer = new SqliteSchemaInitializer(ConnectionFactory);
        }

        public SqliteConnectionFactory ConnectionFactory { get; }
        public SqliteSchemaInitializer SchemaInitializer { get; }

        public static TemporaryDatabase Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), "VisionCellPersistenceTests", Guid.NewGuid().ToString("N"));
            return new TemporaryDatabase(directory);
        }

        public SqliteTeachingHistoryRepository CreateTeachingHistoryRepository()
        {
            return new SqliteTeachingHistoryRepository(ConnectionFactory, SchemaInitializer);
        }

        public async Task<int> CountRowsAsync(string tableName, CancellationToken cancellationToken)
        {
            await using var connection = await ConnectionFactory.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
