using VisionCell.Core.Alarms;

namespace VisionCell.App.Modules.Alarm.ViewModels;

internal static class AlarmRecoveryGuidance
{
    public static string GetHint(string code, EquipmentArea area)
    {
        return code switch
        {
            "EQP-001" => "Check the controller profile and reconnect the simulator/controller.",
            "EQP-002" => "Reconnect the controller after heartbeat is restored.",
            "EQP-003" => "Release EStop, verify the safety cause is clear, then run the validated reset path.",
            "EQP-004" => "Close the safety door before restarting automatic operation.",
            "EQP-005" => "Check command timeout conditions and retry after equipment state is stable.",
            "EQP-006" => "Confirm the operator cancellation was intentional before restarting the command.",
            "EQP-007" => "Clear the interlock cause before retrying the rejected command.",
            "EQP-008" => "Restore air pressure and verify the safety summary before reset.",
            "EQP-009" => "Check vacuum line, product pickup, and fixture state before retrying.",
            "MOT-001" => "Turn servo on before motion commands.",
            "MOT-002" => "Home the axis before move or jog commands.",
            "MOT-003" => "Check soft limit, axis load, and motion timeout cause before reset.",
            "MOT-004" => "Change the target position inside configured soft limits.",
            "MOT-005" => "Clear the servo drive alarm and verify axis state before reset.",
            "CAM-001" => "Retry grab after confirming camera trigger and timeout conditions.",
            "CAM-002" => "Check camera readiness before starting inspection.",
            "CAM-003" => "Check camera state and simulator failure injection before retrying grab.",
            "VIS-001" => "Fix Recipe validation issues before running inspection.",
            "VIS-002" => "Review image, ROI, and Recipe parameters before rerun.",
            "DB-001" => "Check the database path and storage availability before retrying persistence.",
            _ => GetAreaFallback(area)
        };
    }

    private static string GetAreaFallback(EquipmentArea area)
    {
        return area switch
        {
            EquipmentArea.Safety => "Verify safety inputs and keep hardware reset separate from acknowledgement.",
            EquipmentArea.Motion => "Review motion state and clear axis cause before retrying.",
            EquipmentArea.Camera => "Review camera readiness and acquisition state before retrying.",
            EquipmentArea.Inspection => "Review Recipe, image, and judgment context before rerun.",
            EquipmentArea.Database => "Review storage and repository availability before retrying.",
            _ => "Review equipment status and record the operator recovery action memo."
        };
    }
}
