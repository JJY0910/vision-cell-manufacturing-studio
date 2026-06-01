namespace VisionCell.Application.Recipes;

public interface IActiveRecipeContext
{
    Task<ActiveRecipeContextResult> GetActiveAsync(CancellationToken cancellationToken);
}
