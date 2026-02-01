namespace NetLift.Core.Models;

/// <summary>
/// Defines the action to take when mapping a legacy package to a modern equivalent.
/// </summary>
public enum MappingAction
{
    /// <summary>
    /// Keep the package as-is (possibly with version upgrade).
    /// </summary>
    Keep,

    /// <summary>
    /// Replace the package with a modern equivalent.
    /// </summary>
    Replace,

    /// <summary>
    /// Remove the package (functionality is now built into the framework).
    /// </summary>
    Remove,

    /// <summary>
    /// Upgrade to a newer version of the same package.
    /// </summary>
    Upgrade,

    /// <summary>
    /// Manual review required - no automatic mapping available.
    /// </summary>
    Manual
}
