using Microsoft.Data.Sqlite;
using VisionCell.Application.Recipes;
using VisionCell.Persistence.Sqlite;

namespace VisionCell.Persistence.Recipes;

public sealed class SqliteRecipeIndexRepository : IRecipeIndexRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteSchemaInitializer _schemaInitializer;

    public SqliteRecipeIndexRepository(
        SqliteConnectionFactory connectionFactory,
        SqliteSchemaInitializer schemaInitializer)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
    }

    public async Task SaveAsync(RecipeIndexEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO recipes (
              id,
              recipe_id,
              version,
              product_name,
              file_path,
              checksum,
              is_active,
              is_valid,
              validation_summary,
              created_at,
              updated_at
            )
            VALUES (
              $id,
              $recipe_id,
              $version,
              $product_name,
              $file_path,
              $checksum,
              $is_active,
              $is_valid,
              $validation_summary,
              $created_at,
              $updated_at
            )
            ON CONFLICT(recipe_id, version) DO UPDATE SET
              id = excluded.id,
              product_name = excluded.product_name,
              file_path = excluded.file_path,
              checksum = excluded.checksum,
              is_active = excluded.is_active,
              is_valid = excluded.is_valid,
              validation_summary = excluded.validation_summary,
              created_at = excluded.created_at,
              updated_at = excluded.updated_at;
            """;
        AddEntryParameters(command, entry);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RecipeIndexEntry?> FindAsync(
        string recipeId,
        string version,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipeId) || string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
              id,
              recipe_id,
              version,
              product_name,
              file_path,
              checksum,
              is_active,
              is_valid,
              validation_summary,
              created_at,
              updated_at
            FROM recipes
            WHERE recipe_id = $recipe_id COLLATE NOCASE
              AND version = $version
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$recipe_id", recipeId.Trim());
        command.Parameters.AddWithValue("$version", version.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadEntry(reader)
            : null;
    }

    public async Task<RecipeIndexEntry?> FindActiveAsync(CancellationToken cancellationToken)
    {
        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
              id,
              recipe_id,
              version,
              product_name,
              file_path,
              checksum,
              is_active,
              is_valid,
              validation_summary,
              created_at,
              updated_at
            FROM recipes
            WHERE is_active = 1
            ORDER BY updated_at DESC, recipe_id ASC, version DESC
            LIMIT 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadEntry(reader)
            : null;
    }

    public async Task<bool> SetActiveAsync(
        string recipeId,
        string version,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipeId) || string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var normalizedRecipeId = recipeId.Trim();
        var normalizedVersion = version.Trim();
        var updatedAt = DateTimeOffset.UtcNow.ToString("O");

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.Transaction = transaction;
            existsCommand.CommandText = """
                SELECT COUNT(*)
                FROM recipes
                WHERE recipe_id = $recipe_id COLLATE NOCASE
                  AND version = $version;
                """;
            existsCommand.Parameters.AddWithValue("$recipe_id", normalizedRecipeId);
            existsCommand.Parameters.AddWithValue("$version", normalizedVersion);

            var result = await existsCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (Convert.ToInt32(result) == 0)
            {
                return false;
            }
        }

        await using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = """
                UPDATE recipes
                SET
                  is_active = CASE
                    WHEN recipe_id = $recipe_id COLLATE NOCASE AND version = $version THEN 1
                    ELSE 0
                  END,
                  updated_at = CASE
                    WHEN recipe_id = $recipe_id COLLATE NOCASE AND version = $version THEN $updated_at
                    ELSE updated_at
                  END;
                """;
            updateCommand.Parameters.AddWithValue("$recipe_id", normalizedRecipeId);
            updateCommand.Parameters.AddWithValue("$version", normalizedVersion);
            updateCommand.Parameters.AddWithValue("$updated_at", updatedAt);

            await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<RecipeIndexEntry>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be greater than zero.");
        }

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
              id,
              recipe_id,
              version,
              product_name,
              file_path,
              checksum,
              is_active,
              is_valid,
              validation_summary,
              created_at,
              updated_at
            FROM recipes
            ORDER BY updated_at DESC, recipe_id ASC, version DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var entries = new List<RecipeIndexEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    private static void AddEntryParameters(SqliteCommand command, RecipeIndexEntry entry)
    {
        command.Parameters.AddWithValue("$id", entry.Id.ToString("N"));
        command.Parameters.AddWithValue("$recipe_id", entry.RecipeId.Trim());
        command.Parameters.AddWithValue("$version", entry.Version.Trim());
        command.Parameters.AddWithValue("$product_name", entry.ProductName.Trim());
        command.Parameters.AddWithValue("$file_path", entry.DocumentPath);
        command.Parameters.AddWithValue("$checksum", entry.Checksum);
        command.Parameters.AddWithValue("$is_active", entry.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$is_valid", entry.IsValid ? 1 : 0);
        command.Parameters.AddWithValue("$validation_summary", entry.ValidationSummary is null ? DBNull.Value : entry.ValidationSummary);
        command.Parameters.AddWithValue("$created_at", entry.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated_at", entry.UpdatedAt.ToString("O"));
    }

    private static RecipeIndexEntry ReadEntry(SqliteDataReader reader)
    {
        return new RecipeIndexEntry(
            Guid.ParseExact(reader.GetString(0), "N"),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6) != 0,
            reader.GetInt32(7) != 0,
            reader.IsDBNull(8) ? null : reader.GetString(8),
            DateTimeOffset.Parse(reader.GetString(9)),
            DateTimeOffset.Parse(reader.GetString(10)));
    }
}
