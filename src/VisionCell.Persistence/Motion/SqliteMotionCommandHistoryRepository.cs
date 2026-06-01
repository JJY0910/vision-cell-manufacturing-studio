using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using VisionCell.Application.Motion;
using VisionCell.Persistence.Sqlite;

namespace VisionCell.Persistence.Motion;

public sealed class SqliteMotionCommandHistoryRepository : IMotionCommandHistoryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteSchemaInitializer _schemaInitializer;

    public SqliteMotionCommandHistoryRepository(
        SqliteConnectionFactory connectionFactory,
        SqliteSchemaInitializer schemaInitializer)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
    }

    public async Task SaveAsync(MotionCommandHistoryEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO motion_command_history (
              id,
              correlation_id,
              command_name,
              axis_id,
              request_json,
              result_json,
              elapsed_ms,
              created_at
            )
            VALUES (
              $id,
              $correlation_id,
              $command_name,
              $axis_id,
              $request_json,
              $result_json,
              $elapsed_ms,
              $created_at
            );
            """;
        command.Parameters.AddWithValue("$id", entry.Id.ToString("N"));
        command.Parameters.AddWithValue("$correlation_id", entry.Request.CorrelationId.ToString());
        command.Parameters.AddWithValue("$command_name", entry.Request.CommandName);
        command.Parameters.AddWithValue("$axis_id", GetAxisId(entry) is { } axisId ? axisId : DBNull.Value);
        command.Parameters.AddWithValue("$request_json", JsonSerializer.Serialize(entry.Request, JsonOptions));
        command.Parameters.AddWithValue("$result_json", JsonSerializer.Serialize(entry.CommandResult, JsonOptions));
        command.Parameters.AddWithValue("$elapsed_ms", Convert.ToInt64(entry.CommandResult.Elapsed.TotalMilliseconds));
        command.Parameters.AddWithValue("$created_at", entry.CreatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MotionCommandHistoryRow>> ListRecentAsync(
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
              correlation_id,
              command_name,
              axis_id,
              request_json,
              result_json,
              elapsed_ms,
              created_at
            FROM motion_command_history
            ORDER BY created_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var rows = new List<MotionCommandHistoryRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    private static MotionCommandHistoryRow ReadRow(SqliteDataReader reader)
    {
        return new MotionCommandHistoryRow(
            Guid.ParseExact(reader.GetString(0), "N"),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt64(6),
            DateTimeOffset.Parse(reader.GetString(7)));
    }

    private static string? GetAxisId(MotionCommandHistoryEntry entry)
    {
        return entry.Request.Parameters.TryGetValue("axis", out var axisId)
            ? axisId
            : null;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
