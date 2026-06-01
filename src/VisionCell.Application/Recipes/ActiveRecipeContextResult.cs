namespace VisionCell.Application.Recipes;

public sealed record ActiveRecipeContextResult(
    ActiveRecipeContextStatus Status,
    RecipeIndexEntry? Entry,
    string Message)
{
    public bool IsSuccess => Status == ActiveRecipeContextStatus.Success;

    public string? RecipeId => Entry?.RecipeId;

    public string? Version => Entry?.Version;

    public static ActiveRecipeContextResult Success(RecipeIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new ActiveRecipeContextResult(
            ActiveRecipeContextStatus.Success,
            entry,
            $"Active recipe '{entry.RecipeId}' v{entry.Version} is ready.");
    }

    public static ActiveRecipeContextResult NotSelected()
    {
        return new ActiveRecipeContextResult(
            ActiveRecipeContextStatus.NotSelected,
            Entry: null,
            "No active recipe is selected.");
    }

    public static ActiveRecipeContextResult InvalidRecipe(RecipeIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var details = string.IsNullOrWhiteSpace(entry.ValidationSummary)
            ? "validation failed"
            : entry.ValidationSummary;

        return new ActiveRecipeContextResult(
            ActiveRecipeContextStatus.InvalidRecipe,
            entry,
            $"Active recipe '{entry.RecipeId}' v{entry.Version} is invalid: {details}");
    }

    public static ActiveRecipeContextResult RepositoryUnavailable(string message)
    {
        return new ActiveRecipeContextResult(
            ActiveRecipeContextStatus.RepositoryUnavailable,
            Entry: null,
            message);
    }
}
