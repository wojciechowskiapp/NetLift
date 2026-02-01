namespace NetLift.Core.Models;

/// <summary>
/// Represents the migration complexity assessment for a project.
/// </summary>
public class MigrationComplexity
{
    /// <summary>
    /// Gets or sets the complexity level.
    /// </summary>
    public ComplexityLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the complexity score (0-100).
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Gets or sets the factors contributing to the complexity.
    /// </summary>
    public List<string> Factors { get; set; } = new();
}
