using Microsoft.Data.Sqlite;
using VisionCell.Application.Teaching;
using VisionCell.Persistence.Sqlite;

namespace VisionCell.Persistence.Teaching;

public sealed class SqliteTeachingHistoryRepository : ITeachingHistoryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteSchemaInitializer _schemaInitializer;

    public SqliteTeachingHistoryRepository(
        SqliteConnectionFactory connectionFactory,
        SqliteSchemaInitializer schemaInitializer)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
    }

    public async Task SaveAsync(TeachingHistoryEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO teaching_history (
              id,
              teaching_point_id,
              recipe_id,
              action,
              before_json,
              after_json,
              created_at
            )
            VALUES (
              $id,
              $teaching_point_id,
              $recipe_id,
              $action,
              $before_json,
              $after_json,
              $created_at
            );
            """;
        command.Parameters.AddWithValue("$id", entry.Id.ToString("N"));
        command.Parameters.AddWithValue("$teaching_point_id", entry.TeachingPointId.ToString("N"));
        command.Parameters.AddWithValue("$recipe_id", entry.RecipeId is null ? DBNull.Value : entry.RecipeId);
        command.Parameters.AddWithValue("$action", entry.Action.ToString());
        command.Parameters.AddWithValue("$before_json", entry.BeforeJson is null ? DBNull.Value : entry.BeforeJson);
        command.Parameters.AddWithValue("$after_json", entry.AfterJson is null ? DBNull.Value : entry.AfterJson);
        command.Parameters.AddWithValue("$created_at", entry.CreatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TeachingHistoryEntry>> ListByPointAsync(
        Guid teachingPointId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (teachingPointId == Guid.Empty)
        {
            throw new ArgumentException("Teaching point id must not be empty.", nameof(teachingPointId));
        }

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
              teaching_point_id,
              recipe_id,
              action,
              before_json,
              after_json,
              created_at
            FROM teaching_history
            WHERE teaching_point_id = $teaching_point_id
            ORDER BY created_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$teaching_point_id", teachingPointId.ToString("N"));
        command.Parameters.AddWithValue("$limit", limit);

        var entries = new List<TeachingHistoryEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    private static TeachingHistoryEntry ReadEntry(SqliteDataReader reader)
    {
        return new TeachingHistoryEntry(
            Guid.ParseExact(reader.GetString(0), "N"),
            Guid.ParseExact(reader.GetString(1), "N"),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            Enum.Parse<TeachingHistoryAction>(reader.GetString(3)),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            DateTimeOffset.Parse(reader.GetString(6)));
    }
}
