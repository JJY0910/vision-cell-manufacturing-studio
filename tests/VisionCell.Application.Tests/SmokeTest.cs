using FluentAssertions;
using Xunit;

namespace VisionCell_Application_Tests;

public sealed class SmokeTest
{
    [Fact]
    public void Placeholder_Should_Pass()
    {
        true.Should().BeTrue();
    }
}
