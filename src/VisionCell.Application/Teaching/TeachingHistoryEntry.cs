namespace VisionCell.Application.Teaching;

public sealed record TeachingHistoryEntry(
    Guid Id,
    Guid TeachingPointId,
    string? RecipeId,
    TeachingHistoryAction Action,
    string? BeforeJson,
    string? AfterJson,
    DateTimeOffset CreatedAt)
{
    public static TeachingHistoryEntry Create(
        Guid teachingPointId,
        string? recipeId,
        TeachingHistoryAction action,
        string? beforeJson,
        string? afterJson,
        Func<DateTimeOffset>? clock = null)
    {
        if (teachingPointId == Guid.Empty)
        {
            throw new ArgumentException("Teaching point id must not be empty.", nameof(teachingPointId));
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Teaching history action is not supported.");
        }

        ValidateActionPayload(action, beforeJson, afterJson);

        return new TeachingHistoryEntry(
            Guid.NewGuid(),
            teachingPointId,
            NormalizeOptionalText(recipeId),
            action,
            NormalizeOptionalText(beforeJson),
            NormalizeOptionalText(afterJson),
            (clock ?? (() => DateTimeOffset.UtcNow))());
    }

    private static void ValidateActionPayload(TeachingHistoryAction action, string? beforeJson, string? afterJson)
    {
        if (action == TeachingHistoryAction.Created && string.IsNullOrWhiteSpace(afterJson))
        {
            throw new ArgumentException("Created teaching history requires after JSON.", nameof(afterJson));
        }

        if (action == TeachingHistoryAction.Updated &&
            (string.IsNullOrWhiteSpace(beforeJson) || string.IsNullOrWhiteSpace(afterJson)))
        {
            throw new ArgumentException("Updated teaching history requires before and after JSON.");
        }

        if (action == TeachingHistoryAction.Deleted && string.IsNullOrWhiteSpace(beforeJson))
        {
            throw new ArgumentException("Deleted teaching history requires before JSON.", nameof(beforeJson));
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
