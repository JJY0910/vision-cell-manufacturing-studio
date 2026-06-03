using VisionCell.Application.Recipes;

namespace VisionCell.Application.Inspection;

public interface IInspectionReinspectRecipePolicyUseCase
{
    Task<InspectionReinspectRecipePolicyResult> ResolveAsync(
        InspectionReinspectPreparation preparation,
        CancellationToken cancellationToken);
}

public sealed class InspectionReinspectRecipePolicyUseCase : IInspectionReinspectRecipePolicyUseCase
{
    private readonly IActiveRecipeContext _activeRecipeContext;

    public InspectionReinspectRecipePolicyUseCase(IActiveRecipeContext activeRecipeContext)
    {
        _activeRecipeContext = activeRecipeContext ?? throw new ArgumentNullException(nameof(activeRecipeContext));
    }

    public async Task<InspectionReinspectRecipePolicyResult> ResolveAsync(
        InspectionReinspectPreparation preparation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preparation);

        var active = await _activeRecipeContext.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        return active.Status switch
        {
            ActiveRecipeContextStatus.Success => ResolveActive(preparation, active),
            ActiveRecipeContextStatus.InvalidRecipe => InspectionReinspectRecipePolicyResult.ActiveRecipeInvalid(
                preparation,
                active.RecipeId ?? "-",
                active.Version ?? "-",
                active.Message),
            ActiveRecipeContextStatus.RepositoryUnavailable => InspectionReinspectRecipePolicyResult.ActiveRecipeUnavailable(
                preparation,
                active.Message),
            _ => InspectionReinspectRecipePolicyResult.HistoricalOnly(
                preparation,
                active.Message)
        };
    }

    private static InspectionReinspectRecipePolicyResult ResolveActive(
        InspectionReinspectPreparation preparation,
        ActiveRecipeContextResult active)
    {
        var activeRecipeId = active.RecipeId ?? "-";
        var activeVersion = active.Version ?? "-";
        var matches = string.Equals(preparation.RecipeId, activeRecipeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(preparation.RecipeVersion, activeVersion, StringComparison.OrdinalIgnoreCase);

        return matches
            ? InspectionReinspectRecipePolicyResult.ActiveMatchesHistorical(preparation, activeRecipeId, activeVersion)
            : InspectionReinspectRecipePolicyResult.ActiveDiffersFromHistorical(preparation, activeRecipeId, activeVersion);
    }
}

public sealed record InspectionReinspectRecipePolicyResult(
    string HistoricalRecipeId,
    string HistoricalRecipeVersion,
    string ActiveRecipeId,
    string ActiveRecipeVersion,
    InspectionReinspectRecipePolicyStatus Status,
    string PolicyLabel,
    string Message)
{
    public string HistoricalRecipeText => $"{HistoricalRecipeId} v{HistoricalRecipeVersion}";
    public string ActiveRecipeText => ActiveRecipeId == "-" ? "-" : $"{ActiveRecipeId} v{ActiveRecipeVersion}";

    public static InspectionReinspectRecipePolicyResult ActiveMatchesHistorical(
        InspectionReinspectPreparation preparation,
        string activeRecipeId,
        string activeRecipeVersion)
    {
        return new InspectionReinspectRecipePolicyResult(
            preparation.RecipeId,
            preparation.RecipeVersion,
            activeRecipeId,
            activeRecipeVersion,
            InspectionReinspectRecipePolicyStatus.ActiveMatchesHistorical,
            "Current and historical Recipe match",
            "The active Recipe metadata matches the selected historical result. Metadata comparison still does not execute live replay.");
    }

    public static InspectionReinspectRecipePolicyResult ActiveDiffersFromHistorical(
        InspectionReinspectPreparation preparation,
        string activeRecipeId,
        string activeRecipeVersion)
    {
        return new InspectionReinspectRecipePolicyResult(
            preparation.RecipeId,
            preparation.RecipeVersion,
            activeRecipeId,
            activeRecipeVersion,
            InspectionReinspectRecipePolicyStatus.ActiveDiffersFromHistorical,
            "Current active Recipe differs",
            "The active Recipe metadata differs from the selected historical result. Source-image replay policy remains unimplemented.");
    }

    public static InspectionReinspectRecipePolicyResult HistoricalOnly(
        InspectionReinspectPreparation preparation,
        string message)
    {
        return new InspectionReinspectRecipePolicyResult(
            preparation.RecipeId,
            preparation.RecipeVersion,
            "-",
            "-",
            InspectionReinspectRecipePolicyStatus.HistoricalOnly,
            "Historical Recipe metadata only",
            $"{message} Metadata comparison uses the selected historical result context only.");
    }

    public static InspectionReinspectRecipePolicyResult ActiveRecipeInvalid(
        InspectionReinspectPreparation preparation,
        string activeRecipeId,
        string activeRecipeVersion,
        string message)
    {
        return new InspectionReinspectRecipePolicyResult(
            preparation.RecipeId,
            preparation.RecipeVersion,
            activeRecipeId,
            activeRecipeVersion,
            InspectionReinspectRecipePolicyStatus.ActiveRecipeInvalid,
            "Active Recipe invalid",
            $"{message} Metadata comparison remains limited to the selected historical result context.");
    }

    public static InspectionReinspectRecipePolicyResult ActiveRecipeUnavailable(
        InspectionReinspectPreparation preparation,
        string message)
    {
        return new InspectionReinspectRecipePolicyResult(
            preparation.RecipeId,
            preparation.RecipeVersion,
            "-",
            "-",
            InspectionReinspectRecipePolicyStatus.ActiveRecipeUnavailable,
            "Active Recipe unavailable",
            $"{message} Metadata comparison remains limited to the selected historical result context.");
    }
}

public enum InspectionReinspectRecipePolicyStatus
{
    ActiveMatchesHistorical,
    ActiveDiffersFromHistorical,
    HistoricalOnly,
    ActiveRecipeInvalid,
    ActiveRecipeUnavailable
}
