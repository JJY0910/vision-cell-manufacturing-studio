using VisionCell.Application.Teaching;

namespace VisionCell.App.Modules.Teaching.ViewModels;

public sealed class TeachingHistoryItemViewModel
{
    public TeachingHistoryItemViewModel(TeachingHistoryEntry entry)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    public TeachingHistoryEntry Entry { get; }
    public string ActionText => Entry.Action.ToString();
    public string RecipeText => string.IsNullOrWhiteSpace(Entry.RecipeId) ? "-" : Entry.RecipeId;
    public string BeforeText => SummarizeJson(Entry.BeforeJson);
    public string AfterText => SummarizeJson(Entry.AfterJson);
    public string CreatedAtText => Entry.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    private static string SummarizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var normalized = value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return normalized.Length <= 80 ? normalized : $"{normalized[..77]}...";
    }
}
