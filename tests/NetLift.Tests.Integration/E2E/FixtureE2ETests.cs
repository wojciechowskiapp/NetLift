namespace NetLift.Tests.Integration.E2E;

using FluentAssertions;

/// <summary>
/// Example E2E tests demonstrating fixture usage.
/// </summary>
[Collection("E2E")]
public class FixtureE2ETests : E2ETestBase
{
    [Fact]
    public void Fixture_MustExist()
    {
        // Verify that the mvc5-basic fixture exists
        Directory.Exists(FixturePath).Should().BeTrue("the mvc5-basic fixture is required for E2E tests");

        var solutionPath = Path.Combine(FixturePath, "Mvc5Basic.sln");
        File.Exists(solutionPath).Should().BeTrue("the solution file should exist in the fixture");
    }

    [Fact]
    public void WorkingDirectory_IsIsolated()
    {
        // Verify each test gets its own isolated working directory
        WorkingDirectory.Should().NotBeNullOrEmpty();
        Directory.Exists(WorkingDirectory).Should().BeTrue();

        // Verify it's a temp directory
        WorkingDirectory.Should().StartWith(Path.GetTempPath());
        WorkingDirectory.Should().Contain("netlift-e2e-");
    }

    [Fact]
    public void CopyFixture_Works()
    {
        // Copy the fixture to working directory
        CopyFixtureToWorkingDirectory();

        // Verify the fixture was copied
        var copiedSolutionPath = Path.Combine(WorkingDirectory, "Mvc5Basic.sln");
        File.Exists(copiedSolutionPath).Should().BeTrue("the solution file should be copied");

        var copiedProjectPath = Path.Combine(WorkingDirectory, "Mvc5Basic", "Mvc5Basic.csproj");
        File.Exists(copiedProjectPath).Should().BeTrue("the project file should be copied");
    }
}
