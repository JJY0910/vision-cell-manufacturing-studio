using FluentAssertions;
using VisionCell.Core.Primitives;
using VisionCell.Motion.Teaching;
using VisionCell.Persistence.Sqlite;
using VisionCell.Persistence.Teaching;
using Xunit;

namespace VisionCell_Persistence_Tests;

public sealed class SqliteTeachingPointRepositoryTests
{
    [Fact]
    public async Task SaveAsync_Should_Create_Schema_And_Insert_Teaching_Point()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateTeachingRepository();
        var point = CreatePoint("Camera Align");

        await repository.SaveAsync(point, CancellationToken.None);
        var saved = await repository.FindByIdAsync(point.Id, CancellationToken.None);
        var migrationCount = await database.CountRowsAsync("schema_version", CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Id.Should().Be(point.Id);
        saved.Name.Should().Be("Camera Align");
        saved.Role.Should().Be(TeachingRole.Camera);
        saved.Position.Should().Be(point.Position);
        saved.Tolerance.Should().Be(point.Tolerance);
        saved.Memo.Should().Be("first pass");
        saved.CreatedAt.Should().Be(point.CreatedAt);
        saved.UpdatedAt.Should().Be(point.UpdatedAt);
        migrationCount.Should().Be(8);
    }

    [Fact]
    public async Task FindByNameAsync_Should_Be_Case_Insensitive_For_Duplicate_Validation()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateTeachingRepository();
        var point = CreatePoint("Safe Park");

        await repository.SaveAsync(point, CancellationToken.None);
        var found = await repository.FindByNameAsync("safe park", CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(point.Id);
    }

    [Fact]
    public async Task ListAsync_Should_Return_Recently_Updated_Points_First_And_Respect_Limit()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateTeachingRepository();
        var older = CreatePoint(
            "Older",
            Guid.Parse("50be255d-e26d-4529-bf3c-d04909638519"),
            DateTimeOffset.Parse("2026-06-01T07:00:00Z"));
        var newer = CreatePoint(
            "Newer",
            Guid.Parse("2aa24cfd-d1e9-41a2-980c-2292a0f013d5"),
            DateTimeOffset.Parse("2026-06-01T07:05:00Z"));

        await repository.SaveAsync(older, CancellationToken.None);
        await repository.SaveAsync(newer, CancellationToken.None);
        var points = await repository.ListAsync(1, CancellationToken.None);

        points.Should().ContainSingle();
        points[0].Id.Should().Be(newer.Id);
        points[0].Name.Should().Be("Newer");
    }

    [Fact]
    public async Task ListAsync_Should_Reject_NonPositive_Limit()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateTeachingRepository();

        var act = async () => await repository.ListAsync(0, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SaveAsync_Should_Update_Existing_Teaching_Point_By_Id()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateTeachingRepository();
        var point = CreatePoint("Load Position");
        var updated = point with
        {
            Name = "Load Position Revised",
            Role = TeachingRole.Load,
            Position = new Position4D(2.0, 3.0, 4.0, 5.0),
            Tolerance = new PositionTolerance(0.02, 0.03, 0.04, 0.05),
            Memo = null,
            UpdatedAt = point.UpdatedAt.AddMinutes(5)
        };

        await repository.SaveAsync(point, CancellationToken.None);
        await repository.SaveAsync(updated, CancellationToken.None);
        var saved = await repository.FindByIdAsync(point.Id, CancellationToken.None);
        var rowCount = await database.CountRowsAsync("teaching_points", CancellationToken.None);

        rowCount.Should().Be(1);
        saved.Should().Be(updated);
    }

    [Fact]
    public async Task FindByIdAsync_Should_Return_Null_When_Point_Does_Not_Exist()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateTeachingRepository();

        var saved = await repository.FindByIdAsync(Guid.NewGuid(), CancellationToken.None);
        var rowCount = await database.CountRowsAsync("teaching_points", CancellationToken.None);

        saved.Should().BeNull();
        rowCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_Teaching_Point_By_Id()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateTeachingRepository();
        var point = CreatePoint("Delete Target");

        await repository.SaveAsync(point, CancellationToken.None);
        await repository.DeleteAsync(point.Id, CancellationToken.None);
        var saved = await repository.FindByIdAsync(point.Id, CancellationToken.None);
        var rowCount = await database.CountRowsAsync("teaching_points", CancellationToken.None);

        saved.Should().BeNull();
        rowCount.Should().Be(0);
    }

    private static TeachingPoint CreatePoint(string name)
    {
        return CreatePoint(
            name,
            Guid.Parse("f37d1760-ef8e-4a82-84a5-91bf1248a615"),
            DateTimeOffset.Parse("2026-06-01T07:00:00Z"));
    }

    private static TeachingPoint CreatePoint(string name, Guid id, DateTimeOffset timestamp)
    {
        return TeachingPointFactory.Create(
            name,
            TeachingRole.Camera,
            new Position4D(10.0, 11.0, 12.0, 13.0),
            new PositionTolerance(0.01, 0.02, 0.03, 0.04),
            "first pass",
            id,
            timestamp).Point!;
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

        public SqliteTeachingPointRepository CreateTeachingRepository()
        {
            return new SqliteTeachingPointRepository(ConnectionFactory, SchemaInitializer);
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
