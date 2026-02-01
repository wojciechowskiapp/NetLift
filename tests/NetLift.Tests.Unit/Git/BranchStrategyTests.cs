using FluentAssertions;
using Moq;
using NetLift.Core.Exceptions;
using NetLift.Core.Interfaces;
using NetLift.Core.Models;
using NetLift.Git;
using Xunit;

namespace NetLift.Tests.Unit.Git;

public class BranchStrategyTests
{
    private readonly Mock<IGitOperations> _gitOperationsMock;
    private readonly BranchStrategy _sut;

    public BranchStrategyTests()
    {
        _gitOperationsMock = new Mock<IGitOperations>();
        _sut = new BranchStrategy(_gitOperationsMock.Object);
    }

    [Fact]
    public void Constructor_WhenGitOperationsIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new BranchStrategy(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gitOperations");
    }

    [Theory]
    [InlineData(MigrationPhaseType.ProjectFiles, "netlift/project-files")]
    [InlineData(MigrationPhaseType.Configuration, "netlift/configuration")]
    [InlineData(MigrationPhaseType.Controllers, "netlift/controllers")]
    [InlineData(MigrationPhaseType.EntityFramework, "netlift/entity-framework")]
    [InlineData(MigrationPhaseType.Wcf, "netlift/wcf")]
    [InlineData(MigrationPhaseType.Validation, "netlift/validation")]
    public void GetBranchName_ReturnsCorrectBranchName(MigrationPhaseType phase, string expectedBranchName)
    {
        // Act
        var result = _sut.GetBranchName(phase);

        // Assert
        result.Should().Be(expectedBranchName);
    }

    [Fact]
    public void GetBranchName_WhenPhaseIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidPhase = (MigrationPhaseType)999;

