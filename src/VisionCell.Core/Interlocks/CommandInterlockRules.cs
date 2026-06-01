using VisionCell.Core.Commands;

namespace VisionCell.Core.Interlocks;

public static class CommandInterlockRules
{
    public static CommandAvailability Evaluate(CommandKind command, InterlockContext context)
    {
        var violations = new List<InterlockViolation>();

        switch (command)
        {
            case CommandKind.Connect:
                Require(!context.Connected, "ILK-CONNECT-001", InterlockSeverity.Warning, "Connect requires disconnected controller.", violations);
                Require(!context.ControllerBusy, "ILK-COMMON-001", InterlockSeverity.Warning, "Connect requires idle controller.", violations);
                break;
            case CommandKind.Disconnect:
                Require(context.Connected, "ILK-COMMON-002", InterlockSeverity.Warning, "Disconnect requires connected controller.", violations);
                Require(!context.SequenceRunning, "ILK-DISCONNECT-001", InterlockSeverity.Warning, "Disconnect requires sequence stopped.", violations);
                Require(!context.ControllerBusy, "ILK-COMMON-001", InterlockSeverity.Warning, "Disconnect requires idle controller.", violations);
                break;
            case CommandKind.ServoOn:
                RequireConnected(context, command, violations);
                Require(context.SafetyOk, "ILK-SAFETY-001", InterlockSeverity.Alarm, "Servo On requires safety OK.", violations);
                Require(!context.EmergencyStopActive, "ILK-SAFETY-002", InterlockSeverity.Alarm, "Servo On requires emergency stop released.", violations);
                Require(context.DoorClosed, "ILK-SAFETY-003", InterlockSeverity.Alarm, "Servo On requires door closed.", violations);
                Require(!context.AxisAlarm, "ILK-MOTION-001", InterlockSeverity.Alarm, "Servo On requires no active axis alarm.", violations);
                Require(!context.AxisBusy, "ILK-MOTION-002", InterlockSeverity.Warning, "Servo On requires axis idle.", violations);
                break;
            case CommandKind.ServoOff:
                RequireConnected(context, command, violations);
                Require(context.ServoOn, "ILK-MOTION-003", InterlockSeverity.Warning, "Servo Off requires servo on.", violations);
                Require(!context.AxisBusy, "ILK-MOTION-002", InterlockSeverity.Warning, "Servo Off requires axis idle.", violations);
                Require(!context.SequenceRunning, "ILK-SEQUENCE-001", InterlockSeverity.Warning, "Servo Off requires sequence stopped.", violations);
                break;
            case CommandKind.Home:
                RequireConnected(context, command, violations);
                Require(context.ServoOn, "ILK-MOTION-003", InterlockSeverity.Alarm, "Home requires servo on.", violations);
                Require(context.SafetyOk, "ILK-SAFETY-001", InterlockSeverity.Alarm, "Home requires safety OK.", violations);
                Require(context.ManualMode, "ILK-MODE-001", InterlockSeverity.Warning, "Home requires Manual mode.", violations);
                Require(!context.AxisBusy, "ILK-MOTION-002", InterlockSeverity.Warning, "Home requires axis idle.", violations);
                Require(!context.SequenceRunning, "ILK-SEQUENCE-001", InterlockSeverity.Warning, "Home requires sequence stopped.", violations);
                break;
            case CommandKind.Jog:
                RequireConnected(context, command, violations);
                Require(context.ServoOn, "ILK-MOTION-003", InterlockSeverity.Alarm, "Jog requires servo on.", violations);
                Require(context.SafetyOk, "ILK-SAFETY-001", InterlockSeverity.Alarm, "Jog requires safety OK.", violations);
                Require(context.ManualMode, "ILK-MODE-001", InterlockSeverity.Warning, "Jog requires Manual mode.", violations);
                Require(!context.AxisBusy, "ILK-MOTION-002", InterlockSeverity.Warning, "Jog requires axis idle.", violations);
                Require(!context.SequenceRunning, "ILK-SEQUENCE-001", InterlockSeverity.Warning, "Jog requires sequence stopped.", violations);
                Require(context.WithinSoftLimit, "ILK-MOTION-004", InterlockSeverity.Warning, "Jog target must be within soft limit.", violations);
                break;
            case CommandKind.MoveAbsolute:
                RequireConnected(context, command, violations);
                Require(context.ServoOn, "ILK-MOTION-003", InterlockSeverity.Alarm, "Move Absolute requires servo on.", violations);
                Require(context.SafetyOk, "ILK-SAFETY-001", InterlockSeverity.Alarm, "Move Absolute requires safety OK.", violations);
                Require(context.ManualMode, "ILK-MODE-001", InterlockSeverity.Warning, "Move Absolute requires Manual mode.", violations);
                Require(context.AxisHomed, "ILK-MOTION-005", InterlockSeverity.Warning, "Move Absolute requires homed axis.", violations);
                Require(!context.AxisBusy, "ILK-MOTION-002", InterlockSeverity.Warning, "Move Absolute requires axis idle.", violations);
                Require(!context.SequenceRunning, "ILK-SEQUENCE-001", InterlockSeverity.Warning, "Move Absolute requires sequence stopped.", violations);
                Require(context.WithinSoftLimit, "ILK-MOTION-004", InterlockSeverity.Warning, "Move Absolute target must be within soft limit.", violations);
                break;
            case CommandKind.Stop:
                RequireConnected(context, command, violations);
                Require(context.AxisBusy || context.SequenceRunning, "ILK-STOP-001", InterlockSeverity.Warning, "Stop requires moving axis or running sequence.", violations);
                break;
            case CommandKind.ResetAlarm:
                RequireConnected(context, command, violations);
                Require(context.AlarmActive, "ILK-ALARM-001", InterlockSeverity.Warning, "Reset Alarm requires active alarm.", violations);
                Require(!context.EmergencyStopActive, "ILK-SAFETY-002", InterlockSeverity.Alarm, "Reset Alarm requires emergency stop released.", violations);
                Require(context.DoorClosed, "ILK-SAFETY-003", InterlockSeverity.Alarm, "Reset Alarm requires door closed.", violations);
                break;
            case CommandKind.RunInspection:
                RequireConnected(context, command, violations);
                Require(context.AutoMode, "ILK-MODE-002", InterlockSeverity.Warning, "Run Inspection requires Auto mode.", violations);
                Require(context.RecipeLoaded, "ILK-RECIPE-001", InterlockSeverity.Warning, "Run Inspection requires loaded recipe.", violations);
                Require(context.CameraConnected, "ILK-CAMERA-001", InterlockSeverity.Warning, "Run Inspection requires camera connected.", violations);
                Require(context.IoReady, "ILK-IO-001", InterlockSeverity.Warning, "Run Inspection requires I/O ready.", violations);
                Require(context.SafetyOk, "ILK-SAFETY-001", InterlockSeverity.Alarm, "Run Inspection requires safety OK.", violations);
                Require(!context.SequenceRunning, "ILK-SEQUENCE-001", InterlockSeverity.Warning, "Run Inspection requires sequence stopped.", violations);
                Require(context.AllRequiredAxesHomed, "ILK-MOTION-006", InterlockSeverity.Warning, "Run Inspection requires all required axes homed.", violations);
                Require(!context.AxisBusy, "ILK-MOTION-002", InterlockSeverity.Warning, "Run Inspection requires axis idle.", violations);
                Require(!context.AxisAlarm, "ILK-MOTION-001", InterlockSeverity.Alarm, "Run Inspection requires no active axis alarm.", violations);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported command kind.");
        }

        return violations.Count == 0
            ? CommandAvailability.Available(command)
            : CommandAvailability.Blocked(command, violations);
    }

    private static void RequireConnected(InterlockContext context, CommandKind command, ICollection<InterlockViolation> violations)
    {
        Require(context.Connected, "ILK-COMMON-002", InterlockSeverity.Warning, $"{FormatCommand(command)} requires connected controller.", violations);
    }

    private static void Require(bool condition, string code, InterlockSeverity severity, string message, ICollection<InterlockViolation> violations)
    {
        if (!condition)
        {
            violations.Add(new InterlockViolation(code, severity, message));
        }
    }

    private static string FormatCommand(CommandKind command)
    {
        return command switch
        {
            CommandKind.ServoOn => "Servo On",
            CommandKind.ServoOff => "Servo Off",
            CommandKind.MoveAbsolute => "Move Absolute",
            CommandKind.ResetAlarm => "Reset Alarm",
            CommandKind.RunInspection => "Run Inspection",
            _ => command.ToString()
        };
    }
}
