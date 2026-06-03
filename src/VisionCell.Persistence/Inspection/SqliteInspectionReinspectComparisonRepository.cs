using Microsoft.Data.Sqlite;
using VisionCell.Application.Inspection;
using VisionCell.Persistence.Sqlite;

namespace VisionCell.Persistence.Inspection;

public sealed class SqliteInspectionReinspectComparisonRepository :
    IInspectionReinspectComparisonRepository,
    IInspectionReinspectComparisonReader
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteSchemaInitializer _schemaInitializer;

    public SqliteInspectionReinspectComparisonRepository(
        SqliteConnectionFactory connectionFactory,
        SqliteSchemaInitializer schemaInitializer)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
    }

    public async Task SaveAsync(
        InspectionReinspectComparisonResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO inspection_reinspect_comparisons (
              id,
              source_result_id,
              replay_correlation_id,
              lot_id,
              recipe_id,
              recipe_version,
              previous_judgment,
              replayed_judgment,
              previous_defect_count,
              replayed_defect_count,
              previous_cycle_time_ms,
              replayed_cycle_time_ms,
              status,
              compared_at,
              persistence_status,
              message
            )
            VALUES (
              $id,
              $source_result_id,
              $replay_correlation_id,
              $lot_id,
              $recipe_id,
              $recipe_version,
              $previous_judgment,
              $replayed_judgment,
              $previous_defect_count,
              $replayed_defect_count,
              $previous_cycle_time_ms,
              $replayed_cycle_time_ms,
              $status,
              $compared_at,
              $persistence_status,
              $message
            );
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$source_result_id", result.SourceResultId.ToString("N"));
        command.Parameters.AddWithValue("$replay_correlation_id", result.ReplayCorrelationId);
        command.Parameters.AddWithValue("$lot_id", result.LotId);
        command.Parameters.AddWithValue("$recipe_id", result.RecipeId);
        command.Parameters.AddWithValue("$recipe_version", result.RecipeVersion);
        command.Parameters.AddWithValue("$previous_judgment", result.PreviousJudgment);
        command.Parameters.AddWithValue("$replayed_judgment", result.ReplayedJudgment);
        command.Parameters.AddWithValue("$previous_defect_count", result.PreviousDefectCount);
        command.Parameters.AddWithValue("$replayed_defect_count", result.ReplayedDefectCount);
        command.Parameters.AddWithValue("$previous_cycle_time_ms", Convert.ToInt64(result.PreviousCycleTime.TotalMilliseconds));
        command.Parameters.AddWithValue("$replayed_cycle_time_ms", Convert.ToInt64(result.ReplayedCycleTime.TotalMilliseconds));
        command.Parameters.AddWithValue("$status", result.Status.ToString());
        command.Parameters.AddWithValue("$compared_at", result.ComparedAt.ToString("O"));
        command.Parameters.AddWithValue("$persistence_status", result.PersistenceStatus);
        command.Parameters.AddWithValue("$message", result.Message);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<InspectionReinspectComparisonResult>> ListRecentAsync(
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
              source_result_id,
              replay_correlation_id,
              lot_id,
              recipe_id,
              recipe_version,
              previous_judgment,
              replayed_judgment,
              previous_defect_count,
              replayed_defect_count,
              previous_cycle_time_ms,
              replayed_cycle_time_ms,
              status,
              compared_at,
              persistence_status,
              message
            FROM inspection_reinspect_comparisons
            ORDER BY compared_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<InspectionReinspectComparisonResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new InspectionReinspectComparisonResult(
                Guid.ParseExact(reader.GetString(0), "N"),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                TimeSpan.FromMilliseconds(reader.GetInt64(9)),
                TimeSpan.FromMilliseconds(reader.GetInt64(10)),
                Enum.Parse<InspectionReinspectComparisonStatus>(reader.GetString(11)),
                DateTimeOffset.Parse(reader.GetString(12)),
                reader.GetString(13),
                reader.GetString(14)));
        }

        return results;
    }
}
