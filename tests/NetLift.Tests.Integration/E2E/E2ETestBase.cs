namespace NetLift.Tests.Integration.E2E;

/// <summary>
/// Base class for end-to-end tests that require a temporary working directory.
/// Automatically copies fixtures and provides cleanup.
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
    /// <summary>
    /// Gets the temporary working directory for this test instance.
    /// </summary>
    protected string WorkingDirectory { get; private set; } = "";

    /// <summary>
    /// Gets the path to the mvc5-basic fixture directory.
    /// </summary>
    protected string FixturePath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2ETestBase"/> class.
    /// </summary>
    protected E2ETestBase()
    {
        FixturePath = Path.Combine(
            GetSolutionDirectory(),
            "tests", "fixtures", "mvc5-basic");
    }

    /// <summary>
    /// Initializes the test by creating a temporary working directory.
    /// Override this method to control fixture copying behavior.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        WorkingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"netlift-e2e-{Guid.NewGuid():N}");

        Directory.CreateDirectory(WorkingDirectory);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Copies the mvc5-basic fixture to the working directory.
    /// Call this explicitly if needed in your test.
    /// </summary>
    protected void CopyFixtureToWorkingDirectory()
    {
        if (Directory.Exists(FixturePath))
        {
            CopyDirectory(FixturePath, WorkingDirectory);
        }
    }

    /// <summary>
    /// Cleans up the temporary working directory after the test completes.
    /// </summary>
    public virtual Task DisposeAsync()
    {
        if (Directory.Exists(WorkingDirectory))
        {
            try
            {
                Directory.Delete(WorkingDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures - they shouldn't fail tests
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds the solution directory by walking up from the current directory.
    /// </summary>
    /// <returns>The solution directory path.</returns>
    protected static string GetSolutionDirectory()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !File.Exists(Path.Combine(dir, "NetLift.sln")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Recursively copies a directory and all its contents.
    /// </summary>
    /// <param name="source">Source directory path.</param>
    /// <param name="destination">Destination directory path.</param>
    protected static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            var fileName = Path.GetFileName(file);
            File.Copy(file, Path.Combine(destination, fileName));
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            var dirName = Path.GetFileName(dir);
            CopyDirectory(dir, Path.Combine(destination, dirName));
        }
    }

    /// <summary>
    /// Creates a simple .NET 8 project file for testing purposes.
    /// </summary>
    /// <param name="projectPath">The path where the project file should be created.</param>
    /// <param name="targetFramework">The target framework (default: net8.0).</param>
    /// <param name="additionalContent">Additional XML content to include in the project file.</param>
    /// <param name="createProgramCs">Whether to create a simple Program.cs file (default: true).</param>
    protected static async Task CreateTestProjectAsync(
        string projectPath,
        string targetFramework = "net8.0",
        string additionalContent = "",
        bool createProgramCs = true)
    {
        var projectContent = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{targetFramework}</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              {additionalContent}
            </Project>
            """;

        await File.WriteAllTextAsync(projectPath, projectContent);

        // Create a simple Program.cs if requested
        if (createProgramCs)
        {
            var projectDir = Path.GetDirectoryName(projectPath) ?? throw new InvalidOperationException("Invalid project path");
            var programPath = Path.Combine(projectDir, "Program.cs");
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
        }
    }
}
