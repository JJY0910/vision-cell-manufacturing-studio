namespace VisionCell.App.Shared.ViewModels;

public static class OperatorAlertClassifier
{
    private static readonly string[] AlertTerms =
    {
        "blocked",
        "cancelled",
        "failed",
        "rejected",
        "timed out",
        "timeout",
        "unavailable",
        "unsafe"
    };

    public static string? GetAlertMessage(string? message)
    {
        return IsAlert(message) ? message : null;
    }

    public static bool IsAlert(string? message)
    {
        return !string.IsNullOrWhiteSpace(message) &&
               AlertTerms.Any(term => message.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
