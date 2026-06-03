using FluentAssertions;
using VisionCell.Core.Alarms;
using VisionCell.Persistence.Alarms;
using VisionCell.Persistence.Sqlite;
using Xunit;

namespace VisionCell_Persistence_Tests;

public sealed class SqliteEquipmentAlarmRepositoryTests
{
    [Fact]
    public async Task SaveAsync_Should_Create_Schema_And_Insert_Alarm()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var alarm = CreateAlarm("MOT-003", DateTimeOffset.Parse("2026-06-01T12:00:00Z"));

        await repository.SaveAsync(alarm, CancellationToken.None);
        var records = await repository.ListRecentAsync(10, CancellationToken.None);
        var migrationCount = await database.CountRowsAsync("schema_version", CancellationToken.None);

        records.Should().ContainSingle();
        records[0].Id.Should().Be(alarm.Id);
        records[0].Code.Should().Be("MOT-003");
        records[0].Severity.Should().Be(EquipmentAlarmSeverity.Error);
        records[0].Area.Should().Be(EquipmentArea.Motion);
        records[0].Message.Should().Be("Motion command timed out.");
        records[0].CorrelationId.Should().Be("corr-001");
        records[0].IsAcknowledged.Should().BeFalse();
        migrationCount.Should().Be(8);
    }

    [Fact]
    public async Task AcknowledgeAsync_Should_Update_Acknowledged_Time_And_Action_Memo()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var alarm = CreateAlarm("CAM-001", DateTimeOffset.Parse("2026-06-01T12:00:00Z"));

        await repository.SaveAsync(alarm, CancellationToken.None);
        await repository.AcknowledgeAsync(
            alarm.Id,
            DateTimeOffset.Parse("2026-06-01T12:05:00Z"),
            "Checked camera ready and retried grab.",
            CancellationToken.None);
        var records = await repository.ListRecentAsync(10, CancellationToken.None);

        records.Should().ContainSingle();
        records[0].IsAcknowledged.Should().BeTrue();
        records[0].AcknowledgedAt.Should().Be(DateTimeOffset.Parse("2026-06-01T12:05:00Z"));
        records[0].ActionMemo.Should().Be("Checked camera ready and retried grab.");
    }

    [Fact]
    public async Task ListRecentAsync_Should_Return_Newest_First_And_Respect_Limit()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var older = CreateAlarm("EQP-003", DateTimeOffset.Parse("2026-06-01T12:00:00Z"));
        var newer = CreateAlarm("CAM-003", DateTimeOffset.Parse("2026-06-01T12:10:00Z"));

        await repository.SaveAsync(older, CancellationToken.None);
        await repository.SaveAsync(newer, CancellationToken.None);
        var records = await repository.ListRecentAsync(1, CancellationToken.None);

        records.Should().ContainSingle();
        records[0].Id.Should().Be(newer.Id);
    }

    private static EquipmentAlarm CreateAlarm(string code, DateTimeOffset occurredAt)
    {
        return new EquipmentAlarm(
            Guid.NewGuid(),
            code,
            code.StartsWith("EQP-", StringComparison.Ordinal) ? EquipmentAlarmSeverity.Critical : EquipmentAlarmSeverity.Error,
            code.StartsWith("CAM-", StringComparison.Ordinal) ? EquipmentArea.Camera : EquipmentArea.Motion,
            code.StartsWith("CAM-", StringComparison.Ordinal) ? "Camera grab failed." : "Motion command timed out.",
            occurredAt,
            correlationId: "corr-001");
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

        public SqliteEquipmentAlarmRepository CreateRepository()
        {
            return new SqliteEquipmentAlarmRepository(ConnectionFactory, SchemaInitializer);
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
