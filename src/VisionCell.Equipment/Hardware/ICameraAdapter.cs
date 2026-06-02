using VisionCell.Equipment.Cameras;

namespace VisionCell.Equipment.Hardware;

public interface ICameraAdapter : IHardwareAdapter
{
    Task<CameraSnapshot> GetCameraSnapshotAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken);

    Task<CameraGrabResult> GrabAsync(
        CameraGrabRequest request,
        CancellationToken cancellationToken);
}
