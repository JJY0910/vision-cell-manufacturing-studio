using VisionCell.Application.Interlocks;
using VisionCell.Core.Commands;
using VisionCell.Core.Events;
using VisionCell.Core.Interlocks;
using VisionCell.Equipment.Controllers;

namespace VisionCell.Application.Equipment;

public sealed class EquipmentDashboardUseCase : IEquipmentDashboardUseCase
{
    private readonly IEquipmentController _controller;
    private readonly ICommandInterlockService _interlockService;

    public EquipmentDashboardUseCase(
        IEquipmentController controller,
        ICommandInterlockService interlockService)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _interlockService = interlockService ?? throw new ArgumentNullException(nameof(interlockService));
    }

    public CommandAvailability GetCommandAvailability(CommandKind command, InterlockContext context)
    {
        return _interlockService.Evaluate(command, context);
    }

    public async Task<EquipmentDashboardSnapshotResult> RefreshAsync(
        TimeSpan snapshotTimeout,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _controller.GetSnapshotAsync(snapshotTimeout, cancellationToken).ConfigureAwait(false);
            return new EquipmentDashboardSnapshotResult(
                snapshot,
                SystemEvent.Create(SystemEventSeverity.Trace, "Equipment", "Snapshot", "Equipment snapshot refreshed."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EquipmentDashboardSnapshotResult(
                null,
                SystemEvent.Create(SystemEventSeverity.Warning, "Equipment", "SnapshotCancelled", "Snapshot refresh was cancelled."));
        }
        catch (OperationCanceledException)
        {
            return new EquipmentDashboardSnapshotResult(
                null,
                SystemEvent.Create(SystemEventSeverity.Alarm, "Equipment", "SnapshotTimeout", "Snapshot refresh timed out."));
        }
    }

    public async Task<EquipmentDashboardCommandResult> ConnectAsync(
        TimeSpan commandTimeout,
        TimeSpan snapshotTimeout,
        CancellationToken cancellationToken)
    {
        var commandResult = await _controller.ConnectAsync(commandTimeout, cancellationToken).ConfigureAwait(false);
        return await CreateCommandResultAsync(commandResult, "Connect", snapshotTimeout, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EquipmentDashboardCommandResult> DisconnectAsync(
        TimeSpan commandTimeout,
        TimeSpan snapshotTimeout,
        CancellationToken cancellationToken)
    {
        var commandResult = await _controller.DisconnectAsync(commandTimeout, cancellationToken).ConfigureAwait(false);
        return await CreateCommandResultAsync(commandResult, "Disconnect", snapshotTimeout, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EquipmentDashboardCommandResult> ExecuteCommandAsync(
        CommandKind command,
        InterlockContext context,
        TimeSpan commandTimeout,
        TimeSpan snapshotTimeout,
        CancellationToken cancellationToken)
    {
        var commandResult = await _controller
            .ExecuteCommandAsync(command, context, commandTimeout, cancellationToken)
            .ConfigureAwait(false);

        return await CreateCommandResultAsync(
            commandResult,
            FormatCommand(command),
            snapshotTimeout,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<EquipmentDashboardCommandResult> CreateCommandResultAsync(
        MachineCommandResult commandResult,
        string eventType,
        TimeSpan snapshotTimeout,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await RefreshAsync(snapshotTimeout, cancellationToken).ConfigureAwait(false);
        return new EquipmentDashboardCommandResult(
            commandResult,
            commandResult.ToSystemEvent("Equipment", eventType),
            snapshotResult);
    }

    private static string FormatCommand(CommandKind command)
    {
        return command switch
        {
            CommandKind.ServoOn => "Servo On",
            CommandKind.ServoOff => "Servo Off",
            CommandKind.MoveAbsolute => "Move Absolute",
            CommandKind.SequenceMoveToCamera => "Sequence Move To Camera",
            CommandKind.ResetAlarm => "Reset Alarm",
            CommandKind.EnterManualMode => "Enter Manual",
            CommandKind.EnterAutoMode => "Enter Auto",
            CommandKind.RunInspection => "Run Inspection",
            _ => command.ToString()
        };
    }
}
