namespace NetLift.Core.Interfaces;

/// <summary>
/// Migrates configuration files from web.config to appsettings.json and Program.cs.
/// Coordinates all config parsers and generators.
/// </summary>
public interface IConfigMigrationService
{
    /// <summary>
    /// Migrates web.config to modern .NET configuration.
    /// </summary>
    /// <param name="projectDirectory">The directory containing the project and web.config.</param>
    /// <param name="targetFramework">The target .NET framework (e.g., net8.0, net9.0).</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A list of file changes (appsettings.json, appsettings.Development.json, Program.cs).</returns>
    Task<ConfigMigrationResult> MigrateConfigAsync(
        string projectDirectory,
        string targetFramework,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a configuration migration operation.
/// </summary>
public sealed record ConfigMigrationResult
{
    /// <summary>
    /// Gets a value indicating whether the migration was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the list of file changes made during migration.
    /// </summary>
    public IReadOnlyList<FileChange> GeneratedFiles { get; init; } = [];

    /// <summary>
    /// Gets the list of diagnostic messages (warnings, info, errors).
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets the confidence score (0-100) for the migration.
    /// Based on the confidence scoring system:
    /// - 95-100: Auto-apply, no review needed
    /// - 80-94: Auto-apply with INFO comment
    /// - 60-79: Apply with TODO, recommend review
    /// - &lt;60: Don't auto-apply, generate manual task
    /// </summary>
    public int Confidence { get; init; }
}
