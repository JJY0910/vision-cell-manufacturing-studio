namespace VisionCell.Application.Teaching;

public sealed record TeachingPointDeleteRequest(Guid TeachingPointId, string? RecipeId = null);
