using FluentAssertions;

namespace NetLift.Tests.Integration;

/// <summary>
/// Placeholder integration test to verify test infrastructure.
/// </summary>
public class InfrastructureTests
{
    [Fact]
    public void FluentAssertions_Should_BeConfiguredCorrectly()
    {
        // Arrange
        var expected = "NetLift.Integration";
        var actual = "NetLift.Integration";

        // Act & Assert
        actual.Should().Be(expected);
    }
}