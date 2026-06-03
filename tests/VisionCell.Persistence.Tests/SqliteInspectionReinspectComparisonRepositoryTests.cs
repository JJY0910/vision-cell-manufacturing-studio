using FluentAssertions;
using VisionCell.Application.Inspection;
using VisionCell.Persistence.Inspection;
using VisionCell.Persistence.Sqlite;
using VisionCell.Vision.Inspection;
using Xunit;

namespace VisionCell_Persistence_Tests;

public sealed class SqliteInspectionReinspectComparisonRepositoryTests
{
    [Fact]
    public async Task SaveAsync_Should_Create_Schema_And_Insert_Reinspect_Comparison()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var result = CreateResult(
            "offline-reinspect-001",
            new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero));

        await database.CreateInspectionResultRepository()
            .SaveAsync(CreateInspectionResult(result.SourceResultId), CancellationToken.None);
        await repository.SaveAsync(result, CancellationToken.None);
        var records = await repository.ListRecentAsync(10, CancellationToken.None);
        var rowCount = await database.CountRowsAsync("inspection_reinspect_comparisons", CancellationToken.None);
        var migrationCount = await database.CountRowsAsync("schema_version", CancellationToken.None);

        rowCount.Should().Be(1);
        migrationCount.Should().Be(8);
        records.Should().ContainSingle();
        records[0].SourceResultId.Should().Be(result.SourceResultId);
        records[0].ReplayCorrelationId.Should().Be(result.ReplayCorrelationId);
        records[0].LotId.Should().Be(result.LotId);
        records[0].RecipeId.Should().Be(result.RecipeId);
        records[0].Status.Should().Be(InspectionReinspectComparisonStatus.Matched);
        records[0].PersistenceStatus.Should().Contain("Persisted");
        records[0].Message.Should().Contain("Metadata comparison");
    }

    [Fact]
    public async Task ListRecentAsync_Should_Return_Newest_Comparison_First_And_Respect_Limit()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var older = CreateResult(
            "offline-reinspect-older",
            new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero));
        var newer = CreateResult(
            "offline-reinspect-newer",
            new DateTimeOffset(2026, 6, 3, 12, 10, 0, TimeSpan.Zero));

        await database.CreateInspectionResultRepository()
            .SaveAsync(CreateInspectionResult(older.SourceResultId), CancellationToken.None);
        await repository.SaveAsync(older, CancellationToken.None);
        await repository.SaveAsync(newer, CancellationToken.None);
        var records = await repository.ListRecentAsync(1, CancellationToken.None);

        records.Should().ContainSingle();
        records[0].ReplayCorrelationId.Should().Be("offline-reinspect-newer");
    }

    private static InspectionReinspectComparisonResult CreateResult(
        string replayCorrelationId,
        DateTimeOffset comparedAt)
    {
        return new InspectionReinspectComparisonResult(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            replayCorrelationId,
            "LOT-20260603120000",
            "RCP-OFFLINE",
            "1.0.0",
            "Pass",
            "Pass",
            0,
            0,
            TimeSpan.FromMilliseconds(123),
            TimeSpan.FromMilliseconds(123),
            InspectionReinspectComparisonStatus.Matched,
            comparedAt,
            "Persisted to offline re-inspect history.",
            "Metadata comparison completed from the prepared historical result context.");
    }

    private static InspectionResultSaveRequest CreateInspectionResult(Guid id)
    {
        return new InspectionResultSaveRequest(
            id,
            "corr-source-001",
            "LOT-20260603120000",
            "RCP-OFFLINE",
            "1.0.0",
            Judgment.Pass,
            "No defects",
            $"camera-frame://VirtualCamera/{id:N}",
            $"inspection-artifacts/20260603/{id:N}.overlay.bmp",
            $"inspection-artifacts/20260603/{id:N}.height.bmp",
            TimeSpan.FromMilliseconds(123),
            new[]
            {
                new InspectionSequenceStepRecord("Judge", InspectionSequenceStepStatus.Success, "Judge: Pass.", TimeSpan.Zero)
            },
            new Dictionary<string, string>
            {
                ["RecipeId"] = "RCP-OFFLINE"
            },
            Array.Empty<InspectionDefectRecord>(),
            new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero));
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

        public SqliteInspectionReinspectComparisonRepository CreateRepository()
        {
            return new SqliteInspectionReinspectComparisonRepository(ConnectionFactory, SchemaInitializer);
        }

        public SqliteInspectionResultRepository CreateInspectionResultRepository()
        {
            return new SqliteInspectionResultRepository(ConnectionFactory, SchemaInitializer);
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
