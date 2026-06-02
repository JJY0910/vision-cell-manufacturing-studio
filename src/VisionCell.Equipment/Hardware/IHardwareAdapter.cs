namespace VisionCell.Equipment.Hardware;

public interface IHardwareAdapter
{
    string AdapterName { get; }

    Task<HardwareAdapterStatus> GetStatusAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
