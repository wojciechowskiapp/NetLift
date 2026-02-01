namespace NetLift.Core.Models;

/// <summary>
/// Represents a recommended phase in the migration plan.
/// </summary>
public class MigrationPhase
{
    /// <summary>
    /// Gets or sets the order of the phase.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the name of the phase.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the phase.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of projects affected in this phase.
    /// </summary>
    public List<string> AffectedProjects { get; set; } = new();

    /// <summary>
    /// Gets or sets the estimated percentage of automated migration for this phase.
    /// </summary>
    public int EstimatedAutoPercentage { get; set; }

    /// <summary>
    /// Gets or sets the list of manual steps required for this phase.
    /// </summary>
    public List<string> ManualSteps { get; set; } = new();
}
