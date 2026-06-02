namespace VisionCell.Equipment.Hardware;

public sealed record HardwareAdapterStatus
{
    public HardwareAdapterStatus(
        string adapterName,
        bool isConnected,
        bool isReady,
        string endpoint,
        string message,
        DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(adapterName))
        {
            throw new ArgumentException("Adapter name is required.", nameof(adapterName));
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Adapter endpoint is required.", nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Adapter status message is required.", nameof(message));
        }

        AdapterName = adapterName.Trim();
        IsConnected = isConnected;
        IsReady = isReady;
        Endpoint = endpoint.Trim();
        Message = message.Trim();
        UpdatedAt = updatedAt;
    }

    public string AdapterName { get; init; }
    public bool IsConnected { get; init; }
    public bool IsReady { get; init; }
    public string Endpoint { get; init; }
    public string Message { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
