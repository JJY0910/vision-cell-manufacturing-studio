using System.Diagnostics;
using VisionCell.Core.Commands;
using VisionCell.Core.Errors;
using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Alarms;
using VisionCell.Equipment.Cameras;
using VisionCell.Equipment.Controllers;
using VisionCell.Equipment.Faults;
using VisionCell.Equipment.Io;
using VisionCell.Equipment.Safety;
using VisionCell.Motion.Axes;
using VisionCell.Motion.Commands;

namespace VisionCell.Simulator;

public sealed class VirtualEquipmentController : IEquipmentController, IEquipmentFaultInjector
{
    private static readonly TimeSpan SnapshotLatency = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ServoLatency = TimeSpan.FromMilliseconds(75);
    private static readonly TimeSpan HomeLatency = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan JogLatency = TimeSpan.FromMilliseconds(125);
    private static readonly TimeSpan MoveLatency = TimeSpan.FromMilliseconds(175);
    private static readonly TimeSpan StopLatency = TimeSpan.FromMilliseconds(25);

    private readonly object _gate = new();
    private bool _connected;
    private bool _servoEnabled;
    private bool _axisBusy;
    private bool _doorClosed = true;
    private bool _emergencyStopActive;
    private bool _airPressureOk = true;
    private bool _vacuumOk = true;
    private bool _cameraReady = true;
    private bool _servoAlarmActive;
    private MachineMode _mode = MachineMode.Offline;
    private AlarmSnapshot? _alarm;
    private IReadOnlyList<AxisSnapshot> _axes = AxisDefaults.CreatePowerOffAxes();
    private CancellationTokenSource? _activeMotionCancellation;

    public async Task<EquipmentSnapshot> GetSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        await Task.Delay(SnapshotLatency, cts.Token).ConfigureAwait(false);

