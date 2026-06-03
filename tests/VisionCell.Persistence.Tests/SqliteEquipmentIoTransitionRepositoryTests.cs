using FluentAssertions;
using VisionCell.Equipment.Io;
using VisionCell.Persistence.Equipment;
using VisionCell.Persistence.Sqlite;
using Xunit;

namespace VisionCell_Persistence_Tests;

public sealed class SqliteEquipmentIoTransitionRepositoryTests
{
    [Fact]
    public async Task SaveAndListRecentAsync_Should_Roundtrip_Transitions_Latest_First()
    {
        using var database = new TestDatabase();
        var repository = database.CreateRepository();
        var first = CreateTransition(
            "DI_ESTOP_ON",
            "X001",
            previousValue: false,
            currentValue: true,
            previousForced: false,
            currentForced: true,
            changedAt: new DateTimeOffset(2026, 6, 3, 9, 0, 0, TimeSpan.Zero),
            correlationId: "corr-001");
        var second = CreateTransition(
            "DI_DOOR_CLOSED",
            "X000",
            previousValue: true,
            currentValue: false,
            previousForced: false,
            currentForced: true,
            changedAt: new DateTimeOffset(2026, 6, 3, 9, 1, 0, TimeSpan.Zero),
            correlationId: "corr-002");

        await repository.SaveAsync(first, CancellationToken.None);
        await repository.SaveAsync(second, CancellationToken.None);

        var transitions = await repository.ListRecentAsync(10, CancellationToken.None);

        transitions.Should().HaveCount(2);
        transitions[0].Name.Should().Be("DI_DOOR_CLOSED");
        transitions[0].Address.Should().Be("X000");
        transitions[0].Direction.Should().Be(IoBitDirection.Input);
        transitions[0].PreviousValue.Should().BeTrue();
        transitions[0].CurrentValue.Should().BeFalse();
        transitions[0].PreviousForced.Should().BeFalse();
        transitions[0].CurrentForced.Should().BeTrue();
        transitions[0].Source.Should().Be("Fault Injection Door Open On");
        transitions[0].CorrelationId.Should().Be("corr-002");
        transitions[0].OperatorMemo.Should().Be("Door input forced open.");
        transitions[1].Name.Should().Be("DI_ESTOP_ON");
    }

    [Fact]
    public async Task ListRecentAsync_Should_Reject_Invalid_Limit()
    {
        using var database = new TestDatabase();
        var repository = database.CreateRepository();

        var act = () => repository.ListRecentAsync(0, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private static IoTransitionRecord CreateTransition(
        string name,
        string address,
        bool previousValue,
        bool currentValue,
        bool previousForced,
        bool currentForced,
        DateTimeOffset changedAt,
        string correlationId)
    {
        return new IoTransitionRecord(
            Guid.NewGuid(),
            name,
            address,
            IoBitDirection.Input,
            previousValue,
            currentValue,
            previousForced,
            currentForced,
            changedAt,
            name == "DI_DOOR_CLOSED" ? "Fault Injection Door Open On" : "Fault Injection Emergency Stop On",
            correlationId,
            name == "DI_DOOR_CLOSED" ? "Door input forced open." : "EStop input forced on.");
    }

    private sealed class TestDatabase : IDisposable
    {
        private readonly string _directory;

        public TestDatabase()
        {
            _directory = Path.Combine(Path.GetTempPath(), "VisionCellPersistenceTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            var databasePath = Path.Combine(_directory, "visioncell-test.db");
            ConnectionFactory = new SqliteConnectionFactory(databasePath);
            SchemaInitializer = new SqliteSchemaInitializer(ConnectionFactory);
        }

        public SqliteConnectionFactory ConnectionFactory { get; }
        public SqliteSchemaInitializer SchemaInitializer { get; }

        public SqliteEquipmentIoTransitionRepository CreateRepository()
        {
            return new SqliteEquipmentIoTransitionRepository(ConnectionFactory, SchemaInitializer);
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
