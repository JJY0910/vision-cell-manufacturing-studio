using VisionCell.Core.Commands;
using VisionCell.Core.Events;
using VisionCell.Equipment.Faults;

namespace VisionCell.Application.Equipment;

public interface IEquipmentFaultInjectionUseCase
{
    Task<EquipmentFaultInjectionResult> ApplyAsync(
        EquipmentFaultInjectionCommand command,
        CancellationToken cancellationToken);
}

public sealed record EquipmentFaultInjectionCommand
{
    public EquipmentFaultInjectionCommand(
        EquipmentFaultKind kind,
        bool isActive,
        TimeSpan commandTimeout,
        TimeSpan snapshotTimeout,
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

        if (commandTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(commandTimeout), commandTimeout, "Fault injection command timeout must be greater than zero.");
        }

        if (snapshotTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshotTimeout), snapshotTimeout, "Fault injection snapshot timeout must be greater than zero.");
        }

        Kind = kind;
        IsActive = isActive;
        CommandTimeout = commandTimeout;
        SnapshotTimeout = snapshotTimeout;
        OperatorMemo = string.IsNullOrWhiteSpace(operatorMemo) ? null : operatorMemo.Trim();
    }

    public EquipmentFaultKind Kind { get; init; }
    public bool IsActive { get; init; }
    public TimeSpan CommandTimeout { get; init; }
    public TimeSpan SnapshotTimeout { get; init; }
    public string? OperatorMemo { get; init; }
}

public sealed record EquipmentFaultInjectionResult(
    MachineCommandRequest Request,
    MachineCommandResult CommandResult,
    SystemEvent CommandEvent,
    EquipmentDashboardSnapshotResult SnapshotResult);
