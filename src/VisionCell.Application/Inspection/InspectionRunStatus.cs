namespace VisionCell.Application.Inspection;

public enum InspectionRunStatus
{
    Accepted,
    ActiveRecipeNotSelected,
    ActiveRecipeInvalid,
    ActiveRecipeUnavailable,
    InterlockRejected,
    CommandRejected,
    CommandTimeout,
    CommandCancelled,
    CommandFailed,
    CameraGrabFailed,
    Failed
}
