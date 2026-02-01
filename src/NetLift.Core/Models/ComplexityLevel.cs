namespace NetLift.Core.Models;

/// <summary>
/// Represents the level of migration complexity.
/// </summary>
public enum ComplexityLevel
{
    /// <summary>
    /// Low complexity (0-25): Mostly automated migration possible.
    /// </summary>
    Low,

    /// <summary>
    /// Medium complexity (26-50): Some manual work required.
    /// </summary>
    Medium,

    /// <summary>
    /// High complexity (51-75): Significant manual work required.
    /// </summary>
    High,

    /// <summary>
    /// Very high complexity (76-100): Major rewrite needed.
    /// </summary>
    VeryHigh
}
