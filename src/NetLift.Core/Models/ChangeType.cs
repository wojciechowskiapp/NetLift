namespace NetLift.Core.Models;

/// <summary>
/// Represents the type of change planned during a dry-run.
/// </summary>
public enum ChangeType
{
    /// <summary>
    /// A new file will be created.
    /// </summary>
    Create,

    /// <summary>
    /// An existing file will be modified.
    /// </summary>
    Modify,

    /// <summary>
    /// An existing file will be deleted.
    /// </summary>
    Delete,

    /// <summary>
    /// A backup of an existing file will be created.
    /// </summary>
    Backup
}
