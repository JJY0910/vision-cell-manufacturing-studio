using FluentAssertions;
using VisionCell.Core.Alarms;
using VisionCell.Core.Commands;
using VisionCell.Core.Errors;
using VisionCell.Core.Events;
using VisionCell.Core.Primitives;
using Xunit;

namespace VisionCell_Core_Tests;

public sealed class MachineCommandResultTests
{
    [Fact]
    public void ToSystemEvent_Should_Preserve_Correlation_And_Map_Success_To_Info()
    {
        var correlationId = CorrelationId.New();
        var result = new MachineCommandResult(
            CommandStatus.Success,
            null,
            "Connected.",
            TimeSpan.FromMilliseconds(42),
            correlationId);

        var systemEvent = result.ToSystemEvent("Equipment", "Connect");

        systemEvent.CorrelationId.Should().Be(correlationId);
        systemEvent.Severity.Should().Be(SystemEventSeverity.Info);
        systemEvent.Source.Should().Be("Equipment");
        systemEvent.EventType.Should().Be("Connect");
        systemEvent.Message.Should().Be("Connected.");
    }

    [Fact]
    public void ToSystemEvent_Should_Map_Timeout_To_Alarm()
    {
        var result = new MachineCommandResult(
            CommandStatus.Timeout,
            ErrorCode.CommandTimeout,
            "Connect timed out.",
            TimeSpan.FromSeconds(3),
            CorrelationId.New());

        result.ToSystemEvent("Equipment", "Connect").Severity.Should().Be(SystemEventSeverity.Alarm);
    }

    [Fact]
    public void EquipmentAlarmFactory_Should_Map_ErrorCode_To_Area_And_Severity()
    {
        var alarm = EquipmentAlarmFactory.FromFailure(
            ErrorCode.EmergencyStopActive,
            EquipmentArea.Equipment,
            "Emergency stop is active.",
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            "corr-001");

        alarm.Code.Should().Be("EQP-003");
        alarm.Area.Should().Be(EquipmentArea.Safety);
        alarm.Severity.Should().Be(EquipmentAlarmSeverity.Critical);
        alarm.CorrelationId.Should().Be("corr-001");

        var air = EquipmentAlarmFactory.FromFailure(
            ErrorCode.AirPressureLow,
            EquipmentArea.Equipment,
            "Air pressure low.",
            alarm.OccurredAt);
        var servo = EquipmentAlarmFactory.FromFailure(
            ErrorCode.ServoAlarm,
            EquipmentArea.Equipment,
            "Servo alarm.",
            alarm.OccurredAt);

        air.Area.Should().Be(EquipmentArea.Safety);
        air.Severity.Should().Be(EquipmentAlarmSeverity.Critical);
        servo.Area.Should().Be(EquipmentArea.Motion);
        servo.Severity.Should().Be(EquipmentAlarmSeverity.Critical);
    }

    [Fact]
    public void ErrorCodeCatalog_Should_Surface_Documented_Code_Area_Severity_And_Recovery()
    {
        var catalog = ErrorCodeCatalog.Entries;

        catalog.Select(entry => entry.Code).Should().BeEquivalentTo(
        [
            "EQP-001",
            "EQP-002",
            "EQP-003",
            "EQP-004",
            "EQP-005",
            "EQP-006",
            "EQP-007",
            "EQP-008",
            "EQP-009",
            "MOT-001",
            "MOT-002",
            "MOT-003",
            "MOT-004",
            "MOT-005",
            "CAM-001",
            "CAM-002",
            "CAM-003",
            "VIS-001",
            "VIS-002",
            "DB-001"
        ]);

        var estop = ErrorCodeCatalog.Find("EQP-003");
        estop.Should().NotBeNull();
        estop!.Area.Should().Be(EquipmentArea.Safety);
        estop.Severity.Should().Be(EquipmentAlarmSeverity.Critical);
        estop.RecoveryAction.Should().Contain("Release EStop");

        var unknown = ErrorCodeCatalog.GetRecoveryAction("PLC-999", EquipmentArea.Safety);
        unknown.Should().Contain("safety inputs");
    }

    [Fact]
    public void Acknowledge_Should_Return_Acknowledged_Alarm_With_Memo()
    {
        var alarm = new EquipmentAlarm(
            Guid.NewGuid(),
            "CAM-001",
            EquipmentAlarmSeverity.Error,
            EquipmentArea.Camera,
            "Camera grab timed out.",
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

        var acknowledged = alarm.Acknowledge(
            new DateTimeOffset(2026, 6, 1, 12, 5, 0, TimeSpan.Zero),
            "Checked camera trigger.");

        acknowledged.IsAcknowledged.Should().BeTrue();
        acknowledged.ActionMemo.Should().Be("Checked camera trigger.");
    }
}