        // Act
        var act = () => _sut.GetBranchName(invalidPhase);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("phase");
    }

    [Fact]
    public async Task CreatePhaseBranchAsync_WhenRepositoryPathIsNull_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.CreatePhaseBranchAsync(null!, MigrationPhaseType.ProjectFiles);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("repoPath");
    }

    [Fact]
    public async Task CreatePhaseBranchAsync_WhenRepositoryPathIsEmpty_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.CreatePhaseBranchAsync(string.Empty, MigrationPhaseType.ProjectFiles);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("repoPath");
    }

    [Fact]
    public async Task CreatePhaseBranchAsync_WhenNotAGitRepository_ThrowsNotAGitRepositoryException()
    {
        // Arrange
        var repoPath = @"C:\not-a-repo";
        _gitOperationsMock.Setup(x => x.IsGitRepository(repoPath)).Returns(false);

        // Act
        var act = async () => await _sut.CreatePhaseBranchAsync(repoPath, MigrationPhaseType.ProjectFiles);

        // Assert
        await act.Should().ThrowAsync<NotAGitRepositoryException>();
    }

    [Fact]
    public async Task CreatePhaseBranchAsync_WhenRepositoryHasUncommittedChanges_ThrowsDirtyRepositoryException()
    {
        // Arrange
        var repoPath = @"C:\my-repo";
        var modifiedFiles = new List<string> { "file1.cs", "file2.cs" };

        _gitOperationsMock.Setup(x => x.IsGitRepository(repoPath)).Returns(true);
        _gitOperationsMock.Setup(x => x.HasUncommittedChangesAsync(repoPath)).ReturnsAsync(true);
        _gitOperationsMock.Setup(x => x.GetModifiedFiles(repoPath)).Returns(modifiedFiles);

        // Act
        var act = async () => await _sut.CreatePhaseBranchAsync(repoPath, MigrationPhaseType.ProjectFiles);

        // Assert
        var exception = await act.Should().ThrowAsync<DirtyRepositoryException>();
        exception.Which.RepositoryPath.Should().Be(repoPath);
        exception.Which.ModifiedFiles.Should().BeEquivalentTo(modifiedFiles);
    }

    [Fact]
    public async Task CreatePhaseBranchAsync_WhenBranchAlreadyExists_ThrowsBranchAlreadyExistsException()
    {
        // Arrange
        var repoPath = @"C:\my-repo";
        var phase = MigrationPhaseType.ProjectFiles;
        var expectedBranchName = "netlift/project-files";

        _gitOperationsMock.Setup(x => x.IsGitRepository(repoPath)).Returns(true);
        _gitOperationsMock.Setup(x => x.HasUncommittedChangesAsync(repoPath)).ReturnsAsync(false);
        _gitOperationsMock.Setup(x => x.BranchExists(repoPath, expectedBranchName)).Returns(true);

        // Act
        var act = async () => await _sut.CreatePhaseBranchAsync(repoPath, phase);

        // Assert
        var exception = await act.Should().ThrowAsync<BranchAlreadyExistsException>();
        exception.Which.BranchName.Should().Be(expectedBranchName);
        _gitOperationsMock.Verify(x => x.CreateBranchAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreatePhaseBranchAsync_WhenRepositoryIsClean_CreatesBranchSuccessfully()
    {
        // Arrange
        var repoPath = @"C:\my-repo";
        var phase = MigrationPhaseType.Configuration;
        var expectedBranchName = "netlift/configuration";

        _gitOperationsMock.Setup(x => x.IsGitRepository(repoPath)).Returns(true);
        _gitOperationsMock.Setup(x => x.HasUncommittedChangesAsync(repoPath)).ReturnsAsync(false);
        _gitOperationsMock.Setup(x => x.BranchExists(repoPath, expectedBranchName)).Returns(false);
        _gitOperationsMock.Setup(x => x.CreateBranchAsync(repoPath, expectedBranchName))
            .ReturnsAsync(expectedBranchName);

        // Act
        var result = await _sut.CreatePhaseBranchAsync(repoPath, phase);

        // Assert
        result.Should().Be(expectedBranchName);
        _gitOperationsMock.Verify(x => x.BranchExists(repoPath, expectedBranchName), Times.Once);
        _gitOperationsMock.Verify(x => x.CreateBranchAsync(repoPath, expectedBranchName), Times.Once);
    }

    [Theory]
    [InlineData(MigrationPhaseType.ProjectFiles)]
    [InlineData(MigrationPhaseType.Configuration)]
    [InlineData(MigrationPhaseType.Controllers)]
    [InlineData(MigrationPhaseType.EntityFramework)]
    [InlineData(MigrationPhaseType.Wcf)]
    [InlineData(MigrationPhaseType.Validation)]
    public async Task CreatePhaseBranchAsync_ForEachPhaseType_CreatesCorrectBranch(MigrationPhaseType phase)
    {
        // Arrange
        var repoPath = @"C:\my-repo";
        var expectedBranchName = _sut.GetBranchName(phase);

        _gitOperationsMock.Setup(x => x.IsGitRepository(repoPath)).Returns(true);
        _gitOperationsMock.Setup(x => x.HasUncommittedChangesAsync(repoPath)).ReturnsAsync(false);
        _gitOperationsMock.Setup(x => x.BranchExists(repoPath, expectedBranchName)).Returns(false);
        _gitOperationsMock.Setup(x => x.CreateBranchAsync(repoPath, expectedBranchName))
            .ReturnsAsync(expectedBranchName);

        // Act
        var result = await _sut.CreatePhaseBranchAsync(repoPath, phase);

        // Assert
        result.Should().Be(expectedBranchName);
    }

    [Fact]
    public async Task ValidateRepositoryCleanAsync_WhenRepositoryPathIsNull_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.ValidateRepositoryCleanAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("repoPath");
    }

    [Fact]
    public async Task ValidateRepositoryCleanAsync_WhenRepositoryPathIsEmpty_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.ValidateRepositoryCleanAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("repoPath");
    }

    [Fact]
    public async Task ValidateRepositoryCleanAsync_WhenNotAGitRepository_ThrowsNotAGitRepositoryException()
    {
        // Arrange
        var repoPath = @"C:\not-a-repo";
        _gitOperationsMock.Setup(x => x.IsGitRepository(repoPath)).Returns(false);

        // Act
        var act = async () => await _sut.ValidateRepositoryCleanAsync(repoPath);

        // Assert
        await act.Should().ThrowAsync<NotAGitRepositoryException>();
    }

    [Fact]
    public async Task ValidateRepositoryCleanAsync_WhenRepositoryHasUncommittedChanges_ReturnsFalse()
    {
        // Arrange
        var repoPath = @"C:\my-repo";
        _gitOperationsMock.Setup(x => x.IsGitRepository(repoPath)).Returns(true);
        _gitOperationsMock.Setup(x => x.HasUncommittedChangesAsync(repoPath)).ReturnsAsync(true);

        // Act
        var result = await _sut.ValidateRepositoryCleanAsync(repoPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateRepositoryCleanAsync_WhenRepositoryIsClean_ReturnsTrue()
    {
        // Arrange
        var repoPath = @"C:\my-repo";
        _gitOperationsMock.Setup(x => x.IsGitRepository(repoPath)).Returns(true);
        _gitOperationsMock.Setup(x => x.HasUncommittedChangesAsync(repoPath)).ReturnsAsync(false);

        // Act
        var result = await _sut.ValidateRepositoryCleanAsync(repoPath);

        // Assert
        result.Should().BeTrue();
    }
}