        return CreateSnapshot(DateTimeOffset.UtcNow);
    }

    public Task<MachineCommandResult> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return RunControllerCommandAsync(
            "Connect",
            TimeSpan.FromMilliseconds(150),
            timeout,
            cancellationToken,
            CorrelationId.New(),
            ApplyConnect);
    }

    public Task<MachineCommandResult> DisconnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return RunControllerCommandAsync(
            "Disconnect",
            TimeSpan.FromMilliseconds(50),
            timeout,
            cancellationToken,
            CorrelationId.New(),
            ApplyDisconnect);
    }

    public CommandAvailability GetCommandAvailability(CommandKind command, InterlockContext context)
    {
        return CommandInterlockRules.Evaluate(command, context);
    }

    public Task<MachineCommandResult> ExecuteCommandAsync(CommandKind command, InterlockContext context, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var request = new MachineCommandRequest(
            FormatCommand(command),
            CorrelationId.New(),
            timeout,
            DateTimeOffset.UtcNow,
            EmptyParameters.Value);

        return ExecuteCommandAsync(command, context, request, cancellationToken);
    }

    public Task<MachineCommandResult> ExecuteCommandAsync(
        CommandKind command,
        InterlockContext context,
        MachineCommandRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var timeout = request.Timeout;

        var availability = GetCommandAvailability(command, CreateEffectiveContext(context));
        if (!availability.IsEnabled)
        {
            return Task.FromResult(CreateRejectedResult(availability, request.CorrelationId));
        }

        return command switch
        {
            CommandKind.Connect => RunControllerCommandAsync(
                FormatCommand(command),
                TimeSpan.FromMilliseconds(150),
                timeout,
                cancellationToken,
                request.CorrelationId,
                ApplyConnect),
            CommandKind.Disconnect => RunControllerCommandAsync(
                FormatCommand(command),
                TimeSpan.FromMilliseconds(50),
                timeout,
                cancellationToken,
                request.CorrelationId,
                ApplyDisconnect),
            CommandKind.ServoOn => RunControllerCommandAsync(
                FormatCommand(command),
                ServoLatency,
                timeout,
                cancellationToken,
                request.CorrelationId,
                () =>
                {
                    lock (_gate)
                    {
                        _servoEnabled = true;
                        _axes = _axes.Select(axis => axis with { ServoOn = true }).ToArray();
                    }

                    return "Servo On completed.";
                }),
            CommandKind.ServoOff => RunControllerCommandAsync(
                FormatCommand(command),
                ServoLatency,
                timeout,
                cancellationToken,
                request.CorrelationId,
                () =>
                {
                    lock (_gate)
                    {
                        _servoEnabled = false;
                        _axes = _axes.Select(axis => axis with { ServoOn = false, IsMoving = false }).ToArray();
                    }

                    return "Servo Off completed.";
                }),
            CommandKind.Home => RunMotionCommandAsync(
                FormatCommand(command),
                HomeLatency,
                timeout,
                cancellationToken,
                request.CorrelationId,
                () =>
                {
                    lock (_gate)
                    {
                        _axes = _axes.Select(axis => axis with
                        {
                            Position = 0.0,
                            Target = 0.0,
                            IsHomed = true,
                            ServoOn = _servoEnabled,
                            IsMoving = false,
                            Alarm = null
                        }).ToArray();
                    }

                    return "Home completed for all simulator axes.";
                }),
            CommandKind.Jog => RunMotionCommandAsync(
                FormatCommand(command),
                JogLatency,
                timeout,
                cancellationToken,
                request.CorrelationId,
                () => ApplyJogStep(request.Parameters)),
            CommandKind.MoveAbsolute => RunMotionCommandAsync(
                FormatCommand(command),
                MoveLatency,
                timeout,
                cancellationToken,
                request.CorrelationId,
                () => ApplyMoveAbsolute(request.Parameters)),
            CommandKind.SequenceMoveToCamera => RunMotionCommandAsync(
                FormatCommand(command),
                MoveLatency,
                timeout,
                cancellationToken,
                request.CorrelationId,
                () => ApplyMoveAbsolute(request.Parameters)),
            CommandKind.Stop => RunStopCommandAsync(timeout, cancellationToken, request.CorrelationId),
            CommandKind.ResetAlarm => RunControllerCommandAsync(
                FormatCommand(command),
                ServoLatency,
                timeout,
                cancellationToken,
                request.CorrelationId,
                () =>
                {
                    lock (_gate)
                    {
                        _alarm = null;
                        _axes = _axes.Select(axis => axis with { Alarm = null, IsMoving = false }).ToArray();
                        RefreshAlarmFromFaultsLocked();
                    }

                    return "Alarm reset completed.";
                }),
            CommandKind.EnterManualMode => RunControllerCommandAsync(
                FormatCommand(command),
                ServoLatency,
                timeout,
                cancellationToken,
                request.CorrelationId,
                () =>
                {
                    lock (_gate)
                    {
                        _mode = MachineMode.Manual;
                    }

                    return "Machine mode changed to Manual.";
                }),
            CommandKind.EnterAutoMode => RunControllerCommandAsync(
                FormatCommand(command),
                ServoLatency,
                timeout,
                cancellationToken,
                request.CorrelationId,
                () =>
                {
                    lock (_gate)
                    {
                        _mode = MachineMode.Auto;
                    }

                    return "Machine mode changed to Auto.";
                }),
            CommandKind.RunInspection => Task.FromResult(MachineCommandResult.Success(
                "Run Inspection interlock accepted. Inspection execution remains a later use case.",
                TimeSpan.Zero,
                request.CorrelationId)),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported command kind.")
        };
    }

    public Task<MachineCommandResult> ApplyFaultAsync(
        EquipmentFaultInjectionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sw = Stopwatch.StartNew();
        lock (_gate)
        {
            if (!_connected)
            {
                return Task.FromResult(MachineCommandResult.Rejected(
                    ErrorCode.ControllerConnectionFailed,
                    "Fault injection requires a connected virtual controller.",
                    sw.Elapsed,
                    request.CommandRequest.CorrelationId));
            }
        }

        return RunControllerCommandAsync(
            request.CommandRequest.CommandName,
            ServoLatency,
            request.CommandRequest.Timeout,
            cancellationToken,
            request.CommandRequest.CorrelationId,
            () => ApplyFaultState(request));
    }

    private async Task<MachineCommandResult> RunControllerCommandAsync(
        string commandName,
        TimeSpan latency,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        CorrelationId correlationId,
        Func<string> applyState)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await Task.Delay(latency, cts.Token).ConfigureAwait(false);
            var message = applyState();
            return MachineCommandResult.Success(message, sw.Elapsed, correlationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return MachineCommandResult.Cancelled(
                ErrorCode.CommandCancelled,
                $"{commandName} was cancelled.",
                sw.Elapsed,
                correlationId);
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                _alarm = new AlarmSnapshot(ErrorCode.CommandTimeout, $"{commandName} exceeded {timeout.TotalMilliseconds:0} ms.", DateTimeOffset.UtcNow);
            }

            return MachineCommandResult.Timeout(
                ErrorCode.CommandTimeout,
                $"{commandName} timed out after {timeout.TotalMilliseconds:0} ms.",
                sw.Elapsed,
                correlationId);
        }
    }

    private async Task<MachineCommandResult> RunMotionCommandAsync(
        string commandName,
        TimeSpan latency,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        CorrelationId correlationId,
        Func<string> applyState)
    {
        var sw = Stopwatch.StartNew();
        using var commandTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var motionCancellation = new CancellationTokenSource();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(commandTimeout.Token, motionCancellation.Token);
        commandTimeout.CancelAfter(timeout);

        lock (_gate)
        {
            if (_axisBusy)
            {
                return MachineCommandResult.Rejected(
                    ErrorCode.CommandRejected,
                    $"{commandName} rejected because an axis is already busy.",
                    sw.Elapsed,
                    correlationId);
            }

            _axisBusy = true;
            _activeMotionCancellation = motionCancellation;
            _axes = _axes.Select(axis => axis with { IsMoving = true, ServoOn = _servoEnabled }).ToArray();
        }

        try
        {
            await Task.Delay(latency, linkedCancellation.Token).ConfigureAwait(false);
            var message = applyState();
            return MachineCommandResult.Success(message, sw.Elapsed, correlationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return MachineCommandResult.Cancelled(
                ErrorCode.CommandCancelled,
                $"{commandName} was cancelled.",
                sw.Elapsed,
                correlationId);
        }
        catch (OperationCanceledException) when (motionCancellation.IsCancellationRequested)
        {
            return MachineCommandResult.Cancelled(
                ErrorCode.CommandCancelled,
                $"{commandName} was stopped before completion.",
                sw.Elapsed,
                correlationId);
        }
        catch (OperationCanceledException)
        {
            var message = $"{commandName} timed out after {timeout.TotalMilliseconds:0} ms.";
            lock (_gate)
            {
                _alarm = new AlarmSnapshot(ErrorCode.MotionTimeout, message, DateTimeOffset.UtcNow);
                _axes = _axes.Select(axis => axis with
                {
                    Alarm = new AxisAlarm(ErrorCode.MotionTimeout, message, DateTimeOffset.UtcNow)
                }).ToArray();
            }

            return MachineCommandResult.Timeout(ErrorCode.MotionTimeout, message, sw.Elapsed, correlationId);
        }
        catch (InvalidOperationException ex)
        {
            return MachineCommandResult.Rejected(
                ErrorCode.SoftLimitExceeded,
                ex.Message,
                sw.Elapsed,
                correlationId);
        }
        catch (ArgumentException ex)
        {
            return MachineCommandResult.Rejected(
                ErrorCode.CommandRejected,
                ex.Message,
                sw.Elapsed,
                correlationId);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_activeMotionCancellation, motionCancellation))
                {
                    _activeMotionCancellation = null;
                }

                _axisBusy = false;
                _axes = _axes.Select(axis => axis with { IsMoving = false }).ToArray();
            }
        }
    }

    private async Task<MachineCommandResult> RunStopCommandAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken,
        CorrelationId correlationId)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await Task.Delay(StopLatency, cts.Token).ConfigureAwait(false);

            CancellationTokenSource? activeMotion;
            lock (_gate)
            {
                activeMotion = _activeMotionCancellation;
                activeMotion?.Cancel();
                _axisBusy = false;
                _axes = _axes.Select(axis => axis with { IsMoving = false }).ToArray();
            }

            var message = activeMotion is null
                ? "Stop accepted; no active simulator motion was running."
                : "Stop accepted; active simulator motion cancellation requested.";

            return MachineCommandResult.Success(message, sw.Elapsed, correlationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return MachineCommandResult.Cancelled(
                ErrorCode.CommandCancelled,
                "Stop was cancelled.",
                sw.Elapsed,
                correlationId);
        }
        catch (OperationCanceledException)
        {
            return MachineCommandResult.Timeout(
                ErrorCode.CommandTimeout,
                $"Stop timed out after {timeout.TotalMilliseconds:0} ms.",
                sw.Elapsed,
                correlationId);
        }
    }

    private InterlockContext CreateEffectiveContext(InterlockContext context)
    {
        lock (_gate)
        {
            var effectiveDoorClosed = context.DoorClosed && _doorClosed;
            var effectiveEmergencyStop = context.EmergencyStopActive || _emergencyStopActive;
            var effectiveSafetyOk = context.SafetyOk && !effectiveEmergencyStop && effectiveDoorClosed && _airPressureOk;
            var effectiveAxisAlarm = context.AxisAlarm || _servoAlarmActive || _axes.Any(axis => axis.Alarm is not null);
            var effectiveAlarmActive = context.AlarmActive || _alarm is not null || effectiveAxisAlarm;

            return context with
            {
                Connected = context.Connected || _connected,
                ManualMode = _mode == MachineMode.Manual,
                AutoMode = _mode == MachineMode.Auto,
                AxisBusy = context.AxisBusy || _axisBusy,
                EmergencyStopActive = effectiveEmergencyStop,
                DoorClosed = effectiveDoorClosed,
                SafetyOk = effectiveSafetyOk,
                ServoOn = context.ServoOn || _servoEnabled,
                AxisAlarm = effectiveAxisAlarm,
                CameraConnected = context.CameraConnected && _cameraReady,
                IoReady = context.IoReady && _airPressureOk && _vacuumOk,
                AlarmActive = effectiveAlarmActive
            };
        }
    }

    private static MachineCommandResult CreateRejectedResult(CommandAvailability availability, CorrelationId correlationId)
    {
        var errorCode = MapAvailabilityError(availability);
        return MachineCommandResult.Rejected(errorCode, availability.DisabledReason, TimeSpan.Zero, correlationId);
    }

    private static ErrorCode MapAvailabilityError(CommandAvailability availability)
    {
        var codes = availability.Violations.Select(violation => violation.Code).ToHashSet(StringComparer.Ordinal);

        if (codes.Contains("ILK-SAFETY-002"))
        {
            return ErrorCode.EmergencyStopActive;
        }

        if (codes.Contains("ILK-SAFETY-003"))
        {
            return ErrorCode.DoorOpen;
        }

        if (codes.Contains("ILK-MOTION-003"))
        {
            return ErrorCode.ServoOff;
        }

        if (codes.Contains("ILK-MOTION-004"))
        {
            return ErrorCode.SoftLimitExceeded;
        }

        if (codes.Contains("ILK-MOTION-005") || codes.Contains("ILK-MOTION-006"))
        {
            return ErrorCode.AxisNotHomed;
        }

        return ErrorCode.CommandRejected;
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

    private string ApplyConnect()
    {
        lock (_gate)
        {
            _connected = true;
            _mode = MachineMode.Manual;
            _alarm = null;
            ResetFaultsLocked();
        }

        return "Virtual controller connected.";
    }

    private string ApplyDisconnect()
    {
        lock (_gate)
        {
            _connected = false;
            _mode = MachineMode.Offline;
            _servoEnabled = false;
            _axisBusy = false;
            _activeMotionCancellation?.Cancel();
            _activeMotionCancellation = null;
            ResetFaultsLocked();
            _axes = AxisDefaults.CreatePowerOffAxes();
        }

        return "Virtual controller disconnected.";
    }

    private string ApplyFaultState(EquipmentFaultInjectionRequest request)
    {
        lock (_gate)
        {
            switch (request.Kind)
            {
                case EquipmentFaultKind.EmergencyStop:
                    _emergencyStopActive = request.IsActive;
                    if (request.IsActive)
                    {
                        _servoEnabled = false;
                    }

                    break;
                case EquipmentFaultKind.DoorOpen:
                    _doorClosed = !request.IsActive;
                    break;
                case EquipmentFaultKind.AirPressureLow:
                    _airPressureOk = !request.IsActive;
                    break;
                case EquipmentFaultKind.VacuumLoss:
                    _vacuumOk = !request.IsActive;
                    break;
                case EquipmentFaultKind.CameraNotReady:
                    _cameraReady = !request.IsActive;
                    break;
                case EquipmentFaultKind.ServoAlarm:
                    _servoAlarmActive = request.IsActive;
                    if (request.IsActive)
                    {
                        _servoEnabled = false;
                    }

                    break;
                case EquipmentFaultKind.ClearAll:
                    ResetFaultsLocked();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unsupported fault injection kind.");
            }

            ApplyFaultEffectsLocked();
        }

        return request.Kind == EquipmentFaultKind.ClearAll
            ? "All simulator faults cleared."
            : $"{FormatFault(request.Kind)} fault {(request.IsActive ? "injected" : "cleared")}.";
    }

    private string ApplyJogStep(IReadOnlyDictionary<string, string> parameters)
    {
        var fallback = new JogMotionTarget(AxisId.X, MotionDirection.Positive, 1.0);
        if (!MotionCommandParameterParser.TryReadJogTarget(parameters, fallback, out var jogTarget, out var error))
        {
            throw new ArgumentException(error, nameof(parameters));
        }

        lock (_gate)
        {
            var axes = _axes.ToArray();
            var selectedAxis = axes.Single(axis => axis.AxisId == jogTarget.AxisId);
            var signedStep = (int)jogTarget.Direction * jogTarget.Step;
            var target = selectedAxis.Position + signedStep;
            if (!selectedAxis.SoftLimit.Contains(target))
            {
                throw new InvalidOperationException($"Jog target exceeded the {FormatAxis(jogTarget.AxisId)} axis soft limit.");
            }

            _axes = axes.Select(axis => axis.AxisId == jogTarget.AxisId
                ? axis with
                {
                    Position = target,
                    Target = target,
                    ServoOn = _servoEnabled,
                    IsMoving = false,
                    Alarm = null
                }
                : axis with { IsMoving = false, ServoOn = _servoEnabled }).ToArray();
        }

        var direction = jogTarget.Direction == MotionDirection.Positive ? "+" : "-";
        return $"Jog completed: {FormatAxis(jogTarget.AxisId)} {direction}{jogTarget.Step:0.000}.";
    }

    private string ApplyMoveAbsolute(IReadOnlyDictionary<string, string> parameters)
    {
        var fallback = new AbsoluteMoveTarget(10.0, 20.0, 5.0, 0.0);
        if (!MotionCommandParameterParser.TryReadAbsoluteMoveTarget(parameters, fallback, out var moveTarget, out var error))
        {
            throw new ArgumentException(error, nameof(parameters));
        }

        var targets = new Dictionary<AxisId, double>
        {
            [AxisId.X] = moveTarget.X,
            [AxisId.Y] = moveTarget.Y,
            [AxisId.Z] = moveTarget.Z,
            [AxisId.Theta] = moveTarget.Theta
        };

        lock (_gate)
        {
            foreach (var axis in _axes)
            {
                if (!axis.SoftLimit.Contains(targets[axis.AxisId]))
                {
                    throw new InvalidOperationException($"{axis.AxisId} target exceeded soft limit.");
                }
            }

            _axes = _axes.Select(axis => axis with
            {
                Position = targets[axis.AxisId],
                Target = targets[axis.AxisId],
                Profile = new MotionProfile(
                    moveTarget.Velocity,
                    moveTarget.Acceleration,
                    moveTarget.Deceleration,
                    moveTarget.Jerk,
                    axis.Profile.Unit),
                ServoOn = _servoEnabled,
                IsMoving = false,
                Alarm = null
            }).ToArray();
        }

        return $"Move Absolute completed for simulator target X={moveTarget.X:0.000}, Y={moveTarget.Y:0.000}, Z={moveTarget.Z:0.000}, Theta={moveTarget.Theta:0.000}, profile={moveTarget.ProfilePreset}, velocity={moveTarget.Velocity:0.000}, tolerance={moveTarget.ArrivalTolerance:0.000}.";
    }

    private EquipmentSnapshot CreateSnapshot(DateTimeOffset timestamp)
    {
        bool connected;
        bool servoEnabled;
        bool doorClosed;
        bool emergencyStopActive;
        bool airPressureOk;
        bool vacuumOk;
        bool cameraReady;
        AlarmSnapshot? alarm;
        IReadOnlyList<AxisSnapshot> axes;
        lock (_gate)
        {
            connected = _connected;
            servoEnabled = _servoEnabled;
            doorClosed = _doorClosed;
            emergencyStopActive = _emergencyStopActive;
            airPressureOk = _airPressureOk;
            vacuumOk = _vacuumOk;
            cameraReady = _cameraReady;
            var mode = connected ? _mode : MachineMode.Offline;
            alarm = _alarm;
            axes = _axes;
            return CreateSnapshot(
                timestamp,
                connected,
                mode,
                servoEnabled,
                doorClosed,
                emergencyStopActive,
                airPressureOk,
                vacuumOk,
                cameraReady,
                alarm,
                axes);
        }
    }

    private static EquipmentSnapshot CreateSnapshot(
        DateTimeOffset timestamp,
        bool connected,
        MachineMode mode,
        bool servoEnabled,
        bool doorClosed,
        bool emergencyStopActive,
        bool airPressureOk,
        bool vacuumOk,
        bool cameraReady,
        AlarmSnapshot? alarm,
        IReadOnlyList<AxisSnapshot> axes)
    {
        var safety = new SafetySnapshot(
            DoorClosed: doorClosed,
            EmergencyStopActive: emergencyStopActive,
            AirPressureOk: airPressureOk,
            VacuumOn: vacuumOk,
            ServoEnabled: servoEnabled);

        return new EquipmentSnapshot(
            connected,
            mode,
            safety,
            axes,
            CreateIoSnapshot(timestamp, servoEnabled, doorClosed, emergencyStopActive, airPressureOk, vacuumOk, cameraReady, axes),
            new CameraSnapshot(connected && cameraReady, "Virtual 3D camera", timestamp),
            alarm,
            timestamp);
    }

    private static IoSnapshot CreateIoSnapshot(
        DateTimeOffset timestamp,
        bool servoEnabled,
        bool doorClosed,
        bool emergencyStopActive,
        bool airPressureOk,
        bool vacuumOk,
        bool cameraReady,
        IReadOnlyList<AxisSnapshot> axes)
    {
        var servoAlarm = axes.Any(axis => axis.Alarm?.ErrorCode.Code == ErrorCode.ServoAlarm.Code);
        return new IoSnapshot(
            new[]
            {
                new IoBitSnapshot("DI_DOOR_CLOSED", "X000", IoBitDirection.Input, doorClosed, !doorClosed),
                new IoBitSnapshot("DI_ESTOP_ON", "X001", IoBitDirection.Input, emergencyStopActive, emergencyStopActive),
                new IoBitSnapshot("DI_AIR_PRESSURE_OK", "X002", IoBitDirection.Input, airPressureOk, !airPressureOk),
                new IoBitSnapshot("DI_PRODUCT_PRESENT", "X003", IoBitDirection.Input, true, false),
                new IoBitSnapshot("DI_CAMERA_READY", "X004", IoBitDirection.Input, cameraReady, !cameraReady),
                new IoBitSnapshot("DI_VACUUM_OK", "X005", IoBitDirection.Input, vacuumOk, !vacuumOk),
                new IoBitSnapshot("DI_SERVO_ALARM", "X006", IoBitDirection.Input, servoAlarm, servoAlarm),
                new IoBitSnapshot("DO_SERVO_ENABLE", "Y000", IoBitDirection.Output, servoEnabled, false),
                new IoBitSnapshot("DO_VACUUM_ON", "Y001", IoBitDirection.Output, vacuumOk, false),
                new IoBitSnapshot("DO_RING_LIGHT_ON", "Y002", IoBitDirection.Output, false, false),
                new IoBitSnapshot("DO_BUZZER_ON", "Y003", IoBitDirection.Output, false, false),
                new IoBitSnapshot("DO_TOWER_GREEN", "Y004", IoBitDirection.Output, false, false),
                new IoBitSnapshot("DO_TOWER_YELLOW", "Y005", IoBitDirection.Output, false, false),
                new IoBitSnapshot("DO_TOWER_RED", "Y006", IoBitDirection.Output, false, false)
            },
            timestamp);
    }

    private void ResetFaultsLocked()
    {
        _doorClosed = true;
        _emergencyStopActive = false;
        _airPressureOk = true;
        _vacuumOk = true;
        _cameraReady = true;
        _servoAlarmActive = false;
        _alarm = null;
        _axes = _axes.Select(axis => axis with
        {
            Alarm = axis.Alarm?.ErrorCode.Code == ErrorCode.ServoAlarm.Code ? null : axis.Alarm,
            ServoOn = _servoEnabled
        }).ToArray();
    }

    private void ApplyFaultEffectsLocked()
    {
        if (_emergencyStopActive || _servoAlarmActive)
        {
            _servoEnabled = false;
        }

        _axes = _axes.Select(axis => axis with
        {
            ServoOn = _servoEnabled,
            Alarm = _servoAlarmActive
                ? new AxisAlarm(ErrorCode.ServoAlarm, ErrorCode.ServoAlarm.Message, DateTimeOffset.UtcNow)
                : axis.Alarm?.ErrorCode.Code == ErrorCode.ServoAlarm.Code ? null : axis.Alarm
        }).ToArray();

        RefreshAlarmFromFaultsLocked();
        if (_connected)
        {
            if (_alarm is not null)
            {
                _mode = MachineMode.Alarm;
            }
            else if (_mode == MachineMode.Alarm)
            {
                _mode = MachineMode.Manual;
            }
        }
    }

    private void RefreshAlarmFromFaultsLocked()
    {
        var now = DateTimeOffset.UtcNow;
        _alarm = true switch
        {
            _ when _emergencyStopActive => new AlarmSnapshot(ErrorCode.EmergencyStopActive, "Emergency stop fault is active.", now),
            _ when _servoAlarmActive => new AlarmSnapshot(ErrorCode.ServoAlarm, "Servo alarm fault is active.", now),
            _ when !_doorClosed => new AlarmSnapshot(ErrorCode.DoorOpen, "Door open fault is active.", now),
            _ when !_airPressureOk => new AlarmSnapshot(ErrorCode.AirPressureLow, "Air pressure low fault is active.", now),
            _ when !_cameraReady => new AlarmSnapshot(ErrorCode.CameraNotReady, "Camera ready signal is off.", now),
            _ when !_vacuumOk => new AlarmSnapshot(ErrorCode.VacuumLoss, "Vacuum loss fault is active.", now),
            _ => null
        };
    }

    private static string FormatFault(EquipmentFaultKind kind)
    {
        return kind switch
        {
            EquipmentFaultKind.EmergencyStop => "Emergency Stop",
            EquipmentFaultKind.DoorOpen => "Door Open",
            EquipmentFaultKind.AirPressureLow => "Air Pressure Low",
            EquipmentFaultKind.VacuumLoss => "Vacuum Loss",
            EquipmentFaultKind.CameraNotReady => "Camera Not Ready",
            EquipmentFaultKind.ServoAlarm => "Servo Alarm",
            EquipmentFaultKind.ClearAll => "Clear All",
            _ => kind.ToString()
        };
    }

    private static string FormatAxis(AxisId axisId)
    {
        return axisId == AxisId.Theta ? "T" : axisId.ToString();
    }

    private static class EmptyParameters
    {
        internal static readonly IReadOnlyDictionary<string, string> Value = new Dictionary<string, string>();
    }

}
