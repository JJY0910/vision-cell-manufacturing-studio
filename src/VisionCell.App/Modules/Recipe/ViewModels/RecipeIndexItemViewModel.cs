using VisionCell.Application.Recipes;

namespace VisionCell.App.Modules.Recipe.ViewModels;

public sealed class RecipeIndexItemViewModel
{
    public RecipeIndexItemViewModel(RecipeIndexEntry entry)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    public RecipeIndexEntry Entry { get; }
    public string RecipeId => Entry.RecipeId;
    public string Version => Entry.Version;
    public string ProductName => Entry.ProductName;
    public string DocumentPath => Entry.DocumentPath;
    public string Checksum => Entry.Checksum;
    public bool IsActive => Entry.IsActive;
    public bool IsValid => Entry.IsValid;
    public string ActiveText => IsActive ? "Active" : "Inactive";
    public string ValidationStateText => IsValid ? "Valid" : "Invalid";
    public string ValidationSummaryText => Summarize(Entry.ValidationSummary, 120);
    public string ChecksumShort => Checksum.Length <= 12 ? Checksum : Checksum[..12];
    public string CreatedAtText => Entry.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string UpdatedAtText => Entry.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    private static string Summarize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var normalized = value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return normalized.Length <= maxLength ? normalized : $"{normalized[..(maxLength - 3)]}...";
    }
}
