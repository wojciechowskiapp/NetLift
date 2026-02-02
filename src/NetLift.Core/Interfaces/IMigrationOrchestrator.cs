using NetLift.Core.Models;

namespace NetLift.Core.Interfaces;

/// <summary>
/// Orchestrates the complete migration process for a .NET project.
/// Coordinates all transformation phases, aggregates results, and manages the migration workflow.
/// </summary>
public interface IMigrationOrchestrator
{
    /// <summary>
    /// Migrates a .NET Framework project to .NET 8+ with comprehensive transformation.
    /// Uses pre-parsed project information to avoid re-parsing after project file conversion.
    /// </summary>
    /// <param name="projectInfo">The pre-parsed project information containing CompileItems.</param>
    /// <param name="targetFramework">The target framework (e.g., "net8.0", "net9.0").</param>
    /// <param name="options">Migration options controlling the transformation scope.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A comprehensive migration result with all changes and diagnostics.</returns>
    Task<MigrationResult> MigrateProjectAsync(
        ProjectInfo projectInfo,
        string targetFramework,
        MigrationOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration options for the migration process.
/// Controls which transformation phases are executed and how.
/// </summary>
public record MigrationOptions
{
    /// <summary>
    /// Gets whether this is a dry-run (preview mode).
    /// When true, changes are calculated but not written to disk.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets whether to transform C# source code files (controllers, services, etc.).
    /// </summary>
    public bool TransformSourceCode { get; init; } = true;

    /// <summary>
    /// Gets whether to migrate configuration files (web.config → appsettings.json).
    /// </summary>
    public bool TransformConfig { get; init; } = true;

    /// <summary>
    /// Gets whether to generate _ViewImports.cshtml for MVC projects.
    /// </summary>
    public bool GenerateViewImports { get; init; } = true;

    /// <summary>
    /// Gets the target migration type for WCF services.
    /// </summary>
    public MigrationTarget WcfTarget { get; init; } = MigrationTarget.GrpcAndRest;

    /// <summary>
    /// Gets whether to transform Entity Framework 6 code to EF Core.
    /// </summary>
    public bool TransformEntityFramework { get; init; } = true;

    /// <summary>
    /// Gets whether to transform WCF services.
    /// </summary>
    public bool TransformWcfServices { get; init; } = true;

    /// <summary>
    /// Gets whether to transform MVC Areas (AreaRegistration classes).
    /// </summary>
    public bool TransformAreas { get; init; } = true;

    /// <summary>
    /// Gets whether to transform BundleConfig.cs to modern asset pipeline.
    /// </summary>
    public bool TransformBundles { get; init; } = true;

    /// <summary>
    /// Gets the target build tool for bundle migration.
    /// </summary>
    public BundleTarget BundleTarget { get; init; } = BundleTarget.Vite;
}

/// <summary>
/// Target build tool for bundle migration.
/// </summary>
public enum BundleTarget
{
    /// <summary>
    /// Generate Vite configuration (recommended, default).
    /// </summary>
    Vite,

    /// <summary>
    /// Generate Webpack configuration.
    /// </summary>
    Webpack
}

/// <summary>
/// Target type for WCF service migration.
/// </summary>
public enum MigrationTarget
{
    /// <summary>
    /// Migrate WCF services to gRPC only.
    /// </summary>
    Grpc,

    /// <summary>
    /// Migrate WCF services to REST API only.
    /// </summary>
    Rest,

    /// <summary>
    /// Migrate WCF services to both gRPC and REST API (default).
    /// </summary>
    GrpcAndRest
}

/// <summary>
/// Comprehensive result of a migration operation.
/// Contains all file changes, diagnostics, confidence scores, and manual tasks.
/// </summary>
public record MigrationResult
{
    /// <summary>
    /// Gets whether the migration completed successfully without critical errors.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets all file changes (creates, modifications, deletions, backups).
    /// </summary>
    public IReadOnlyList<FileChange> Changes { get; init; } = Array.Empty<FileChange>();

    /// <summary>
    /// Gets all diagnostics (info, warnings, errors) generated during migration.
    /// </summary>
    public IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; } = Array.Empty<MigrationDiagnostic>();

    /// <summary>
    /// Gets the overall confidence score (0-100) for the migration.
    /// Aggregated from all individual transformation confidence scores.
    /// </summary>
    public int OverallConfidence { get; init; }

    /// <summary>
    /// Gets manual tasks that require developer intervention.
    /// Generated when automatic transformation is not possible or confidence is low.
    /// </summary>
    public IReadOnlyList<string> ManualTasks { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the total number of files transformed.
    /// </summary>
    public int FilesTransformed { get; init; }

    /// <summary>
    /// Gets the elapsed time for the migration operation.
    /// </summary>
    public TimeSpan ElapsedTime { get; init; }
}

/// <summary>
/// Represents a file change made during migration.
/// </summary>
public record FileChange
{
    /// <summary>
    /// Gets the absolute path to the file being changed.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type of change (Create, Modify, Delete, Backup).
    /// </summary>
    public ChangeType Type { get; init; }

    /// <summary>
    /// Gets the original file content (for modifications and deletions).
    /// Null for new files.
    /// </summary>
    public string? OriginalContent { get; init; }

    /// <summary>
    /// Gets the new file content (for creations and modifications).
    /// Null for deletions.
    /// </summary>
    public string? NewContent { get; init; }

    /// <summary>
    /// Gets the confidence score (0-100) for this specific transformation.
    /// Used to determine if manual review is recommended.
    /// </summary>
    public int Confidence { get; init; }

    /// <summary>
    /// Gets the description of the change.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Represents a diagnostic message generated during migration.
/// </summary>
public record MigrationDiagnostic
{
    /// <summary>
    /// Gets the severity level of this diagnostic.
    /// </summary>
    public DiagnosticLevel Level { get; init; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the file path related to this diagnostic (if applicable).
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets the line number in the file (if applicable).
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Gets the diagnostic code for categorization.
    /// </summary>
    public string? Code { get; init; }
}

/// <summary>
/// Severity level for migration diagnostics.
/// </summary>
public enum DiagnosticLevel
{
    /// <summary>
    /// Informational message (e.g., "Applied transformation with 95% confidence").
    /// </summary>
    Info,

    /// <summary>
    /// Warning message (e.g., "Low confidence transformation, manual review recommended").
    /// </summary>
    Warning,

    /// <summary>
    /// Error message (e.g., "Unable to transform, manual intervention required").
    /// </summary>
    Error
}
