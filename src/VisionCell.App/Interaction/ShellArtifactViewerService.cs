using System.Diagnostics;

namespace VisionCell.App.Interaction;

public sealed class ShellArtifactViewerService : IArtifactViewerService
{
    public Task OpenAsync(
        string artifactPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            throw new ArgumentException("Artifact path is required.", nameof(artifactPath));
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = artifactPath,
            UseShellExecute = true
        });
        if (process is null)
        {
            throw new InvalidOperationException("Artifact viewer process could not be started.");
        }

        return Task.CompletedTask;
    }
}
