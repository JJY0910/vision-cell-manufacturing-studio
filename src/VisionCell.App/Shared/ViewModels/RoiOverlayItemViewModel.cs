namespace VisionCell.App.Shared.ViewModels;

public sealed record RoiOverlayItemViewModel(
    double X,
    double Y,
    double Width,
    double Height,
    string Label,
    string ScoreText,
    string State)
{
    public bool HasLabel => !string.IsNullOrWhiteSpace(DisplayText);

    public string DisplayText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Label))
            {
                return string.IsNullOrWhiteSpace(ScoreText) ? string.Empty : ScoreText;
            }

            return string.IsNullOrWhiteSpace(ScoreText)
                ? Label
                : $"{Label} {ScoreText}";
        }
    }
}
