namespace NetLift.Core.Errors;

/// <summary>
/// Categorizes errors by their source to enable better recovery suggestions.
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Error occurred during solution/project analysis phase.
    /// </summary>
    Analysis,

    /// <summary>
    /// Error occurred during code transformation/migration.
    /// </summary>
    Transformation,

    /// <summary>
    /// Error occurred during build/compilation validation.
    /// </summary>
    Compilation,

    /// <summary>
    /// Error occurred during file system operations.
    /// </summary>
    FileSystem,

    /// <summary>
    /// Error occurred due to invalid or missing configuration.
    /// </summary>
    Configuration,

    /// <summary>
    /// Error occurred during post-migration validation.
    /// </summary>
    Validation,

    /// <summary>
    /// Error occurred in external tools or dependencies.
    /// </summary>
    External
}

/// <summary>
/// Represents a migration error with detailed context and recovery suggestions.
/// </summary>
public sealed record MigrationError
{
    /// <summary>
    /// Unique error code (e.g., "NETLIFT001").
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Category of the error for recovery suggestion lookup.
    /// </summary>
    public required ErrorCategory Category { get; init; }

    /// <summary>
    /// File path where the error occurred, if applicable.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Line number where the error occurred, if applicable.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Column number where the error occurred, if applicable.
    /// </summary>
    public int? Column { get; init; }

    /// <summary>
    /// Stack trace of the error, if available.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// List of suggested actions to recover from this error.
    /// </summary>
    public IReadOnlyList<string> RecoverySuggestions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The underlying exception that caused this error, if any.
    /// </summary>
    public Exception? InnerException { get; init; }
}
