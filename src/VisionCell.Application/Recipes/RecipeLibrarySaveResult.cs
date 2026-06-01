namespace VisionCell.Application.Recipes;

public sealed record RecipeLibrarySaveResult(
    RecipeLibraryOperationStatus Status,
    string Message,
    RecipeIndexEntry? Entry,
    IReadOnlyList<RecipeValidationIssue> ValidationIssues)
{
    public bool IsSuccess => Status == RecipeLibraryOperationStatus.Success;

    public static RecipeLibrarySaveResult Success(RecipeIndexEntry entry)
    {
        return new RecipeLibrarySaveResult(
            RecipeLibraryOperationStatus.Success,
            "Recipe saved and indexed.",
            entry,
            Array.Empty<RecipeValidationIssue>());
    }

    public static RecipeLibrarySaveResult ValidationFailed(IReadOnlyList<RecipeValidationIssue> issues)
    {
        return new RecipeLibrarySaveResult(
            RecipeLibraryOperationStatus.ValidationFailed,
            "Recipe validation failed.",
            null,
            issues);
    }

    public static RecipeLibrarySaveResult InvalidFileName(string message)
    {
        return new RecipeLibrarySaveResult(
            RecipeLibraryOperationStatus.InvalidFileName,
            message,
            null,
            Array.Empty<RecipeValidationIssue>());
    }

    public static RecipeLibrarySaveResult StorageUnavailable(string message)
    {
        return new RecipeLibrarySaveResult(
            RecipeLibraryOperationStatus.StorageUnavailable,
            message,
            null,
            Array.Empty<RecipeValidationIssue>());
    }

    public static RecipeLibrarySaveResult IndexUnavailable(string message)
    {
        return new RecipeLibrarySaveResult(
            RecipeLibraryOperationStatus.IndexUnavailable,
            message,
            null,
            Array.Empty<RecipeValidationIssue>());
    }
}
