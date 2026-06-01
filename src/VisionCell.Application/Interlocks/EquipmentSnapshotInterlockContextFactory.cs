using VisionCell.Core.Interlocks;
using VisionCell.Core.Primitives;
using VisionCell.Equipment.Controllers;

namespace VisionCell.Application.Interlocks;

public static class EquipmentSnapshotInterlockContextFactory
{
    public static InterlockContext Create(EquipmentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var anyAxisBusy = snapshot.Axes.Any(axis => axis.IsMoving);
        var anyAxisAlarm = snapshot.Axes.Any(axis => axis.Alarm is not null) || snapshot.Alarm is not null;
        var allAxesHomed = snapshot.Axes.Count > 0 && snapshot.Axes.All(axis => axis.IsHomed);
        var servoOn = snapshot.Safety.ServoEnabled || snapshot.Axes.Any(axis => axis.ServoOn);
        var safetyOk = !snapshot.Safety.EmergencyStopActive && snapshot.Safety.DoorClosed && snapshot.Safety.AirPressureOk;
        var withinSoftLimit = snapshot.Axes.All(axis => axis.SoftLimit.Contains(axis.Target));

        return new InterlockContext(
            Connected: snapshot.IsConnected,
            ControllerBusy: false,
            SequenceRunning: false,
            EmergencyStopActive: snapshot.Safety.EmergencyStopActive,
            DoorClosed: snapshot.Safety.DoorClosed,
            SafetyOk: safetyOk,
            ManualMode: snapshot.Mode == MachineMode.Manual,
            AutoMode: snapshot.Mode == MachineMode.Auto,
            ServoOn: servoOn,
            AxisHomed: allAxesHomed,
            AllRequiredAxesHomed: allAxesHomed,
            AxisBusy: anyAxisBusy,
            AxisAlarm: anyAxisAlarm,
            WithinSoftLimit: withinSoftLimit,
            RecipeLoaded: false,
            CameraConnected: snapshot.Camera.IsReady,
            IoReady: snapshot.Safety.AirPressureOk,
            AlarmActive: snapshot.Alarm is not null || snapshot.Mode == MachineMode.Alarm || anyAxisAlarm);
    }
}
