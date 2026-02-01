namespace NetLift.Core.Models;

/// <summary>
/// Summary statistics for a dry-run report.
/// </summary>
public sealed class DryRunSummary
{
    /// <summary>
    /// Gets or sets the number of files to be created.
    /// </summary>
    public int FilesToCreate { get; set; }

    /// <summary>
    /// Gets or sets the number of files to be modified.
    /// </summary>
    public int FilesToModify { get; set; }

    /// <summary>
    /// Gets or sets the number of files to be deleted.
    /// </summary>
    public int FilesToDelete { get; set; }

    /// <summary>
    /// Gets or sets the number of backups to be created.
    /// </summary>
    public int FilesToBackup { get; set; }

    /// <summary>
    /// Gets or sets the total number of files affected.
    /// </summary>
    public int TotalFilesAffected { get; set; }

    /// <summary>
    /// Gets or sets the number of warnings.
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    /// Gets or sets the number of errors.
    /// </summary>
    public int ErrorCount { get; set; }
}
