namespace VisionCell.Core.Interlocks;

public sealed record InterlockContext(
    bool Connected,
    bool ControllerBusy,
    bool SequenceRunning,
    bool EmergencyStopActive,
    bool DoorClosed,
    bool SafetyOk,
    bool ManualMode,
    bool AutoMode,
    bool ServoOn,
    bool AxisHomed,
    bool AllRequiredAxesHomed,
    bool AxisBusy,
    bool AxisAlarm,
    bool WithinSoftLimit,
    bool RecipeLoaded,
    bool CameraConnected,
    bool IoReady,
    bool AlarmActive)
{
    public static InterlockContext Disconnected { get; } = new(
        Connected: false,
        ControllerBusy: false,
        SequenceRunning: false,
        EmergencyStopActive: false,
        DoorClosed: true,
        SafetyOk: true,
        ManualMode: false,
        AutoMode: false,
        ServoOn: false,
        AxisHomed: false,
        AllRequiredAxesHomed: false,
        AxisBusy: false,
        AxisAlarm: false,
        WithinSoftLimit: true,
        RecipeLoaded: false,
        CameraConnected: false,
        IoReady: true,
        AlarmActive: false);
}
