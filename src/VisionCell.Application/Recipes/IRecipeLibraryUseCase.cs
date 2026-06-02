namespace VisionCell.Application.Recipes;

public interface IRecipeLibraryUseCase
{
    Task<IReadOnlyList<RecipeIndexEntry>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken);

    Task<RecipeLibrarySaveResult> SaveAsync(
        RecipeLibrarySaveRequest request,
        CancellationToken cancellationToken);

    Task<bool> ActivateAsync(
        string recipeId,
        string version,
        CancellationToken cancellationToken);
}
