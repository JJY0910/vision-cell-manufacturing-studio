using Microsoft.Data.Sqlite;
using VisionCell.Application.Equipment;
using VisionCell.Equipment.Io;
using VisionCell.Persistence.Sqlite;

namespace VisionCell.Persistence.Equipment;

public sealed class SqliteEquipmentIoTransitionRepository : IEquipmentIoTransitionRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteSchemaInitializer _schemaInitializer;

    public SqliteEquipmentIoTransitionRepository(
        SqliteConnectionFactory connectionFactory,
        SqliteSchemaInitializer schemaInitializer)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
    }

    public async Task SaveAsync(IoTransitionRecord transition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transition);

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO io_transition_history (
              id,
              name,
              address,
              direction,
              previous_value,
              current_value,
              previous_forced,
              current_forced,
              source,
              correlation_id,
              operator_memo,
              changed_at
            )
            VALUES (
              $id,
              $name,
              $address,
              $direction,
              $previous_value,
              $current_value,
              $previous_forced,
              $current_forced,
              $source,
              $correlation_id,
              $operator_memo,
              $changed_at
            );
            """;
        WriteParameters(command, transition);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<IoTransitionRecord>> ListRecentAsync(
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
              name,
              address,
              direction,
              previous_value,
              current_value,
              previous_forced,
              current_forced,
              source,
              correlation_id,
              operator_memo,
              changed_at
            FROM io_transition_history
            ORDER BY changed_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var transitions = new List<IoTransitionRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            transitions.Add(ReadTransition(reader));
        }

        return transitions;
    }

    private static void WriteParameters(SqliteCommand command, IoTransitionRecord transition)
    {
        command.Parameters.AddWithValue("$id", transition.Id.ToString("N"));
        command.Parameters.AddWithValue("$name", transition.Name);
        command.Parameters.AddWithValue("$address", transition.Address);
        command.Parameters.AddWithValue("$direction", transition.Direction.ToString());
        command.Parameters.AddWithValue("$previous_value", transition.PreviousValue ? 1 : 0);
        command.Parameters.AddWithValue("$current_value", transition.CurrentValue ? 1 : 0);
        command.Parameters.AddWithValue("$previous_forced", transition.PreviousForced ? 1 : 0);
        command.Parameters.AddWithValue("$current_forced", transition.CurrentForced ? 1 : 0);
        command.Parameters.AddWithValue("$source", transition.Source);
        command.Parameters.AddWithValue("$correlation_id", transition.CorrelationId is null ? DBNull.Value : transition.CorrelationId);
        command.Parameters.AddWithValue("$operator_memo", transition.OperatorMemo is null ? DBNull.Value : transition.OperatorMemo);
        command.Parameters.AddWithValue("$changed_at", transition.ChangedAt.ToString("O"));
    }

    private static IoTransitionRecord ReadTransition(SqliteDataReader reader)
    {
        return new IoTransitionRecord(
            Guid.ParseExact(reader.GetString(0), "N"),
            reader.GetString(1),
            reader.GetString(2),
            Enum.Parse<IoBitDirection>(reader.GetString(3)),
            reader.GetInt64(4) != 0,
            reader.GetInt64(5) != 0,
            reader.GetInt64(6) != 0,
            reader.GetInt64(7) != 0,
            DateTimeOffset.Parse(reader.GetString(11)),
            reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10));
    }
}
