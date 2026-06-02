using Microsoft.Data.Sqlite;
using VisionCell.Application.Alarms;
using VisionCell.Core.Alarms;
using VisionCell.Persistence.Sqlite;

namespace VisionCell.Persistence.Alarms;

public sealed class SqliteEquipmentAlarmRepository : IEquipmentAlarmRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteSchemaInitializer _schemaInitializer;

    public SqliteEquipmentAlarmRepository(
        SqliteConnectionFactory connectionFactory,
        SqliteSchemaInitializer schemaInitializer)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
    }

    public async Task SaveAsync(EquipmentAlarm alarm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alarm);

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO equipment_alarms (
              id,
              code,
              severity,
              area,
              message,
              correlation_id,
              occurred_at,
              acknowledged_at,
              action_memo
            )
            VALUES (
              $id,
              $code,
              $severity,
              $area,
              $message,
              $correlation_id,
              $occurred_at,
              $acknowledged_at,
              $action_memo
            );
            """;
        WriteAlarmParameters(command, alarm);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EquipmentAlarm>> ListRecentAsync(
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
              code,
              severity,
              area,
              message,
              correlation_id,
              occurred_at,
              acknowledged_at,
              action_memo
            FROM equipment_alarms
            ORDER BY occurred_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var alarms = new List<EquipmentAlarm>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            alarms.Add(ReadAlarm(reader));
        }

        return alarms;
    }

    public async Task AcknowledgeAsync(
        Guid alarmId,
        DateTimeOffset acknowledgedAt,
        string? actionMemo,
        CancellationToken cancellationToken)
    {
        if (alarmId == Guid.Empty)
        {
            throw new ArgumentException("Alarm ID is required.", nameof(alarmId));
        }

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE equipment_alarms
            SET acknowledged_at = $acknowledged_at,
                action_memo = $action_memo
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", alarmId.ToString("N"));
        command.Parameters.AddWithValue("$acknowledged_at", acknowledgedAt.ToString("O"));
        command.Parameters.AddWithValue("$action_memo", string.IsNullOrWhiteSpace(actionMemo) ? DBNull.Value : actionMemo.Trim());

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new InvalidOperationException($"Alarm '{alarmId:N}' was not found.");
        }
    }

    private static void WriteAlarmParameters(SqliteCommand command, EquipmentAlarm alarm)
    {
        command.Parameters.AddWithValue("$id", alarm.Id.ToString("N"));
        command.Parameters.AddWithValue("$code", alarm.Code);
        command.Parameters.AddWithValue("$severity", alarm.Severity.ToString());
        command.Parameters.AddWithValue("$area", alarm.Area.ToString());
        command.Parameters.AddWithValue("$message", alarm.Message);
        command.Parameters.AddWithValue("$correlation_id", alarm.CorrelationId is null ? DBNull.Value : alarm.CorrelationId);
        command.Parameters.AddWithValue("$occurred_at", alarm.OccurredAt.ToString("O"));
        command.Parameters.AddWithValue("$acknowledged_at", alarm.AcknowledgedAt is null ? DBNull.Value : alarm.AcknowledgedAt.Value.ToString("O"));
        command.Parameters.AddWithValue("$action_memo", alarm.ActionMemo is null ? DBNull.Value : alarm.ActionMemo);
    }

    private static EquipmentAlarm ReadAlarm(SqliteDataReader reader)
    {
        return new EquipmentAlarm(
            Guid.ParseExact(reader.GetString(0), "N"),
            reader.GetString(1),
            Enum.Parse<EquipmentAlarmSeverity>(reader.GetString(2)),
            Enum.Parse<EquipmentArea>(reader.GetString(3)),
            reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(6)),
            reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }
}
