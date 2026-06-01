using FluentAssertions;
using VisionCell.Application.Motion;
using VisionCell.Core.Commands;
using VisionCell.Core.Primitives;
using VisionCell.Persistence.Motion;
using VisionCell.Persistence.Sqlite;
using Xunit;

namespace VisionCell_Persistence_Tests;

public sealed class SqliteMotionCommandHistoryRepositoryTests
{
    [Fact]
    public async Task SaveAsync_Should_Create_Schema_And_Insert_Motion_Command_History()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var entry = CreateEntry("Move Absolute", DateTimeOffset.Parse("2026-06-01T00:00:00Z"));

        await repository.SaveAsync(entry, CancellationToken.None);
        var rows = await repository.ListRecentRowsAsync(10, CancellationToken.None);
        var migrationCount = await database.CountRowsAsync("schema_version", CancellationToken.None);

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(entry.Id);
        rows[0].CorrelationId.Should().Be(entry.Request.CorrelationId.ToString());
        rows[0].CommandName.Should().Be("Move Absolute");
        rows[0].AxisId.Should().Be("X");
        rows[0].ElapsedMs.Should().Be(42);
        rows[0].RequestJson.Should().Contain("Move Absolute");
        rows[0].CommandResultJson.Should().Contain("Success");
        migrationCount.Should().Be(5);
    }

    [Fact]
    public async Task InitializeAsync_Should_Be_Idempotent()
    {
        using var database = TemporaryDatabase.Create();

        await database.SchemaInitializer.InitializeAsync(CancellationToken.None);
        await database.SchemaInitializer.InitializeAsync(CancellationToken.None);
        var migrationCount = await database.CountRowsAsync("schema_version", CancellationToken.None);

        migrationCount.Should().Be(5);
    }

    [Fact]
    public async Task ListRecentAsync_Should_Return_Newest_Records_First_And_Respect_Limit()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var older = CreateEntry("Jog", DateTimeOffset.Parse("2026-06-01T00:00:00Z"));
        var newer = CreateEntry("Home", DateTimeOffset.Parse("2026-06-01T00:00:10Z"));

        await repository.SaveAsync(older, CancellationToken.None);
        await repository.SaveAsync(newer, CancellationToken.None);
        var records = await repository.ListRecentAsync(1, CancellationToken.None);

        records.Should().ContainSingle();
        records[0].Id.Should().Be(newer.Id);
        records[0].CommandName.Should().Be("Home");
        records[0].Status.Should().Be(CommandStatus.Success);
        records[0].Message.Should().Be("Home completed.");
        records[0].Elapsed.Should().Be(TimeSpan.FromMilliseconds(42));
    }

    [Fact]
    public async Task ListRecentAsync_Should_Create_Empty_Table_When_No_History_Exists()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();

        var rows = await repository.ListRecentAsync(10, CancellationToken.None);
        var historyCount = await database.CountRowsAsync("motion_command_history", CancellationToken.None);

        rows.Should().BeEmpty();
        historyCount.Should().Be(0);
    }

    private static MotionCommandHistoryEntry CreateEntry(string commandName, DateTimeOffset createdAt)
    {
        var correlationId = CorrelationId.New();
        var request = new MachineCommandRequest(
            commandName,
            correlationId,
            TimeSpan.FromSeconds(2),
            createdAt,
            new Dictionary<string, string>
            {
                ["Axis"] = "X",
                ["X"] = "10.000"
            });
        var result = MachineCommandResult.Success(
            $"{commandName} completed.",
            TimeSpan.FromMilliseconds(42),
            correlationId);

        return new MotionCommandHistoryEntry(Guid.NewGuid(), request, result, createdAt);
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

        public SqliteMotionCommandHistoryRepository CreateRepository()
        {
            return new SqliteMotionCommandHistoryRepository(ConnectionFactory, SchemaInitializer);
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

