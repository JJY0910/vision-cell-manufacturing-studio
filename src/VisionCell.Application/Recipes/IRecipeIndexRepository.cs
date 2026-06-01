namespace VisionCell.Application.Recipes;

public interface IRecipeIndexRepository
{
    Task SaveAsync(RecipeIndexEntry entry, CancellationToken cancellationToken);

    Task<RecipeIndexEntry?> FindAsync(
        string recipeId,
        string version,
        CancellationToken cancellationToken);

    Task<RecipeIndexEntry?> FindActiveAsync(CancellationToken cancellationToken);

    Task<bool> SetActiveAsync(
        string recipeId,
        string version,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RecipeIndexEntry>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken);
}
