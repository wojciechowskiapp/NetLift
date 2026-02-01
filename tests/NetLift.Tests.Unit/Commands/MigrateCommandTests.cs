namespace NetLift.Tests.Unit.Commands;

using FluentAssertions;
using Moq;
using NetLift.Cli.Commands;
using NetLift.Core.Interfaces;
using NetLift.Cli.Services;
using Xunit;

/// <summary>
/// Tests for the MigrateCommand class.
/// </summary>
public sealed class MigrateCommandTests
{
    [Fact]
    public void Constructor_WithNullSolutionParser_ThrowsArgumentNullException()
    {
        // Arrange
        var projectParser = new Mock<IProjectParser>().Object;
        var packagesConfigParser = new Mock<IPackagesConfigParser>().Object;
        var sdkProjectConverter = new Mock<ISdkProjectConverter>().Object;
        var assemblyInfoExtractor = new Mock<IAssemblyInfoExtractor>().Object;
        var packageReferenceConverter = new Mock<IPackageReferenceConverter>().Object;
        var packageMappingService = new Mock<IPackageMappingService>().Object;
        var dryRunService = new Mock<IDryRunService>().Object;
        var orchestrator = new Mock<IMigrationOrchestrator>().Object;

        // Act & Assert
        var act = () => new MigrateCommand(
            null!,
            projectParser,
            packagesConfigParser,
            sdkProjectConverter,
            assemblyInfoExtractor,
            packageReferenceConverter,
            packageMappingService,
            dryRunService,
            orchestrator);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("solutionParser");
    }

    [Fact]
    public void Constructor_WithNullProjectParser_ThrowsArgumentNullException()
    {
        // Arrange
        var solutionParser = new Mock<ISolutionParser>().Object;
        var packagesConfigParser = new Mock<IPackagesConfigParser>().Object;
        var sdkProjectConverter = new Mock<ISdkProjectConverter>().Object;
        var assemblyInfoExtractor = new Mock<IAssemblyInfoExtractor>().Object;
        var packageReferenceConverter = new Mock<IPackageReferenceConverter>().Object;
        var packageMappingService = new Mock<IPackageMappingService>().Object;
        var dryRunService = new Mock<IDryRunService>().Object;
        var orchestrator = new Mock<IMigrationOrchestrator>().Object;

        // Act & Assert
        var act = () => new MigrateCommand(
            solutionParser,
            null!,
            packagesConfigParser,
            sdkProjectConverter,
            assemblyInfoExtractor,
            packageReferenceConverter,
            packageMappingService,
            dryRunService,
            orchestrator);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("projectParser");
    }

    [Fact]
    public void Constructor_WithNullOrchestrator_ThrowsArgumentNullException()
    {
        // Arrange
        var solutionParser = new Mock<ISolutionParser>().Object;
        var projectParser = new Mock<IProjectParser>().Object;
        var packagesConfigParser = new Mock<IPackagesConfigParser>().Object;
        var sdkProjectConverter = new Mock<ISdkProjectConverter>().Object;
        var assemblyInfoExtractor = new Mock<IAssemblyInfoExtractor>().Object;
        var packageReferenceConverter = new Mock<IPackageReferenceConverter>().Object;
        var packageMappingService = new Mock<IPackageMappingService>().Object;
        var dryRunService = new Mock<IDryRunService>().Object;

        // Act & Assert
        var act = () => new MigrateCommand(
            solutionParser,
            projectParser,
            packagesConfigParser,
            sdkProjectConverter,
            assemblyInfoExtractor,
            packageReferenceConverter,
            packageMappingService,
            dryRunService,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("orchestrator");
    }

    [Fact]
    public void Constructor_WithAllValidArguments_CreatesInstance()
    {
        // Arrange
        var solutionParser = new Mock<ISolutionParser>().Object;
        var projectParser = new Mock<IProjectParser>().Object;
        var packagesConfigParser = new Mock<IPackagesConfigParser>().Object;
        var sdkProjectConverter = new Mock<ISdkProjectConverter>().Object;
        var assemblyInfoExtractor = new Mock<IAssemblyInfoExtractor>().Object;
        var packageReferenceConverter = new Mock<IPackageReferenceConverter>().Object;
        var packageMappingService = new Mock<IPackageMappingService>().Object;
        var dryRunService = new Mock<IDryRunService>().Object;
        var orchestrator = new Mock<IMigrationOrchestrator>().Object;

        // Act
        var command = new MigrateCommand(
            solutionParser,
            projectParser,
            packagesConfigParser,
            sdkProjectConverter,
            assemblyInfoExtractor,
            packageReferenceConverter,
            packageMappingService,
            dryRunService,
            orchestrator);

        // Assert
        command.Should().NotBeNull();
    }
}
