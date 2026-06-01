using CommunityToolkit.Mvvm.ComponentModel;
using VisionCell.Application.Inspection;

namespace VisionCell.App.Modules.Inspection.ViewModels;

public sealed partial class InspectionStepViewModel : ObservableObject
{
    public InspectionStepViewModel(InspectionSequenceStepRecord step)
    {
        Name = step.Name;
        Status = step.Status.ToString();
        Detail = step.Message;
        ElapsedText = FormatElapsed(step.Elapsed);
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _detail = string.Empty;

    [ObservableProperty]
    private string _elapsedText = string.Empty;

    public void Apply(InspectionSequenceStepRecord step)
    {
        Status = step.Status.ToString();
        Detail = step.Message;
        ElapsedText = FormatElapsed(step.Elapsed);
    }

    private static string FormatElapsed(TimeSpan? elapsed)
    {
        return elapsed is null ? "-" : $"{elapsed.Value.TotalMilliseconds:0} ms";
    }
}
