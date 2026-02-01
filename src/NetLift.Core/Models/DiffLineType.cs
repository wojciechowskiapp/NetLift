namespace NetLift.Core.Models;

/// <summary>
/// Represents the type of a line in a diff.
/// </summary>
public enum DiffLineType
{
    /// <summary>
    /// Contextual line (unchanged).
    /// </summary>
    Context,

    /// <summary>
    /// Added line.
    /// </summary>
    Addition,

    /// <summary>
    /// Deleted line.
    /// </summary>
    Deletion
}
