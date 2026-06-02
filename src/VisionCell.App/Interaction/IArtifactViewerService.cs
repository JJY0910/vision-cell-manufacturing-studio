namespace VisionCell.App.Interaction;

public interface IArtifactViewerService
{
    Task OpenAsync(
        string artifactPath,
        CancellationToken cancellationToken);
}
