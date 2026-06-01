using Microsoft.Data.Sqlite;

namespace VisionCell.Persistence.Sqlite;

public sealed class SqliteSchemaInitializer
{
    private const string MotionHistoryMigrationId = "001_motion_command_history";
    private const string TeachingPointsMigrationId = "002_teaching_points";
    private const string TeachingHistoryMigrationId = "003_teaching_history";
    private const string RecipesMigrationId = "004_recipes";
    private const string InspectionResultsMigrationId = "005_inspection_results";
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
        await CreateTeachingHistoryTableAsync(connection, cancellationToken).ConfigureAwait(false);
        await RecordMigrationAsync(connection, TeachingHistoryMigrationId, cancellationToken).ConfigureAwait(false);
        await CreateRecipesTableAsync(connection, cancellationToken).ConfigureAwait(false);
        await RecordMigrationAsync(connection, RecipesMigrationId, cancellationToken).ConfigureAwait(false);
        await CreateInspectionResultTablesAsync(connection, cancellationToken).ConfigureAwait(false);
        await RecordMigrationAsync(connection, InspectionResultsMigrationId, cancellationToken).ConfigureAwait(false);
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

    private static async Task CreateTeachingHistoryTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS teaching_history (
              id TEXT PRIMARY KEY,
              teaching_point_id TEXT NOT NULL,
              recipe_id TEXT NULL,
              action TEXT NOT NULL,
              before_json TEXT NULL,
              after_json TEXT NULL,
              created_at TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateRecipesTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS recipes (
              id TEXT PRIMARY KEY,
              recipe_id TEXT NOT NULL,
              version TEXT NOT NULL,
              product_name TEXT NOT NULL,
              file_path TEXT NOT NULL,
              checksum TEXT NOT NULL,
              is_active INTEGER NOT NULL DEFAULT 0,
              is_valid INTEGER NOT NULL DEFAULT 0,
              validation_summary TEXT NULL,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL,
              UNIQUE(recipe_id, version)
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateInspectionResultTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS inspection_results (
              id TEXT PRIMARY KEY,
              correlation_id TEXT NOT NULL,
              lot_id TEXT NOT NULL,
              recipe_id TEXT NOT NULL,
              recipe_version TEXT NOT NULL,
              judgment TEXT NOT NULL,
              defect_summary TEXT NULL,
              source_image_path TEXT NOT NULL,
              overlay_image_path TEXT NULL,
              height_map_path TEXT NULL,
              cycle_time_ms INTEGER NOT NULL,
              step_timings_json TEXT NOT NULL,
              parameters_json TEXT NOT NULL,
              created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS defects (
              id TEXT PRIMARY KEY,
              result_id TEXT NOT NULL,
              defect_type TEXT NOT NULL,
              score REAL NOT NULL,
              roi_id TEXT NULL,
              bbox_x INTEGER NOT NULL,
              bbox_y INTEGER NOT NULL,
              bbox_w INTEGER NOT NULL,
              bbox_h INTEGER NOT NULL,
              message TEXT NULL,
              FOREIGN KEY(result_id) REFERENCES inspection_results(id)
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
