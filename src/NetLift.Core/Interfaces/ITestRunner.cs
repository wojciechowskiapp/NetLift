namespace NetLift.Core.Interfaces;

using NetLift.Core.Models;

/// <summary>
/// Interface for running tests on .NET projects and solutions.
/// </summary>
public interface ITestRunner
{
    /// <summary>
    /// Runs tests for a solution or project using dotnet test.
    /// </summary>
    /// <param name="solutionOrProjectPath">The absolute path to the solution (.sln) or project (.csproj) file.</param>
    /// <param name="options">Optional configuration for test execution.</param>
    /// <param name="cancellationToken">Cancellation token to abort the test run.</param>
    /// <returns>A task that completes with the test execution results.</returns>
    Task<TestResult> RunTestsAsync(
        string solutionOrProjectPath,
        TestRunnerOptions? options = null,
        CancellationToken cancellationToken = default);
}
