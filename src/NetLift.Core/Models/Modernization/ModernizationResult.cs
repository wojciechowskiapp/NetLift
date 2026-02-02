namespace NetLift.Core.Models.Modernization;

/// <summary>
/// Represents the result of a modernization operation.
/// </summary>
public sealed record ModernizationResult
{
    /// <summary>
    /// Gets whether the modernization was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the list of newly generated files.
    /// </summary>
    public IReadOnlyList<GeneratedFileInfo> GeneratedFiles { get; init; } = [];

    /// <summary>
    /// Gets the list of existing files that were modified.
    /// </summary>
    public IReadOnlyList<ModifiedFileInfo> ModifiedFiles { get; init; } = [];

    /// <summary>
    /// Gets the list of diagnostics (warnings and errors) generated during modernization.
    /// </summary>
    public IReadOnlyList<ModernizationDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets the overall confidence score of the modernization.
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets the duration of the modernization operation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the summary of applied patterns.
    /// </summary>
    public IReadOnlyDictionary<ModernizationPattern, int> AppliedPatterns { get; init; } = new Dictionary<ModernizationPattern, int>();
}

/// <summary>
/// Represents information about a newly generated file.
/// </summary>
public sealed record GeneratedFileInfo
{
    /// <summary>
    /// Gets the absolute path to the generated file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the type of file that was generated (e.g., "Command", "Query", "Validator").
    /// </summary>
    public required string FileType { get; init; }

    /// <summary>
    /// Gets the confidence score for this file generation (0-100).
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets the source file or element that triggered this generation.
    /// </summary>
    public string? SourceReference { get; init; }
}

/// <summary>
/// Represents information about a modified file.
/// </summary>
public sealed record ModifiedFileInfo
{
    /// <summary>
    /// Gets the absolute path to the modified file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the list of changes made to the file.
    /// </summary>
    public IReadOnlyList<string> Changes { get; init; } = [];

    /// <summary>
    /// Gets the confidence score for these modifications (0-100).
    /// </summary>
    public int Confidence { get; init; }
}

/// <summary>
/// Represents a diagnostic message generated during modernization.
/// </summary>
public sealed record ModernizationDiagnostic
{
    /// <summary>
    /// Gets the severity of the diagnostic.
    /// </summary>
    public DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the file path related to this diagnostic, if applicable.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets the line number related to this diagnostic, if applicable.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Gets the diagnostic code (e.g., "MOD001").
    /// </summary>
    public string? Code { get; init; }
}

/// <summary>
/// Represents the severity of a diagnostic message.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Warning that doesn't prevent modernization.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that prevents successful modernization.
    /// </summary>
    Error
}
