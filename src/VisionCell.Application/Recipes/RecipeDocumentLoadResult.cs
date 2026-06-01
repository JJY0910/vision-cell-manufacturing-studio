namespace VisionCell.Application.Recipes;

public sealed record RecipeDocumentLoadResult(
    RecipeDocumentOperationStatus Status,
    string Message,
    RecipeDefinition? Recipe,
    string? DocumentPath,
    IReadOnlyList<RecipeValidationIssue> ValidationIssues)
{
    public bool IsSuccess => Status == RecipeDocumentOperationStatus.Success;

    public static RecipeDocumentLoadResult Success(RecipeDefinition recipe, string documentPath)
    {
        return new RecipeDocumentLoadResult(
            RecipeDocumentOperationStatus.Success,
            "Recipe document loaded.",
            recipe,
            documentPath,
            Array.Empty<RecipeValidationIssue>());
    }

    public static RecipeDocumentLoadResult NotFound(string documentPath)
    {
        return new RecipeDocumentLoadResult(
            RecipeDocumentOperationStatus.NotFound,
            "Recipe document was not found.",
            null,
            documentPath,
            Array.Empty<RecipeValidationIssue>());
    }

    public static RecipeDocumentLoadResult ValidationFailed(
        string documentPath,
        IReadOnlyList<RecipeValidationIssue> issues)
    {
        return new RecipeDocumentLoadResult(
            RecipeDocumentOperationStatus.ValidationFailed,
            "Recipe validation failed.",
            null,
            documentPath,
            issues);
    }

    public static RecipeDocumentLoadResult InvalidFileName(string message)
    {
        return new RecipeDocumentLoadResult(
            RecipeDocumentOperationStatus.InvalidFileName,
            message,
            null,
            null,
            Array.Empty<RecipeValidationIssue>());
    }

    public static RecipeDocumentLoadResult InvalidDocument(string documentPath, string message)
    {
        return new RecipeDocumentLoadResult(
            RecipeDocumentOperationStatus.InvalidDocument,
            message,
            null,
            documentPath,
            Array.Empty<RecipeValidationIssue>());
    }

    public static RecipeDocumentLoadResult StorageUnavailable(string message)
    {
        return new RecipeDocumentLoadResult(
            RecipeDocumentOperationStatus.StorageUnavailable,
            message,
            null,
            null,
            Array.Empty<RecipeValidationIssue>());
    }
}
