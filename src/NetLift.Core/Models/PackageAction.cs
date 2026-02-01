namespace NetLift.Core.Models;

/// <summary>
/// Represents the action to take for a package during conversion.
/// </summary>
public enum PackageAction
{
    /// <summary>
    /// Keep the package as-is.
    /// </summary>
    Keep,

    /// <summary>
    /// Replace with a different package or version.
    /// </summary>
    Replace,

    /// <summary>
    /// Remove the package (now part of framework).
    /// </summary>
    Remove,

    /// <summary>
    /// Requires manual review and decision.
    /// </summary>
    Manual
}
