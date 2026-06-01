namespace VisionCell.Application.Recipes;

public interface IRecipeIndexRepository
{
    Task SaveAsync(RecipeIndexEntry entry, CancellationToken cancellationToken);

    Task<RecipeIndexEntry?> FindAsync(
        string recipeId,
        string version,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RecipeIndexEntry>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken);
}
