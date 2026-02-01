namespace NetLift.Core.Models;

/// <summary>
/// Represents the result of a dotnet build validation.
/// </summary>
public sealed record BuildResult
{
    /// <summary>
    /// Gets whether the build was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the exit code from the build process.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Gets the duration of the build.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the list of build errors.
    /// </summary>
    public IReadOnlyList<BuildDiagnostic> Errors { get; init; } = [];

    /// <summary>
    /// Gets the list of build warnings.
    /// </summary>
    public IReadOnlyList<BuildDiagnostic> Warnings { get; init; } = [];

    /// <summary>
    /// Gets the raw output from the build process.
    /// </summary>
    public string RawOutput { get; init; } = "";
}

/// <summary>
/// Represents a build diagnostic (error or warning).
/// </summary>
public sealed record BuildDiagnostic
{
    /// <summary>
    /// Gets the diagnostic code (e.g., CS0103, MSB3644).
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the file path where the diagnostic occurred.
    /// </summary>
    public string File { get; init; } = "";

    /// <summary>
    /// Gets the line number where the diagnostic occurred.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// Gets the column number where the diagnostic occurred.
    /// </summary>
    public int Column { get; init; }

    /// <summary>
    /// Gets the severity level of the diagnostic.
    /// </summary>
    public DiagnosticSeverity Severity { get; init; }
}

/// <summary>
/// Represents the severity of a build diagnostic.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Error level diagnostic.
    /// </summary>
    Error,

    /// <summary>
    /// Warning level diagnostic.
    /// </summary>
    Warning,

    /// <summary>
    /// Info level diagnostic.
    /// </summary>
    Info
}
