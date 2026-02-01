using FluentAssertions;
using NetLift.Analysis.Parsers;
using NetLift.Core.Models;

namespace NetLift.Tests.Unit.Analysis.Parsers;

public class SolutionParserTests
{
    private readonly SolutionParser _parser;
    private readonly string _testFixturesPath;

    public SolutionParserTests()
    {
        _parser = new SolutionParser();

        // Get the test fixtures path relative to the test assembly
        var assemblyLocation = Path.GetDirectoryName(typeof(SolutionParserTests).Assembly.Location);
        _testFixturesPath = Path.GetFullPath(
            Path.Combine(assemblyLocation!, "..", "..", "..", "..", "..", "tests", "test-fixtures"));
    }

    [Fact]
    public async Task ParseAsync_WithValidSolution_ReturnsSolutionInfo()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "mvc5-basic", "Mvc5Basic.sln");

        // Act
        var result = await _parser.ParseAsync(solutionPath);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Mvc5Basic");
        result.FilePath.Should().Be(Path.GetFullPath(solutionPath));
    }

    [Fact]
    public async Task ParseAsync_WithValidSolution_ParsesFormatVersion()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "mvc5-basic", "Mvc5Basic.sln");

        // Act
        var result = await _parser.ParseAsync(solutionPath);

        // Assert
        result.FormatVersion.Should().Be("12.00");
    }

    [Fact]
    public async Task ParseAsync_WithValidSolution_ParsesVisualStudioVersion()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "mvc5-basic", "Mvc5Basic.sln");

        // Act
        var result = await _parser.ParseAsync(solutionPath);

        // Assert
        result.VisualStudioVersion.Should().Be("17.0.31903.59");
    }

    [Fact]
    public async Task ParseAsync_WithValidSolution_ParsesProjects()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "mvc5-basic", "Mvc5Basic.sln");

        // Act
        var result = await _parser.ParseAsync(solutionPath);

        // Assert
        result.Projects.Should().NotBeEmpty();
        result.Projects.Should().HaveCount(1);

        var project = result.Projects.First();
        project.Name.Should().Be("Mvc5Basic");
        project.ProjectGuid.Should().Be(new Guid("A8B4D6F2-3C9E-4A1B-8D5F-2E7C4B9A1F3D"));
        project.TypeGuid.Should().Be(ProjectTypeGuids.CSharp);
        project.RelativePath.Should().Be(@"Mvc5Basic\Mvc5Basic.csproj");
    }

    [Fact]
    public async Task ParseAsync_WithValidSolution_ParsesProjectAbsolutePath()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "mvc5-basic", "Mvc5Basic.sln");

        // Act
        var result = await _parser.ParseAsync(solutionPath);

        // Assert
        var project = result.Projects.First();
        project.AbsolutePath.Should().NotBeNullOrEmpty();
        project.AbsolutePath.Should().EndWith("Mvc5Basic.csproj");
        Path.IsPathFullyQualified(project.AbsolutePath).Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_WithValidSolution_DetectsProjectType()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "mvc5-basic", "Mvc5Basic.sln");

        // Act
        var result = await _parser.ParseAsync(solutionPath);

        // Assert
        var project = result.Projects.First();
        project.DetectedType.Should().Be(ProjectType.CSharpClassLibrary);
    }

    [Fact]
    public async Task ParseAsync_WithValidSolution_ParsesConfigurations()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "mvc5-basic", "Mvc5Basic.sln");

        // Act
        var result = await _parser.ParseAsync(solutionPath);

        // Assert
        result.Configurations.Should().NotBeEmpty();
        result.Configurations.Should().HaveCount(2);

        result.Configurations.Should().Contain(c => c.Name == "Debug" && c.Platform == "Any CPU");
        result.Configurations.Should().Contain(c => c.Name == "Release" && c.Platform == "Any CPU");
    }

    [Fact]
    public async Task ParseAsync_WithValidSolution_SetsConfigurationFullName()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "mvc5-basic", "Mvc5Basic.sln");

        // Act
        var result = await _parser.ParseAsync(solutionPath);

        // Assert
        var debugConfig = result.Configurations.First(c => c.Name == "Debug");
        debugConfig.FullName.Should().Be("Debug|Any CPU");

        var releaseConfig = result.Configurations.First(c => c.Name == "Release");
        releaseConfig.FullName.Should().Be("Release|Any CPU");
    }

    [Fact]
    public async Task ParseAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "non-existent.sln");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _parser.ParseAsync(solutionPath));
    }

    [Fact]
    public async Task ParseAsync_WithEmptyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, string.Empty);

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _parser.ParseAsync(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void IsValidSolutionFile_WithValidSolutionFile_ReturnsTrue()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "mvc5-basic", "Mvc5Basic.sln");

        // Act
        var result = _parser.IsValidSolutionFile(solutionPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidSolutionFile_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "non-existent.sln");

        // Act
        var result = _parser.IsValidSolutionFile(solutionPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidSolutionFile_WithNonSlnFile_ReturnsFalse()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "mvc5-basic", "Mvc5Basic", "Mvc5Basic.csproj");

        // Act
        var result = _parser.IsValidSolutionFile(solutionPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidSolutionFile_WithInvalidContent_ReturnsFalse()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.sln");
        File.WriteAllText(tempFile, "This is not a valid solution file");

        try
        {
            // Act
            var result = _parser.IsValidSolutionFile(tempFile);

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseAsync_SolutionDirectory_ReturnsCorrectDirectory()
    {
        // Arrange
        var solutionPath = Path.Combine(_testFixturesPath, "mvc5-basic", "Mvc5Basic.sln");

        // Act
        var result = await _parser.ParseAsync(solutionPath);

        // Assert
        result.Directory.Should().Be(Path.GetDirectoryName(result.FilePath));
        result.Directory.Should().EndWith("mvc5-basic");
    }

    [Fact]
    public void ProjectTypeGuids_CSharp_MatchesExpectedGuid()
    {
        // Assert
        ProjectTypeGuids.CSharp.Should().Be(new Guid("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC"));
    }

    [Fact]
    public void ProjectTypeGuids_SolutionFolder_MatchesExpectedGuid()
    {
        // Assert
        ProjectTypeGuids.SolutionFolder.Should().Be(new Guid("2150E333-8FDC-42A3-9474-1A3956D46DE8"));
    }

    [Fact]
    public void ProjectTypeGuids_Web_MatchesExpectedGuid()
    {
        // Assert
        ProjectTypeGuids.Web.Should().Be(new Guid("349C5851-65DF-11DA-9384-00065B846F21"));
    }

    [Fact]
    public void ProjectTypeGuids_GetProjectType_WithCSharpGuid_ReturnsCSharpClassLibrary()
    {
        // Act
        var result = ProjectTypeGuids.GetProjectType(ProjectTypeGuids.CSharp);

        // Assert
        result.Should().Be(ProjectType.CSharpClassLibrary);
    }

    [Fact]
    public void ProjectTypeGuids_GetProjectType_WithSolutionFolderGuid_ReturnsSolutionFolder()
    {
        // Act
        var result = ProjectTypeGuids.GetProjectType(ProjectTypeGuids.SolutionFolder);

        // Assert
        result.Should().Be(ProjectType.SolutionFolder);
    }

    [Fact]
    public void ProjectTypeGuids_GetProjectType_WithWebGuid_ReturnsCSharpWeb()
    {
        // Act
        var result = ProjectTypeGuids.GetProjectType(ProjectTypeGuids.Web);

        // Assert
        result.Should().Be(ProjectType.CSharpWeb);
    }

    [Fact]
    public void ProjectTypeGuids_GetProjectType_WithUnknownGuid_ReturnsUnknown()
    {
        // Act
        var result = ProjectTypeGuids.GetProjectType(Guid.NewGuid());

        // Assert
        result.Should().Be(ProjectType.Unknown);
    }
}
