namespace NetLift.Core.Models;

/// <summary>
/// Represents the user's choice during interactive migration.
/// </summary>
public enum InteractiveChoice
{
    /// <summary>
    /// Apply the migration for this project.
    /// </summary>
    Apply,

    /// <summary>
    /// Skip the migration for this project.
    /// </summary>
    Skip,

    /// <summary>
    /// Apply the migration for this project and all remaining projects without further prompts.
    /// </summary>
    ApplyAll,

    /// <summary>
    /// Abort the entire migration process.
    /// </summary>
    Abort,

    /// <summary>
    /// Preview the changes that would be made to this project.
    /// </summary>
    Preview
}
