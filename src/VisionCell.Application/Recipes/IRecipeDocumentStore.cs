namespace VisionCell.Application.Recipes;

public interface IRecipeDocumentStore
{
    Task<RecipeDocumentSaveResult> SaveAsync(RecipeDefinition recipe, CancellationToken cancellationToken);

    Task<RecipeDocumentLoadResult> LoadAsync(
        string recipeId,
        string version,
        CancellationToken cancellationToken);
}
