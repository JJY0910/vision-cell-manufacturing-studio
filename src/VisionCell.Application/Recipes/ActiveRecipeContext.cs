namespace VisionCell.Application.Recipes;

public sealed class ActiveRecipeContext : IActiveRecipeContext
{
    private readonly IRecipeIndexRepository _recipeIndexRepository;

    public ActiveRecipeContext(IRecipeIndexRepository recipeIndexRepository)
    {
        _recipeIndexRepository = recipeIndexRepository ?? throw new ArgumentNullException(nameof(recipeIndexRepository));
    }

    public async Task<ActiveRecipeContextResult> GetActiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _recipeIndexRepository.FindActiveAsync(cancellationToken).ConfigureAwait(false);
            if (entry is null)
            {
                return ActiveRecipeContextResult.NotSelected();
            }

            return entry.IsValid
                ? ActiveRecipeContextResult.Success(entry)
                : ActiveRecipeContextResult.InvalidRecipe(entry);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ActiveRecipeContextResult.RepositoryUnavailable(
                $"Unable to read active recipe context: {ex.Message}");
        }
    }
}
