namespace NetLift.Tests.Integration.E2E;

using FluentAssertions;
using NetLift.Validation;

/// <summary>
/// End-to-end tests for build validation functionality.
/// </summary>
[Collection("E2E")]
public class BuildValidationE2ETests : E2ETestBase
{
    [Fact]
    public async Task BuildValidator_WithValidProject_Succeeds()
    {
        // Arrange
        var projectPath = Path.Combine(WorkingDirectory, "test.csproj");
        await CreateTestProjectAsync(projectPath);

        var validator = new BuildValidator();

        // Act
        var result = await validator.ValidateAsync(projectPath);

        // Assert
        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildValidator_WithInvalidProject_ReturnsErrors()
    {
        // Arrange
        var projectPath = Path.Combine(WorkingDirectory, "invalid.csproj");
        await CreateTestProjectAsync(projectPath, createProgramCs: false);

        // Create a C# file with compilation errors
        var csFilePath = Path.Combine(WorkingDirectory, "Program.cs");
        await File.WriteAllTextAsync(csFilePath, """
            namespace Test;

            public class InvalidClass
            {
                public void Method()
                {
                    UndefinedType variable; // This should cause a compilation error
                }
            }
            """);

        var validator = new BuildValidator();

        // Act
        var result = await validator.ValidateAsync(projectPath);

        // Assert
        result.Success.Should().BeFalse();
        result.ExitCode.Should().NotBe(0);
        result.Errors.Should().NotBeEmpty();
        result.RawOutput.Should().Contain("error");
    }

    [Fact]
    public async Task BuildValidator_WithWarnings_SucceedsWithWarnings()
    {
        // Arrange
        var projectPath = Path.Combine(WorkingDirectory, "warnings.csproj");
        await CreateTestProjectAsync(projectPath, createProgramCs: false);

        // Create a C# file that generates warnings (unused variable)
        var csFilePath = Path.Combine(WorkingDirectory, "Program.cs");
        await File.WriteAllTextAsync(csFilePath, """
            using System;

            namespace Test;

            public class Program
            {
                public static void Main(string[] args)
                {
                    int unusedVariable = 42; // CS0219: The variable 'unusedVariable' is assigned but never used
                    Console.WriteLine("Hello, World!");
                }
            }
            """);

        var validator = new BuildValidator();

        // Act
        var result = await validator.ValidateAsync(projectPath);

        // Assert
        result.Success.Should().BeTrue("warnings should not fail the build");
        result.ExitCode.Should().Be(0);
        result.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BuildValidator_WithNonExistentProject_ThrowsFileNotFoundException()
    {
        // Arrange
        var projectPath = Path.Combine(WorkingDirectory, "nonexistent.csproj");
        var validator = new BuildValidator();

        // Act
        var act = async () => await validator.ValidateAsync(projectPath);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task BuildValidator_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var validator = new BuildValidator();

        // Act
        var act = async () => await validator.ValidateAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task BuildValidator_TracksDuration()
    {
        // Arrange
        var projectPath = Path.Combine(WorkingDirectory, "test.csproj");
        await CreateTestProjectAsync(projectPath);

        var validator = new BuildValidator();

        // Act
        var result = await validator.ValidateAsync(projectPath);

        // Assert
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        result.Duration.Should().BeLessThan(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task BuildValidator_WithMultipleTargetFrameworks_Succeeds()
    {
        // Arrange
        var projectPath = Path.Combine(WorkingDirectory, "multitarget.csproj");

        // Create multi-target project
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);

        // Create Program.cs
        var programPath = Path.Combine(WorkingDirectory, "Program.cs");
        await File.WriteAllTextAsync(programPath, """
            using System;

            namespace TestProject;

            public class Program
            {
                public static void Main(string[] args)
                {
                    Console.WriteLine("Hello, World!");
                }
            }
            """);

        var validator = new BuildValidator();

        // Act
        var result = await validator.ValidateAsync(projectPath);

        // Assert
        result.Success.Should().BeTrue();
        result.RawOutput.Should().Contain("net8.0");
    }
}
