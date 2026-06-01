namespace VisionCell.Application.Recipes;

public sealed record RecipeValidationResult(IReadOnlyList<RecipeValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;

    public static RecipeValidationResult Success { get; } = new(Array.Empty<RecipeValidationIssue>());
}
