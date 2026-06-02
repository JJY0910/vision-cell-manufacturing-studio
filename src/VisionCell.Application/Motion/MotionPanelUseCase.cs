using VisionCell.Application.Interlocks;
using VisionCell.Core.Commands;
using VisionCell.Core.Interlocks;
using VisionCell.Equipment.Controllers;

namespace VisionCell.Application.Motion;

public sealed class MotionPanelUseCase : IMotionPanelUseCase
{
    private readonly IEquipmentController _controller;
    private readonly ICommandInterlockService _interlockService;

    public MotionPanelUseCase(
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

    public async Task<MotionSnapshotRefreshResult> RefreshSnapshotAsync(
        TimeSpan snapshotTimeout,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _controller.GetSnapshotAsync(snapshotTimeout, cancellationToken).ConfigureAwait(false);
            return new MotionSnapshotRefreshResult(
                MotionSnapshotRefreshStatus.Refreshed,
                snapshot,
                "Motion snapshot refreshed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new MotionSnapshotRefreshResult(
                MotionSnapshotRefreshStatus.Cancelled,
                null,
                "Snapshot refresh cancelled");
        }
        catch (OperationCanceledException)
        {
            return new MotionSnapshotRefreshResult(
                MotionSnapshotRefreshStatus.Timeout,
                null,
                "Snapshot refresh timed out");
        }
        catch (Exception ex)
        {
            return new MotionSnapshotRefreshResult(
                MotionSnapshotRefreshStatus.Failed,
                null,
                ex.Message);
        }
    }
}
