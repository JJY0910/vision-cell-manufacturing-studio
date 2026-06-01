using FluentAssertions;
using VisionCell.Application.Inspection;
using VisionCell.Persistence.Inspection;
using VisionCell.Persistence.Sqlite;
using VisionCell.Vision.Inspection;
using Xunit;

namespace VisionCell_Persistence_Tests;

public sealed class SqliteInspectionResultRepositoryTests
{
    [Fact]
    public async Task SaveAsync_Should_Create_Schema_And_Insert_Inspection_Result_With_Defects()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var request = CreateRequest(
            Judgment.Fail,
            new[]
            {
                new InspectionDefectRecord("Missing", 0.91, "ROI-01", 10, 20, 30, 40, "Missing area."),
                new InspectionDefectRecord("Lift", 0.23, "ROI-01", 12, 22, 1, 1, "Lift height.")
            },
            DateTimeOffset.Parse("2026-06-01T00:00:00Z"));

        await repository.SaveAsync(request, CancellationToken.None);
        var records = await repository.ListRecentAsync(10, CancellationToken.None);
        var resultCount = await database.CountRowsAsync("inspection_results", CancellationToken.None);
        var defectCount = await database.CountRowsAsync("defects", CancellationToken.None);
        var migrationCount = await database.CountRowsAsync("schema_version", CancellationToken.None);

        resultCount.Should().Be(1);
        defectCount.Should().Be(2);
        migrationCount.Should().Be(5);
        records.Should().ContainSingle();
        records[0].Id.Should().Be(request.Id);
        records[0].CorrelationId.Should().Be(request.CorrelationId);
        records[0].LotId.Should().Be("LOT-20260601000000");
        records[0].RecipeId.Should().Be("RCP-RESULT");
        records[0].Judgment.Should().Be(Judgment.Fail);
        records[0].SourceImagePath.Should().Contain("camera-frame");
        records[0].Defects.Should().HaveCount(2);
        records[0].Defects.Should().Contain(defect => defect.Type == "Lift" && defect.RoiId == "ROI-01");
    }

    [Fact]
    public async Task ListRecentAsync_Should_Return_Newest_Results_First_And_Respect_Limit()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var older = CreateRequest(Judgment.Pass, Array.Empty<InspectionDefectRecord>(), DateTimeOffset.Parse("2026-06-01T00:00:00Z"));
        var newer = CreateRequest(Judgment.Fail, new[] { new InspectionDefectRecord("Scratch", 0.7, null, 1, 2, 3, 4, "Scratch.") }, DateTimeOffset.Parse("2026-06-01T00:00:10Z"));

        await repository.SaveAsync(older, CancellationToken.None);
        await repository.SaveAsync(newer, CancellationToken.None);
        var records = await repository.ListRecentAsync(1, CancellationToken.None);

        records.Should().ContainSingle();
        records[0].Id.Should().Be(newer.Id);
        records[0].Judgment.Should().Be(Judgment.Fail);
        records[0].Defects.Should().ContainSingle(defect => defect.Type == "Scratch");
    }

    private static InspectionResultSaveRequest CreateRequest(
        Judgment judgment,
        IReadOnlyList<InspectionDefectRecord> defects,
        DateTimeOffset createdAt)
    {
        return new InspectionResultSaveRequest(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            "LOT-20260601000000",
            "RCP-RESULT",
            "1.0.0",
            judgment,
            defects.Count == 0 ? "No defects" : $"{defects.Count} defect(s)",
            "camera-frame://Virtual%203D%20camera/test",
            OverlayImagePath: null,
            HeightMapPath: null,
            TimeSpan.FromMilliseconds(123),
            new[]
            {
                new InspectionSequenceStepRecord("Judge", InspectionSequenceStepStatus.Success, $"Judge: {judgment}.", TimeSpan.FromMilliseconds(1))
            },
            new Dictionary<string, string>
            {
                ["RecipeId"] = "RCP-RESULT",
                ["FrameWidth"] = "320"
            },
            defects,
            createdAt);
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

        public SqliteInspectionResultRepository CreateRepository()
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
