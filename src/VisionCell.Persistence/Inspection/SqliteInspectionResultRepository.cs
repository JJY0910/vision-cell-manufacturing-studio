using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using VisionCell.Application.Inspection;
using VisionCell.Persistence.Sqlite;
using VisionCell.Vision.Inspection;

namespace VisionCell.Persistence.Inspection;

public sealed class SqliteInspectionResultRepository : IInspectionResultRepository, IInspectionResultReader
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly SqliteSchemaInitializer _schemaInitializer;

    public SqliteInspectionResultRepository(
        SqliteConnectionFactory connectionFactory,
        SqliteSchemaInitializer schemaInitializer)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
    }

    public async Task SaveAsync(InspectionResultSaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _schemaInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await InsertResultAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
        foreach (var defect in request.Defects)
        {
            await InsertDefectAsync(connection, transaction, request.Id, defect, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<InspectionResultRecord>> ListRecentAsync(
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
              lot_id,
              recipe_id,
              recipe_version,
              judgment,
              defect_summary,
              source_image_path,
              overlay_image_path,
              height_map_path,
              cycle_time_ms,
              created_at
            FROM inspection_results
            ORDER BY created_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var rows = new List<InspectionResultRow>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new InspectionResultRow(
                    Guid.ParseExact(reader.GetString(0), "N"),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    Enum.Parse<Judgment>(reader.GetString(5)),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    TimeSpan.FromMilliseconds(reader.GetInt64(10)),
                    DateTimeOffset.Parse(reader.GetString(11))));
            }
        }

        var records = new List<InspectionResultRecord>();
        foreach (var row in rows)
        {
            records.Add(new InspectionResultRecord(
                row.Id,
                row.CorrelationId,
                row.LotId,
                row.RecipeId,
                row.RecipeVersion,
                row.Judgment,
                row.DefectSummary,
                row.SourceImagePath,
                row.OverlayImagePath,
                row.HeightMapPath,
                row.CycleTime,
                row.CreatedAt,
                await ListDefectsAsync(connection, row.Id, cancellationToken).ConfigureAwait(false)));
        }

        return records;
    }

    private static async Task InsertResultAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        InspectionResultSaveRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO inspection_results (
              id,
              correlation_id,
              lot_id,
              recipe_id,
              recipe_version,
              judgment,
              defect_summary,
              source_image_path,
              overlay_image_path,
              height_map_path,
              cycle_time_ms,
              step_timings_json,
              parameters_json,
              created_at
            )
            VALUES (
              $id,
              $correlation_id,
              $lot_id,
              $recipe_id,
              $recipe_version,
              $judgment,
              $defect_summary,
              $source_image_path,
              $overlay_image_path,
              $height_map_path,
              $cycle_time_ms,
              $step_timings_json,
              $parameters_json,
              $created_at
            );
            """;
        command.Parameters.AddWithValue("$id", request.Id.ToString("N"));
        command.Parameters.AddWithValue("$correlation_id", request.CorrelationId);
        command.Parameters.AddWithValue("$lot_id", request.LotId);
        command.Parameters.AddWithValue("$recipe_id", request.RecipeId);
        command.Parameters.AddWithValue("$recipe_version", request.RecipeVersion);
        command.Parameters.AddWithValue("$judgment", request.Judgment.ToString());
        command.Parameters.AddWithValue("$defect_summary", request.DefectSummary is null ? DBNull.Value : request.DefectSummary);
        command.Parameters.AddWithValue("$source_image_path", request.SourceImagePath);
        command.Parameters.AddWithValue("$overlay_image_path", request.OverlayImagePath is null ? DBNull.Value : request.OverlayImagePath);
        command.Parameters.AddWithValue("$height_map_path", request.HeightMapPath is null ? DBNull.Value : request.HeightMapPath);
        command.Parameters.AddWithValue("$cycle_time_ms", Convert.ToInt64(request.CycleTime.TotalMilliseconds));
        command.Parameters.AddWithValue("$step_timings_json", JsonSerializer.Serialize(request.StepTimings, JsonOptions));
        command.Parameters.AddWithValue("$parameters_json", JsonSerializer.Serialize(request.Parameters, JsonOptions));
        command.Parameters.AddWithValue("$created_at", request.CreatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertDefectAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid resultId,
        InspectionDefectRecord defect,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO defects (
              id,
              result_id,
              defect_type,
              score,
              roi_id,
              bbox_x,
              bbox_y,
              bbox_w,
              bbox_h,
              message
            )
            VALUES (
              $id,
              $result_id,
              $defect_type,
              $score,
              $roi_id,
              $bbox_x,
              $bbox_y,
              $bbox_w,
              $bbox_h,
              $message
            );
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$result_id", resultId.ToString("N"));
        command.Parameters.AddWithValue("$defect_type", defect.Type);
        command.Parameters.AddWithValue("$score", defect.Score);
        command.Parameters.AddWithValue("$roi_id", defect.RoiId is null ? DBNull.Value : defect.RoiId);
        command.Parameters.AddWithValue("$bbox_x", defect.X);
        command.Parameters.AddWithValue("$bbox_y", defect.Y);
        command.Parameters.AddWithValue("$bbox_w", defect.Width);
        command.Parameters.AddWithValue("$bbox_h", defect.Height);
        command.Parameters.AddWithValue("$message", defect.Message is null ? DBNull.Value : defect.Message);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<InspectionDefectRecord>> ListDefectsAsync(
        SqliteConnection connection,
        Guid resultId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
              defect_type,
              score,
              roi_id,
              bbox_x,
              bbox_y,
              bbox_w,
              bbox_h,
              message
            FROM defects
            WHERE result_id = $result_id
            ORDER BY defect_type, id;
            """;
        command.Parameters.AddWithValue("$result_id", resultId.ToString("N"));

        var defects = new List<InspectionDefectRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            defects.Add(new InspectionDefectRecord(
                reader.GetString(0),
                reader.GetDouble(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return defects;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record InspectionResultRow(
        Guid Id,
        string CorrelationId,
        string LotId,
        string RecipeId,
        string RecipeVersion,
        Judgment Judgment,
        string? DefectSummary,
        string SourceImagePath,
        string? OverlayImagePath,
        string? HeightMapPath,
        TimeSpan CycleTime,
        DateTimeOffset CreatedAt);
}
