using Microsoft.Data.Sqlite;

namespace VisionCell.Persistence.Sqlite;

public sealed class SqliteSchemaInitializer
{
    private const string MotionHistoryMigrationId = "001_motion_command_history";
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteSchemaInitializer(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await CreateSchemaVersionTableAsync(connection, cancellationToken).ConfigureAwait(false);
        await CreateMotionCommandHistoryTableAsync(connection, cancellationToken).ConfigureAwait(false);
        await RecordMigrationAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateSchemaVersionTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_version (
              id TEXT PRIMARY KEY,
              applied_at TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateMotionCommandHistoryTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS motion_command_history (
              id TEXT PRIMARY KEY,
              correlation_id TEXT NOT NULL,
              command_name TEXT NOT NULL,
              axis_id TEXT NULL,
              request_json TEXT NOT NULL,
              result_json TEXT NOT NULL,
              elapsed_ms INTEGER NOT NULL,
              created_at TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RecordMigrationAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO schema_version (id, applied_at)
            VALUES ($id, $applied_at);
            """;
        command.Parameters.AddWithValue("$id", MotionHistoryMigrationId);
        command.Parameters.AddWithValue("$applied_at", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
