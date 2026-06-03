using FluentAssertions;
using VisionCell.Application.Recipes;
using VisionCell.Persistence.Recipes;
using VisionCell.Persistence.Sqlite;
using Xunit;

namespace VisionCell_Persistence_Tests;

public sealed class SqliteRecipeIndexRepositoryTests
{
    [Fact]
    public async Task SaveAsync_Should_Create_Schema_And_Insert_Recipe_Index()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var entry = CreateEntry("PKG-MEMORY-MODULE", "1.0.0", "Memory Module", true, true, null, "2026-06-01T08:00:00Z");

        await repository.SaveAsync(entry, CancellationToken.None);
        var saved = await repository.FindAsync(entry.RecipeId, entry.Version, CancellationToken.None);
        var migrationCount = await database.CountRowsAsync("schema_version", CancellationToken.None);

        saved.Should().Be(entry);
        migrationCount.Should().Be(7);
    }

    [Fact]
    public async Task SaveAsync_Should_Upsert_By_Recipe_Id_And_Version()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var first = CreateEntry("PKG-MEMORY-MODULE", "1.0.0", "Memory Module", false, false, "missing ROI", "2026-06-01T08:00:00Z");
        var updated = first with
        {
            Id = Guid.Parse("99999999-0000-0000-0000-000000000001"),
            ProductName = "Memory Module Revised",
            IsActive = true,
            IsValid = true,
            ValidationSummary = null,
            UpdatedAt = DateTimeOffset.Parse("2026-06-01T08:05:00Z")
        };

        await repository.SaveAsync(first, CancellationToken.None);
        await repository.SaveAsync(updated, CancellationToken.None);
        var saved = await repository.FindAsync(first.RecipeId, first.Version, CancellationToken.None);
        var rowCount = await database.CountRowsAsync("recipes", CancellationToken.None);

        rowCount.Should().Be(1);
        saved.Should().Be(updated);
    }

    [Fact]
    public async Task ListRecentAsync_Should_Return_Recently_Updated_Recipes_First_And_Respect_Limit()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var older = CreateEntry("PKG-A", "1.0.0", "Package A", false, true, null, "2026-06-01T08:00:00Z");
        var newer = CreateEntry("PKG-B", "1.1.0", "Package B", true, true, null, "2026-06-01T08:10:00Z");

        await repository.SaveAsync(older, CancellationToken.None);
        await repository.SaveAsync(newer, CancellationToken.None);
        var entries = await repository.ListRecentAsync(1, CancellationToken.None);

        entries.Should().ContainSingle();
        entries[0].RecipeId.Should().Be("PKG-B");
        entries[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task FindAsync_Should_Be_Case_Insensitive_For_Recipe_Id()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var entry = CreateEntry("PKG-MEMORY-MODULE", "1.0.0", "Memory Module", false, true, null, "2026-06-01T08:00:00Z");

        await repository.SaveAsync(entry, CancellationToken.None);
        var saved = await repository.FindAsync("pkg-memory-module", "1.0.0", CancellationToken.None);

        saved.Should().Be(entry);
    }

    [Fact]
    public async Task FindActiveAsync_Should_Return_Null_When_No_Active_Recipe_Exists()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var entry = CreateEntry("PKG-MEMORY-MODULE", "1.0.0", "Memory Module", false, true, null, "2026-06-01T08:00:00Z");

        await repository.SaveAsync(entry, CancellationToken.None);
        var active = await repository.FindActiveAsync(CancellationToken.None);

        active.Should().BeNull();
    }

    [Fact]
    public async Task SetActiveAsync_Should_Select_One_Recipe_And_Clear_Previous_Active_Row()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var previous = CreateEntry("PKG-A", "1.0.0", "Package A", true, true, null, "2026-06-01T08:00:00Z");
        var next = CreateEntry("PKG-B", "2.0.0", "Package B", false, true, null, "2026-06-01T08:10:00Z");

        await repository.SaveAsync(previous, CancellationToken.None);
        await repository.SaveAsync(next, CancellationToken.None);
        var selected = await repository.SetActiveAsync("pkg-b", "2.0.0", CancellationToken.None);
        var active = await repository.FindActiveAsync(CancellationToken.None);
        var entries = await repository.ListRecentAsync(10, CancellationToken.None);

        selected.Should().BeTrue();
        active.Should().NotBeNull();
        active!.RecipeId.Should().Be("PKG-B");
        entries.Should().ContainSingle(entry => entry.IsActive);
        entries.Single(entry => entry.RecipeId == "PKG-A").IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveAsync_Should_Return_False_And_Preserve_Active_Row_When_Target_Is_Missing()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();
        var activeEntry = CreateEntry("PKG-A", "1.0.0", "Package A", true, true, null, "2026-06-01T08:00:00Z");

        await repository.SaveAsync(activeEntry, CancellationToken.None);
        var selected = await repository.SetActiveAsync("PKG-MISSING", "1.0.0", CancellationToken.None);
        var active = await repository.FindActiveAsync(CancellationToken.None);

        selected.Should().BeFalse();
        active.Should().Be(activeEntry);
    }

    [Fact]
    public async Task SetActiveAsync_Should_Return_False_For_Blank_Identifiers()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();

        var selected = await repository.SetActiveAsync(" ", "1.0.0", CancellationToken.None);

        selected.Should().BeFalse();
    }

    [Fact]
    public async Task ListRecentAsync_Should_Reject_NonPositive_Limit()
    {
        using var database = TemporaryDatabase.Create();
        var repository = database.CreateRepository();

        var act = async () => await repository.ListRecentAsync(0, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private static RecipeIndexEntry CreateEntry(
        string recipeId,
        string version,
        string productName,
        bool isActive,
        bool isValid,
        string? validationSummary,
        string updatedAt)
    {
        return new RecipeIndexEntry(
            Guid.NewGuid(),
            recipeId,
            version,
            productName,
            $"recipes/{recipeId}.v{version}.recipe.json",
            $"sha256:{recipeId}:{version}",
            isActive,
            isValid,
            validationSummary,
            DateTimeOffset.Parse("2026-06-01T07:00:00Z"),
            DateTimeOffset.Parse(updatedAt));
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

        public SqliteRecipeIndexRepository CreateRepository()
        {
            return new SqliteRecipeIndexRepository(ConnectionFactory, SchemaInitializer);
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
