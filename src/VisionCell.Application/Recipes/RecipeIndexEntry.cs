namespace VisionCell.Application.Recipes;

public sealed record RecipeIndexEntry(
    Guid Id,
    string RecipeId,
    string Version,
    string ProductName,
    string DocumentPath,
    string Checksum,
    bool IsActive,
    bool IsValid,
    string? ValidationSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
