namespace NetLift.Tests.Unit.Commands;

using FluentAssertions;
using Moq;
using NetLift.Cli.Commands;
using NetLift.Core.Interfaces;
using Xunit;

/// <summary>
/// Tests for the ValidateCommand class.
/// </summary>
public sealed class ValidateCommandTests
{
    [Fact]
    public void Constructor_WithNullBuildValidator_ThrowsArgumentNullException()
    {
        // Arrange
        var testRunner = new Mock<ITestRunner>().Object;
        var confidenceScorer = new Mock<IConfidenceScorer>().Object;
        var htmlReportGenerator = new Mock<IFullHtmlReportGenerator>().Object;

        // Act & Assert
        var act = () => new ValidateCommand(
            null!,
            testRunner,
            confidenceScorer,
            htmlReportGenerator);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("buildValidator");
    }

    [Fact]
    public void Constructor_WithNullTestRunner_ThrowsArgumentNullException()
    {
        // Arrange
        var buildValidator = new Mock<IBuildValidator>().Object;
        var confidenceScorer = new Mock<IConfidenceScorer>().Object;
        var htmlReportGenerator = new Mock<IFullHtmlReportGenerator>().Object;

        // Act & Assert
        var act = () => new ValidateCommand(
            buildValidator,
            null!,
            confidenceScorer,
            htmlReportGenerator);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("testRunner");
    }

    [Fact]
    public void Constructor_WithNullConfidenceScorer_ThrowsArgumentNullException()
    {
        // Arrange
        var buildValidator = new Mock<IBuildValidator>().Object;
        var testRunner = new Mock<ITestRunner>().Object;
        var htmlReportGenerator = new Mock<IFullHtmlReportGenerator>().Object;

        // Act & Assert
        var act = () => new ValidateCommand(
            buildValidator,
            testRunner,
            null!,
            htmlReportGenerator);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("confidenceScorer");
    }

    [Fact]
    public void Constructor_WithNullHtmlReportGenerator_ThrowsArgumentNullException()
    {
        // Arrange
        var buildValidator = new Mock<IBuildValidator>().Object;
        var testRunner = new Mock<ITestRunner>().Object;
        var confidenceScorer = new Mock<IConfidenceScorer>().Object;

        // Act & Assert
        var act = () => new ValidateCommand(
            buildValidator,
            testRunner,
            confidenceScorer,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("htmlReportGenerator");
    }

    [Fact]
    public void Constructor_WithAllValidArguments_CreatesInstance()
    {
        // Arrange
        var buildValidator = new Mock<IBuildValidator>().Object;
        var testRunner = new Mock<ITestRunner>().Object;
        var confidenceScorer = new Mock<IConfidenceScorer>().Object;
        var htmlReportGenerator = new Mock<IFullHtmlReportGenerator>().Object;

        // Act
        var command = new ValidateCommand(
            buildValidator,
            testRunner,
            confidenceScorer,
            htmlReportGenerator);

        // Assert
        command.Should().NotBeNull();
    }
}
