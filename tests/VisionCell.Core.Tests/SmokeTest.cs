using FluentAssertions;
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
}
