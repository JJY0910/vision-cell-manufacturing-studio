using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VisionCell.Application.Recipes;

namespace VisionCell.Persistence.Recipes;

public sealed class JsonRecipeDocumentStore : IRecipeDocumentStore
{
    private static readonly Regex SafeRecipeIdPattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SemanticVersionPattern = new(
        @"^\d+\.\d+\.\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _rootDirectory;
    private readonly RecipeValidator _validator;

    public JsonRecipeDocumentStore(string rootDirectory, RecipeValidator? validator = null)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Recipe root directory is required.", nameof(rootDirectory));
        }

        _rootDirectory = Path.GetFullPath(rootDirectory);
        _validator = validator ?? new RecipeValidator();
    }

    public async Task<RecipeDocumentSaveResult> SaveAsync(
        RecipeDefinition recipe,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var validation = _validator.Validate(recipe);
        if (!validation.IsValid)
        {
            return RecipeDocumentSaveResult.ValidationFailed(validation.Issues);
        }

        if (!TryBuildDocumentPath(recipe.RecipeId, recipe.Version, out var documentPath, out var error))
        {
            return RecipeDocumentSaveResult.InvalidFileName(error);
        }

        try
        {
            Directory.CreateDirectory(_rootDirectory);
            var json = JsonSerializer.Serialize(recipe, JsonOptions);
            await File.WriteAllTextAsync(documentPath, json, cancellationToken).ConfigureAwait(false);
            return RecipeDocumentSaveResult.Success(documentPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return RecipeDocumentSaveResult.StorageUnavailable($"Unable to save recipe document: {ex.Message}");
        }
    }

    public async Task<RecipeDocumentLoadResult> LoadAsync(
        string recipeId,
        string version,
        CancellationToken cancellationToken)
    {
        if (!TryBuildDocumentPath(recipeId, version, out var documentPath, out var error))
        {
            return RecipeDocumentLoadResult.InvalidFileName(error);
        }

        if (!File.Exists(documentPath))
        {
            return RecipeDocumentLoadResult.NotFound(documentPath);
        }

        try
        {
            var json = await File.ReadAllTextAsync(documentPath, cancellationToken).ConfigureAwait(false);
            var recipe = JsonSerializer.Deserialize<RecipeDefinition>(json, JsonOptions);
            if (recipe is null)
            {
                return RecipeDocumentLoadResult.InvalidDocument(documentPath, "Recipe document is empty or invalid.");
            }

            var validation = _validator.Validate(recipe);
            return validation.IsValid
                ? RecipeDocumentLoadResult.Success(recipe, documentPath)
                : RecipeDocumentLoadResult.ValidationFailed(documentPath, validation.Issues);
        }
        catch (JsonException ex)
        {
            return RecipeDocumentLoadResult.InvalidDocument(documentPath, $"Recipe JSON is invalid: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return RecipeDocumentLoadResult.StorageUnavailable($"Unable to load recipe document: {ex.Message}");
        }
    }

    private bool TryBuildDocumentPath(
        string recipeId,
        string version,
        out string documentPath,
        out string error)
    {
        documentPath = string.Empty;
        error = string.Empty;

        var normalizedRecipeId = recipeId?.Trim();
        var normalizedVersion = version?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRecipeId) ||
            !SafeRecipeIdPattern.IsMatch(normalizedRecipeId) ||
            normalizedRecipeId.Contains("..", StringComparison.Ordinal))
        {
            error = "Recipe id contains unsupported file-name characters.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedVersion) || !SemanticVersionPattern.IsMatch(normalizedVersion))
        {
            error = "Recipe version must use major.minor.patch format.";
            return false;
        }

        var fileName = $"{normalizedRecipeId}.v{normalizedVersion}.recipe.json";
        var candidate = Path.GetFullPath(Path.Combine(_rootDirectory, fileName));
        var rootWithSeparator = _rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _rootDirectory
            : _rootDirectory + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            error = "Recipe document path must stay inside the recipe root directory.";
            return false;
        }

        documentPath = candidate;
        return true;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
