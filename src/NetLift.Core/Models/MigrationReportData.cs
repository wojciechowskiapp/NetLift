namespace NetLift.Core.Models;

/// <summary>
/// Represents comprehensive migration report data for HTML generation.
/// </summary>
public sealed record MigrationReportData
{
    /// <summary>
    /// Gets the name of the solution that was migrated.
    /// </summary>
    public required string SolutionName { get; init; }

    /// <summary>
    /// Gets the target framework (e.g., "net8.0", "net9.0").
    /// </summary>
    public required string TargetFramework { get; init; }

    /// <summary>
    /// Gets the total number of projects migrated.
    /// </summary>
    public int ProjectCount { get; init; }

    /// <summary>
    /// Gets the total number of files transformed during migration.
    /// </summary>
    public int FilesTransformed { get; init; }

    /// <summary>
    /// Gets the build validation result, if available.
    /// </summary>
    public BuildResult? BuildResult { get; init; }

    /// <summary>
    /// Gets the test execution result, if available.
    /// </summary>
    public TestResult? TestResult { get; init; }

    /// <summary>
    /// Gets the migration confidence score, if calculated.
    /// </summary>
    public ConfidenceScore? ConfidenceScore { get; init; }

    /// <summary>
    /// Gets the list of migration issues encountered.
    /// </summary>
    public IReadOnlyList<MigrationIssue> Issues { get; init; } = [];

    /// <summary>
    /// Gets the timestamp when the report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; }

    /// <summary>
    /// Gets the version of NetLift used for migration.
    /// </summary>
    public string NetLiftVersion { get; init; } = "1.0.0";
}

/// <summary>
/// Represents an issue encountered during migration.
/// </summary>
public sealed record MigrationIssue
{
    /// <summary>
    /// Gets the issue code (e.g., "MVC001", "EF001").
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the issue message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the file path where the issue occurred, if applicable.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets the severity of the issue.
    /// </summary>
    public IssueSeverity Severity { get; init; }
}
