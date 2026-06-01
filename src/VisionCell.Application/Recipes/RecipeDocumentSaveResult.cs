namespace VisionCell.Application.Recipes;

public sealed record RecipeDocumentSaveResult(
    RecipeDocumentOperationStatus Status,
    string Message,
    string? DocumentPath,
    IReadOnlyList<RecipeValidationIssue> ValidationIssues)
{
    public bool IsSuccess => Status == RecipeDocumentOperationStatus.Success;

    public static RecipeDocumentSaveResult Success(string documentPath)
    {
        return new RecipeDocumentSaveResult(
            RecipeDocumentOperationStatus.Success,
            "Recipe document saved.",
            documentPath,
            Array.Empty<RecipeValidationIssue>());
    }

    public static RecipeDocumentSaveResult ValidationFailed(IReadOnlyList<RecipeValidationIssue> issues)
    {
        return new RecipeDocumentSaveResult(
            RecipeDocumentOperationStatus.ValidationFailed,
            "Recipe validation failed.",
            null,
            issues);
    }

    public static RecipeDocumentSaveResult InvalidFileName(string message)
    {
        return new RecipeDocumentSaveResult(
            RecipeDocumentOperationStatus.InvalidFileName,
            message,
            null,
            Array.Empty<RecipeValidationIssue>());
    }

    public static RecipeDocumentSaveResult StorageUnavailable(string message)
    {
        return new RecipeDocumentSaveResult(
            RecipeDocumentOperationStatus.StorageUnavailable,
            message,
            null,
            Array.Empty<RecipeValidationIssue>());
    }
}
