namespace VisionCell.Core.Commands;

public enum CommandKind
{
    Connect,
    Disconnect,
    ServoOn,
    ServoOff,
    Home,
    Jog,
    MoveAbsolute,
    Stop,
    ResetAlarm,
    RunInspection
}
