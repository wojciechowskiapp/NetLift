namespace NetLift.Core.Models;

/// <summary>
/// Represents a comprehensive report of all changes that would be made during a migration.
/// </summary>
public sealed class DryRunReport
{
    /// <summary>
    /// Gets or sets a value indicating whether the migration would succeed.
    /// </summary>
    public bool WouldSucceed { get; set; }

    /// <summary>
    /// Gets or sets the list of file diffs.
    /// </summary>
    public List<FileDiff> FileDiffs { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets the summary statistics.
    /// </summary>
    public DryRunSummary Summary { get; set; } = new();
}
