using Microsoft.Data.Sqlite;

namespace VisionCell.Persistence.Sqlite;

public sealed class SqliteSchemaInitializer
{
    private const string MotionHistoryMigrationId = "001_motion_command_history";
    private const string TeachingPointsMigrationId = "002_teaching_points";
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
        await RecordMigrationAsync(connection, MotionHistoryMigrationId, cancellationToken).ConfigureAwait(false);
        await CreateTeachingPointsTableAsync(connection, cancellationToken).ConfigureAwait(false);
        await RecordMigrationAsync(connection, TeachingPointsMigrationId, cancellationToken).ConfigureAwait(false);
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

    private static async Task CreateTeachingPointsTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS teaching_points (
              id TEXT PRIMARY KEY,
              name TEXT NOT NULL COLLATE NOCASE UNIQUE,
              role TEXT NOT NULL,
              x REAL NOT NULL,
              y REAL NOT NULL,
              z REAL NOT NULL,
              theta REAL NOT NULL,
              tolerance_x REAL NOT NULL,
              tolerance_y REAL NOT NULL,
              tolerance_z REAL NOT NULL,
              tolerance_theta REAL NOT NULL,
              memo TEXT NULL,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RecordMigrationAsync(
        SqliteConnection connection,
        string migrationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO schema_version (id, applied_at)
            VALUES ($id, $applied_at);
            """;
        command.Parameters.AddWithValue("$id", migrationId);
        command.Parameters.AddWithValue("$applied_at", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
