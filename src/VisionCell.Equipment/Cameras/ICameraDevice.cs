namespace VisionCell.Equipment.Cameras;

public interface ICameraDevice
{
    Task<CameraGrabResult> GrabAsync(CameraGrabRequest request, CancellationToken cancellationToken);
}
