using FluentAssertions;
using NetLift.Tests.Unit.TestHelpers;

namespace NetLift.Tests.Unit;

/// <summary>
/// Placeholder test class to verify xUnit, FluentAssertions, and test infrastructure is working.
/// </summary>
public class InfrastructureTests
{
    [Fact]
    public void FluentAssertions_Should_BeConfiguredCorrectly()
    {
        // Arrange
        var expected = "NetLift";
        var actual = "NetLift";

        // Act & Assert
        actual.Should().Be(expected);
        actual.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TestFixtureHelper_Should_ReturnValidBasePath()
    {
        // Act
        var basePath = TestFixtureHelper.GetFixturesBasePath();

        // Assert
        basePath.Should().NotBeNullOrEmpty();
        basePath.Should().EndWith(Path.Combine("tests", "fixtures"));
    }

    [Fact]
    public void TestFixtureHelper_Should_BuildCorrectFixturePath()
    {
        // Arrange
        var fixtureName = "test-fixture";
        var fileName = "test.txt";

        // Act
        var path = TestFixtureHelper.GetFixturePath(fixtureName, fileName);

        // Assert
        path.Should().Contain(fixtureName);
        path.Should().EndWith(fileName);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(5, 5, 10)]
    [InlineData(-1, 1, 0)]
    public void Theory_Example_Should_AddNumbersCorrectly(int a, int b, int expected)
    {
        // Act
        var result = a + b;

        // Assert
        result.Should().Be(expected);
    }
}