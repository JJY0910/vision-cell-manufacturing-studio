using FluentAssertions;
using Xunit;

namespace VisionCell_Persistence_Tests;

public sealed class SmokeTest
{
    [Fact]
    public void Placeholder_Should_Pass()
    {
        true.Should().BeTrue();
    }
}
