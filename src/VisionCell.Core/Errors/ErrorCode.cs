namespace VisionCell.Core.Errors;

public readonly record struct ErrorCode(string Code, string Message)
{
    public static readonly ErrorCode ControllerConnectionFailed = new("EQP-001", "Controller connection failed.");
    public static readonly ErrorCode HeartbeatLost = new("EQP-002", "Controller heartbeat was lost.");
    public static readonly ErrorCode EmergencyStopActive = new("EQP-003", "Emergency stop is active.");
    public static readonly ErrorCode DoorOpen = new("EQP-004", "Door is open.");
    public static readonly ErrorCode CommandTimeout = new("EQP-005", "Command timed out.");
    public static readonly ErrorCode CommandCancelled = new("EQP-006", "Command was cancelled.");
    public static readonly ErrorCode CommandRejected = new("EQP-007", "Command rejected by interlock.");
    public static readonly ErrorCode ServoOff = new("MOT-001", "Servo is off.");
    public static readonly ErrorCode AxisNotHomed = new("MOT-002", "Axis is not homed.");
    public static readonly ErrorCode MotionTimeout = new("MOT-003", "Motion command timed out.");
    public static readonly ErrorCode SoftLimitExceeded = new("MOT-004", "Target exceeds soft limit.");
    public static readonly ErrorCode CameraGrabTimeout = new("CAM-001", "Camera grab timed out.");
    public static readonly ErrorCode CameraNotReady = new("CAM-002", "Camera is not ready.");
    public static readonly ErrorCode CameraGrabFailed = new("CAM-003", "Camera grab failed.");
    public static readonly ErrorCode PersistenceFailed = new("DB-001", "Persistence failed.");
}
