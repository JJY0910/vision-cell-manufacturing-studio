namespace VisionCell.Application.Inspection;

public enum InspectionRunStatus
{
    Accepted,
    ActiveRecipeNotSelected,
    ActiveRecipeInvalid,
    ActiveRecipeUnavailable,
    RecipeDocumentUnavailable,
    InterlockRejected,
    CommandRejected,
    CommandTimeout,
    CommandCancelled,
    CommandFailed,
    MoveToCameraFailed,
    CameraGrabFailed,
    VisionInspectionFailed,
    Failed
}
