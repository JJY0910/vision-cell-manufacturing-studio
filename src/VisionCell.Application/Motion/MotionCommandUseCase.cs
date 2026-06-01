using VisionCell.Core.Commands;
using VisionCell.Core.Errors;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Controllers;

namespace VisionCell.Application.Motion;

public sealed class MotionCommandUseCase : IMotionCommandUseCase
{
    private static readonly HashSet<CommandKind> SupportedCommands = new()
    {
        CommandKind.ServoOn,
        CommandKind.ServoOff,
        CommandKind.Home,
        CommandKind.Jog,
        CommandKind.MoveAbsolute,
        CommandKind.SequenceMoveToCamera,
        CommandKind.Stop
    };

    private readonly IEquipmentController _controller;
    private readonly IMotionCommandHistoryRepository _historyRepository;
    private readonly Func<DateTimeOffset> _clock;

    public MotionCommandUseCase(
        IEquipmentController controller,
        IMotionCommandHistoryRepository historyRepository,
        Func<DateTimeOffset>? clock = null)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<MotionCommandExecutionResult> ExecuteAsync(
        MotionCommandExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Timeout, "Motion command timeout must be greater than zero.");
        }

        var requestedAt = _clock();
        var commandRequest = new MachineCommandRequest(
            FormatCommand(request.Command),
            CorrelationId.New(),
            request.Timeout,
            requestedAt,
            request.GetParameters());

        var result = await ExecuteControllerCommandAsync(request, commandRequest, cancellationToken).ConfigureAwait(false);
        var correlatedResult = result with { CorrelationId = commandRequest.CorrelationId };

        var historyEntry = new MotionCommandHistoryEntry(
            Guid.NewGuid(),
            commandRequest,
            correlatedResult,
            _clock());

        await _historyRepository.SaveAsync(historyEntry, cancellationToken).ConfigureAwait(false);

        return new MotionCommandExecutionResult(commandRequest, correlatedResult);
    }

    private async Task<MachineCommandResult> ExecuteControllerCommandAsync(
        MotionCommandExecutionRequest request,
        MachineCommandRequest commandRequest,
        CancellationToken cancellationToken)
    {
        if (!SupportedCommands.Contains(request.Command))
        {
            return MachineCommandResult.Rejected(
                ErrorCode.CommandRejected,
                $"{commandRequest.CommandName} is not a motion command.",
                TimeSpan.Zero,
                commandRequest.CorrelationId);
        }

        var result = await _controller.ExecuteCommandAsync(
            request.Command,
            request.InterlockContext,
            commandRequest,
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    private static string FormatCommand(CommandKind command)
    {
        return command switch
        {
            CommandKind.ServoOn => "Servo On",
            CommandKind.ServoOff => "Servo Off",
            CommandKind.MoveAbsolute => "Move Absolute",
            CommandKind.SequenceMoveToCamera => "Sequence Move To Camera",
            CommandKind.EnterManualMode => "Enter Manual",
            CommandKind.EnterAutoMode => "Enter Auto",
            CommandKind.RunInspection => "Run Inspection",
            _ => command.ToString()
        };
    }
}
