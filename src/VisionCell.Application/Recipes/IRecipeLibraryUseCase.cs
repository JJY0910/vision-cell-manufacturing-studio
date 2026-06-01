namespace VisionCell.Application.Recipes;

public interface IRecipeLibraryUseCase
{
    Task<RecipeLibrarySaveResult> SaveAsync(
        RecipeLibrarySaveRequest request,
        CancellationToken cancellationToken);
}
