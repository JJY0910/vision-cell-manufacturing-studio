using VisionCell.Core.Commands;

namespace VisionCell.Equipment.Faults;

public sealed record EquipmentFaultInjectionRequest
{
    public EquipmentFaultInjectionRequest(
        EquipmentFaultKind kind,
        bool isActive,
        MachineCommandRequest commandRequest,
        string? operatorMemo = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported fault injection kind.");
        }

        if (kind == EquipmentFaultKind.ClearAll && isActive)
        {
            throw new ArgumentException("ClearAll cannot be injected as an active fault.", nameof(isActive));
        }

        ArgumentNullException.ThrowIfNull(commandRequest);
        if (commandRequest.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(commandRequest), commandRequest.Timeout, "Fault injection timeout must be greater than zero.");
        }

        Kind = kind;
        IsActive = isActive;
        CommandRequest = commandRequest;
        OperatorMemo = string.IsNullOrWhiteSpace(operatorMemo) ? null : operatorMemo.Trim();
    }

    public EquipmentFaultKind Kind { get; init; }
    public bool IsActive { get; init; }
    public MachineCommandRequest CommandRequest { get; init; }
    public string? OperatorMemo { get; init; }
}
