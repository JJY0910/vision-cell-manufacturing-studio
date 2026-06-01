using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisionCell.Application.Recipes;

public sealed class RecipeLibraryUseCase : IRecipeLibraryUseCase
{
    private static readonly JsonSerializerOptions ChecksumJsonOptions = CreateChecksumJsonOptions();

    private readonly IRecipeDocumentStore _documentStore;
    private readonly IRecipeIndexRepository _indexRepository;
    private readonly RecipeValidator _validator;
    private readonly Func<Guid> _idFactory;

    public RecipeLibraryUseCase(
        IRecipeDocumentStore documentStore,
        IRecipeIndexRepository indexRepository,
        RecipeValidator? validator = null,
        Func<Guid>? idFactory = null)
    {
        _documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
        _indexRepository = indexRepository ?? throw new ArgumentNullException(nameof(indexRepository));
        _validator = validator ?? new RecipeValidator();
        _idFactory = idFactory ?? Guid.NewGuid;
    }

    public async Task<RecipeLibrarySaveResult> SaveAsync(
        RecipeLibrarySaveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = _validator.Validate(request.Recipe);
        if (!validation.IsValid)
        {
            return RecipeLibrarySaveResult.ValidationFailed(validation.Issues);
        }

        var recipe = request.Recipe!;
        var documentResult = await _documentStore.SaveAsync(recipe, cancellationToken).ConfigureAwait(false);
        if (!documentResult.IsSuccess || string.IsNullOrWhiteSpace(documentResult.DocumentPath))
        {
            return MapDocumentFailure(documentResult);
        }

        var entry = new RecipeIndexEntry(
            _idFactory(),
            recipe.RecipeId.Trim(),
            recipe.Version.Trim(),
            recipe.ProductName.Trim(),
            documentResult.DocumentPath,
            ComputeChecksum(recipe),
            IsActive: false,
            IsValid: true,
            ValidationSummary: "Valid",
            recipe.CreatedAt,
            recipe.UpdatedAt);

        try
        {
            await _indexRepository.SaveAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return RecipeLibrarySaveResult.IndexUnavailable($"Unable to index recipe metadata: {ex.Message}");
        }

        return RecipeLibrarySaveResult.Success(entry);
    }

    private static RecipeLibrarySaveResult MapDocumentFailure(RecipeDocumentSaveResult result)
    {
        return result.Status switch
        {
            RecipeDocumentOperationStatus.ValidationFailed => RecipeLibrarySaveResult.ValidationFailed(result.ValidationIssues),
            RecipeDocumentOperationStatus.InvalidFileName => RecipeLibrarySaveResult.InvalidFileName(result.Message),
            _ => RecipeLibrarySaveResult.StorageUnavailable(result.Message)
        };
    }

    private static string ComputeChecksum(RecipeDefinition recipe)
    {
        var json = JsonSerializer.Serialize(recipe, ChecksumJsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static JsonSerializerOptions CreateChecksumJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
