using Microsoft.Data.Sqlite;
using VisionCell.Application.Teaching;
using VisionCell.Core.Primitives;
using VisionCell.Motion.Teaching;
using VisionCell.Persistence.Sqlite;

namespace VisionCell.Persistence.Teaching;

public sealed class SqliteTeachingPointRepository : ITeachingPointRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteSchemaInitializer _schemaInitializer;

    public SqliteTeachingPointRepository(
        SqliteConnectionFactory connectionFactory,
        SqliteSchemaInitializer schemaInitializer)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
    }

    public async Task<TeachingPoint?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
              id,
              name,
              role,
              x,
              y,
              z,
              theta,
              tolerance_x,
              tolerance_y,
              tolerance_z,
              tolerance_theta,
              memo,
              created_at,
              updated_at
            FROM teaching_points
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("N"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadPoint(reader)
            : null;
    }

    public async Task<TeachingPoint?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
              id,
              name,
              role,
              x,
              y,
              z,
              theta,
              tolerance_x,
              tolerance_y,
              tolerance_z,
              tolerance_theta,
              memo,
              created_at,
              updated_at
            FROM teaching_points
            WHERE name = $name COLLATE NOCASE
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$name", name.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadPoint(reader)
            : null;
    }

    public async Task SaveAsync(TeachingPoint point, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(point);

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO teaching_points (
              id,
              name,
              role,
              x,
              y,
              z,
              theta,
              tolerance_x,
              tolerance_y,
              tolerance_z,
              tolerance_theta,
              memo,
              created_at,
              updated_at
            )
            VALUES (
              $id,
              $name,
              $role,
              $x,
              $y,
              $z,
              $theta,
              $tolerance_x,
              $tolerance_y,
              $tolerance_z,
              $tolerance_theta,
              $memo,
              $created_at,
              $updated_at
            )
            ON CONFLICT(id) DO UPDATE SET
              name = excluded.name,
              role = excluded.role,
              x = excluded.x,
              y = excluded.y,
              z = excluded.z,
              theta = excluded.theta,
              tolerance_x = excluded.tolerance_x,
              tolerance_y = excluded.tolerance_y,
              tolerance_z = excluded.tolerance_z,
              tolerance_theta = excluded.tolerance_theta,
              memo = excluded.memo,
              created_at = excluded.created_at,
              updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$id", point.Id.ToString("N"));
        command.Parameters.AddWithValue("$name", point.Name);
        command.Parameters.AddWithValue("$role", point.Role.ToString());
        command.Parameters.AddWithValue("$x", point.Position.X);
        command.Parameters.AddWithValue("$y", point.Position.Y);
        command.Parameters.AddWithValue("$z", point.Position.Z);
        command.Parameters.AddWithValue("$theta", point.Position.Theta);
        command.Parameters.AddWithValue("$tolerance_x", point.Tolerance.X);
        command.Parameters.AddWithValue("$tolerance_y", point.Tolerance.Y);
        command.Parameters.AddWithValue("$tolerance_z", point.Tolerance.Z);
        command.Parameters.AddWithValue("$tolerance_theta", point.Tolerance.Theta);
        command.Parameters.AddWithValue("$memo", point.Memo is null ? DBNull.Value : point.Memo);
        command.Parameters.AddWithValue("$created_at", point.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated_at", point.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TeachingPoint ReadPoint(SqliteDataReader reader)
    {
        return new TeachingPoint(
            Guid.ParseExact(reader.GetString(0), "N"),
            reader.GetString(1),
            Enum.Parse<TeachingRole>(reader.GetString(2)),
            new Position4D(
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetDouble(6)),
            new PositionTolerance(
                reader.GetDouble(7),
                reader.GetDouble(8),
                reader.GetDouble(9),
                reader.GetDouble(10)),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            DateTimeOffset.Parse(reader.GetString(12)),
            DateTimeOffset.Parse(reader.GetString(13)));
    }
}
