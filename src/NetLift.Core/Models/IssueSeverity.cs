namespace NetLift.Core.Models;

/// <summary>
/// Represents the severity level of a compatibility issue.
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// Informational only, no action needed.
    /// </summary>
    Info,

    /// <summary>
    /// Warning, may need attention.
    /// </summary>
    Warning,

    /// <summary>
    /// Error, must be addressed for migration.
    /// </summary>
    Error,

    /// <summary>
    /// Blocker, cannot migrate without fixing this issue.
    /// </summary>
    Blocker
}
