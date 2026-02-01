namespace NetLift.Core.Models;

/// <summary>
/// Options for configuring test execution.
/// </summary>
public sealed record TestRunnerOptions
{
    /// <summary>
    /// Gets the timeout for test execution in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 600;

    /// <summary>
    /// Gets whether to stop on the first test failure.
    /// </summary>
    public bool StopOnFirstFailure { get; init; }

    /// <summary>
    /// Gets the filter expression for selecting tests to run.
    /// </summary>
    public string? Filter { get; init; }

    /// <summary>
    /// Gets the build configuration to use (Debug or Release).
    /// </summary>
    public string Configuration { get; init; } = "Debug";
}

/// <summary>
/// Represents the result of running tests via dotnet test.
/// </summary>
public sealed record TestResult
{
    /// <summary>
    /// Gets whether all tests passed.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the exit code from the test process.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Gets the duration of the test run.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the total number of tests executed.
    /// </summary>
    public int TotalTests { get; init; }

    /// <summary>
    /// Gets the number of tests that passed.
    /// </summary>
    public int PassedTests { get; init; }

    /// <summary>
    /// Gets the number of tests that failed.
    /// </summary>
    public int FailedTests { get; init; }

    /// <summary>
    /// Gets the number of tests that were skipped.
    /// </summary>
    public int SkippedTests { get; init; }

    /// <summary>
    /// Gets the list of test failures with details.
    /// </summary>
    public IReadOnlyList<TestFailure> Failures { get; init; } = [];

    /// <summary>
    /// Gets the raw output from the test process.
    /// </summary>
    public string RawOutput { get; init; } = "";
}

/// <summary>
/// Represents a failed test with diagnostic information.
/// </summary>
public sealed record TestFailure
{
    /// <summary>
    /// Gets the fully qualified name of the test method.
    /// </summary>
    public required string TestName { get; init; }

    /// <summary>
    /// Gets the class name containing the test.
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// Gets the error message from the test failure.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Gets the stack trace from the test failure.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Gets the duration of the failed test.
    /// </summary>
    public TimeSpan Duration { get; init; }
}
